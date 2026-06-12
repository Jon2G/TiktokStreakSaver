import Foundation
import Network
import UIKit
import WebKit

@objc public final class StreakWebViewRunner: NSObject, WKNavigationDelegate, WKScriptMessageHandler {
    public static let shared = StreakWebViewRunner()

    private var webView: WKWebView?
    private var hostWindow: UIWindow?
    private var friends: [FriendConfig] = []
    private var friendIndex = 0
    private var runResult = StreakRunResult(runTime: Date(), duration: nil, success: true, errorMessage: nil, friendResults: [])
    private var baseScript = ""
    private var completion: ((Bool) -> Void)?
    private var messagesLoaded = false
    private var shuffledMessages: [String] = []
    private var messageIndex = 0
    private var runStartedAt = Date()
    private var overallTimeoutWork: DispatchWorkItem?
    private var messagesTimeoutWork: DispatchWorkItem?
    private var didFinishRun = false
    private let settings = SharedSettings.shared

    private override init() {
        super.init()
    }

    @objc public func run(completion: @escaping (Bool) -> Void) {
        self.completion = completion
        friendIndex = 0
        messagesLoaded = false
        didFinishRun = false
        runStartedAt = Date()
        runResult = StreakRunResult(runTime: Date(), duration: nil, success: true, errorMessage: nil, friendResults: [])

        RunLogStore.clear()
        RunLogStore.append("run_start")

        let monitor = NWPathMonitor()
        let queue = DispatchQueue(label: "streak.network")
        monitor.pathUpdateHandler = { [weak self] path in
            monitor.cancel()
            guard let self else { return }
            if path.status != .satisfied {
                self.failRun(
                    reason: SharedConstants.failureNoNetwork,
                    message: "No network connection",
                    notifyTitle: "Streak Saver — Offline",
                    notifyBody: "No network connection. Try again when online.")
                return
            }
            DispatchQueue.main.async { self.startRun() }
        }
        monitor.start(queue: queue)
    }

    private func startRun() {
        scheduleOverallTimeout()

        let all = settings.getFriends().filter { $0.isEnabled }
        let forceManual = settings.getBool(SharedConstants.forceManualRunKey)

        if forceManual {
            friends = all
            RunLogStore.append("manual_run force_all enabled=\(all.count)")
        } else {
            let today = Calendar.current.startOfDay(for: Date())
            friends = all.filter { friend in
                guard let last = friend.lastMessageSent else { return true }
                return Calendar.current.startOfDay(for: last) < today
            }
            RunLogStore.append("scheduled_run due=\(friends.count) enabled=\(all.count)")
        }

        if friends.isEmpty {
            settings.setString(SharedConstants.lastRunFailureReasonKey, SharedConstants.failureNoFriendsDue)
            StreakNotifications.notify(title: "Streak Saver", body: "No friends due for a streak message today.", isAlert: false)
            RunLogStore.append("no_friends_due")
            finish(success: false)
            return
        }

        let cookies = CookieImporter.loadExportedCookies()
        RunLogStore.append("cookie_check count=\(cookies.count)")
        guard !cookies.isEmpty else {
            settings.setBool(SharedConstants.authRequiredKey, true)
            failRun(
                reason: SharedConstants.failureNoCookies,
                message: "Missing exported cookies",
                notifyTitle: "Streak Saver — Login required",
                notifyBody: "Open the app and sign in to TikTok again.")
            return
        }

        baseScript = loadAutomationScript()
        guard !baseScript.isEmpty else {
            failRun(
                reason: SharedConstants.failureScriptMissing,
                message: "tiktok_automation.js missing from bundle",
                notifyTitle: "Streak Saver — Error",
                notifyBody: "Automation script is missing. Reinstall the app.")
            return
        }

        if settings.getRandomizeMessages() {
            shuffledMessages = settings.builtInMessages().shuffled()
            messageIndex = 0
        }

        let config = WKWebViewConfiguration()
        config.defaultWebpagePreferences.allowsContentJavaScript = true
        config.userContentController.add(self, name: "streakBridge")
        let prefs = WKWebpagePreferences()
        prefs.allowsContentJavaScript = true
        config.defaultWebpagePreferences = prefs

        let view = WKWebView(frame: CGRect(x: 0, y: 0, width: 390, height: 844), configuration: config)
        view.customUserAgent = settings.getLoginUserAgent()
        view.navigationDelegate = self
        webView = view
        attachWebViewToWindow(view)

        scheduleMessagesTimeout()
        CookieImporter.apply(cookies: cookies, to: config.websiteDataStore.httpCookieStore) { [weak self] in
            guard let self, let url = URL(string: SharedConstants.messagesUrl) else { return }
            RunLogStore.append("loading_messages")
            self.webView?.load(URLRequest(url: url))
        }
    }

    private func loadAutomationScript() -> String {
        guard let url = Bundle(for: StreakWebViewRunner.self).url(forResource: "tiktok_automation", withExtension: "js"),
              let script = try? String(contentsOf: url, encoding: .utf8) else {
            return ""
        }
        return script
            .replacingOccurrences(of: "\n", with: "")
            .replacingOccurrences(of: "\r", with: "")
            .replacingOccurrences(of: "  ", with: " ")
    }

    public func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        guard let url = webView.url?.absoluteString.lowercased() else { return }
        RunLogStore.append("nav_finish \(url)")

        if url.contains("/login") {
            settings.setBool(SharedConstants.authRequiredKey, true)
            runResult.success = false
            runResult.errorMessage = "TikTok login required"
            failRun(
                reason: SharedConstants.failureLoginRequired,
                message: "Redirected to login",
                notifyTitle: "Streak Saver — Login required",
                notifyBody: "Session expired. Open the app to sign in again.")
            return
        }

        if url.contains("/messages") && !messagesLoaded {
            messagesLoaded = true
            cancelMessagesTimeout()
            RunLogStore.append("messages_ready friends=\(friends.count)")
            processNextFriend()
        }
    }

    public func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
        navigationFailed(error)
    }

    public func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
        navigationFailed(error)
    }

    private func navigationFailed(_ error: Error) {
        RunLogStore.append("nav_failed \(error.localizedDescription)")
        runResult.success = false
        runResult.errorMessage = error.localizedDescription
        failRun(
            reason: SharedConstants.failureNavigationFailed,
            message: error.localizedDescription,
            notifyTitle: "Streak Saver — Error",
            notifyBody: "Could not load TikTok messages.")
    }

    private func processNextFriend() {
        guard friendIndex < friends.count, let webView else {
            completeRun()
            return
        }

        let friend = friends[friendIndex]
        let lookupName = friend.isGroup
            ? (friend.displayName.isEmpty ? friend.username : friend.displayName)
            : friend.username
        let message = pickMessage()

        RunLogStore.append("friend_start \(friend.username) index=\(friendIndex + 1)/\(friends.count)")

        var script = baseScript
            .replacingOccurrences(of: "[UserName]", with: lookupName.replacingOccurrences(of: "'", with: "\\'"))
            .replacingOccurrences(of: "[Message]", with: message.replacingOccurrences(of: "'", with: "\\'"))
            .replacingOccurrences(of: "[IsGroup]", with: friend.isGroup ? "true" : "false")

        let bridge = """
        window.StreakApp = window.StreakApp || {
          log: function(m) { webkit.messageHandlers.streakBridge.postMessage({t:'log',m:m}); },
          onMessageSent: function(u, ok, err) { webkit.messageHandlers.streakBridge.postMessage({t:'done',u:u,ok:ok,err:err||''}); }
        };
        """
        script = bridge + script

        webView.evaluateJavaScript(script) { [weak self] _, error in
            if let error {
                RunLogStore.append("js_eval_error \(friend.username): \(error.localizedDescription)")
                self?.recordFriendResult(friend: friend, success: false, error: error.localizedDescription)
                self?.friendIndex += 1
                DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) { self?.processNextFriend() }
            }
        }
    }

    private func pickMessage() -> String {
        if settings.getRandomizeMessages(), !shuffledMessages.isEmpty {
            let msg = shuffledMessages[messageIndex % shuffledMessages.count]
            messageIndex += 1
            return msg
        }
        return settings.getMessageText()
    }

    public func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        guard let body = message.body as? [String: Any],
              let type = body["t"] as? String else { return }

        if type == "log", let text = body["m"] as? String {
            RunLogStore.append("js \(text)")
            return
        }

        if type == "done", friendIndex < friends.count {
            let friend = friends[friendIndex]
            let ok = (body["ok"] as? Bool) ?? false
            let err = body["err"] as? String
            RunLogStore.append("friend_done \(friend.username) ok=\(ok) err=\(err ?? "")")
            recordFriendResult(friend: friend, success: ok, error: err)
            friendIndex += 1
            DispatchQueue.main.asyncAfter(deadline: .now() + 2.0) { [weak self] in
                self?.processNextFriend()
            }
        }
    }

    private func recordFriendResult(friend: FriendConfig, success: Bool, error: String?) {
        runResult.friendResults.append(FriendMessageResult(
            friendId: friend.id,
            username: friend.username,
            success: success,
            errorMessage: error,
            timestamp: Date()))

        var allFriends = settings.getFriends()
        if let idx = allFriends.firstIndex(where: { $0.id == friend.id }) {
            if success {
                allFriends[idx].lastMessageSent = Date()
                allFriends[idx].successCount += 1
            } else {
                allFriends[idx].failureCount += 1
            }
            settings.saveFriends(allFriends)
        }
    }

    private func completeRun() {
        cancelTimeouts()

        let successes = runResult.friendResults.filter { $0.success }.count
        runResult.success = successes > 0 || runResult.friendResults.isEmpty
        if successes < runResult.friendResults.count && !runResult.friendResults.isEmpty {
            runResult.success = settings.getSkipUnreachable()
        }

        let duration = Date().timeIntervalSince(runStartedAt)
        runResult.duration = String(format: "%.1fs", duration)

        settings.addRunResult(runResult)
        settings.setLastRun(Date())
        settings.setBool(SharedConstants.authRequiredKey, false)
        settings.setString(SharedConstants.lastRunFailureReasonKey, "")

        RunLogStore.append("run_finish sent=\(successes) total=\(runResult.friendResults.count)")

        StreakNotifications.notify(
            title: "Streak Saver — Done",
            body: "Sent \(successes) of \(runResult.friendResults.count) messages.",
            isAlert: false)
        finish(success: runResult.success)
    }

    private func failRun(reason: String, message: String, notifyTitle: String, notifyBody: String) {
        cancelTimeouts()
        settings.setBool("last_run_failed", true)
        settings.setString(SharedConstants.lastRunFailureReasonKey, reason)
        runResult.success = false
        runResult.errorMessage = message
        settings.addRunResult(runResult)
        RunLogStore.append("run_fail \(reason): \(message)")
        StreakNotifications.notify(title: notifyTitle, body: notifyBody, isAlert: true)
        finish(success: false)
    }

    private func finish(success: Bool) {
        guard !didFinishRun else { return }
        didFinishRun = true

        cancelTimeouts()
        settings.setBool(SharedConstants.forceManualRunKey, false)

        webView?.navigationDelegate = nil
        webView?.configuration.userContentController.removeScriptMessageHandler(forName: "streakBridge")
        webView?.removeFromSuperview()
        webView = nil
        detachWebViewFromWindow()

        let cb = completion
        completion = nil
        cb?(success)
    }

    private func attachWebViewToWindow(_ webView: WKWebView) {
        let scene = UIApplication.shared.connectedScenes
            .compactMap { $0 as? UIWindowScene }
            .first { $0.activationState == .foregroundActive || $0.activationState == .foregroundInactive }
            ?? UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first

        let window: UIWindow
        if let scene {
            window = UIWindow(windowScene: scene)
        } else {
            window = UIWindow(frame: UIScreen.main.bounds)
        }
        window.windowLevel = .normal - 1
        window.frame = CGRect(x: 0, y: 0, width: 1, height: 1)
        window.alpha = 0.01
        window.isHidden = false

        let host = UIViewController()
        host.view.addSubview(webView)
        window.rootViewController = host
        window.makeKeyAndVisible()
        hostWindow = window
    }

    private func detachWebViewFromWindow() {
        hostWindow?.isHidden = true
        hostWindow?.rootViewController = nil
        hostWindow = nil
    }

    private func scheduleOverallTimeout() {
        cancelOverallTimeout()
        let work = DispatchWorkItem { [weak self] in
            guard let self, !self.didFinishRun else { return }
            self.runResult.success = false
            self.runResult.errorMessage = "Run timed out"
            self.failRun(
                reason: SharedConstants.failureTimedOut,
                message: "Run timed out",
                notifyTitle: "Streak Saver — Timed out",
                notifyBody: "The streak run took too long. Try again.")
        }
        overallTimeoutWork = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 90, execute: work)
    }

    private func scheduleMessagesTimeout() {
        cancelMessagesTimeout()
        let work = DispatchWorkItem { [weak self] in
            guard let self, !self.didFinishRun, !self.messagesLoaded else { return }
            self.failRun(
                reason: SharedConstants.failureTimedOut,
                message: "Messages page timed out",
                notifyTitle: "Streak Saver — Timed out",
                notifyBody: "TikTok messages did not load in time.")
        }
        messagesTimeoutWork = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 30, execute: work)
    }

    private func cancelTimeouts() {
        cancelOverallTimeout()
        cancelMessagesTimeout()
    }

    private func cancelOverallTimeout() {
        overallTimeoutWork?.cancel()
        overallTimeoutWork = nil
    }

    private func cancelMessagesTimeout() {
        messagesTimeoutWork?.cancel()
        messagesTimeoutWork = nil
    }
}
