import Foundation
import Network
import os
import UIKit
import WebKit

@objc public final class StreakWebViewRunner: NSObject, WKNavigationDelegate, WKScriptMessageHandler {
    public static let shared = StreakWebViewRunner()

    private static let logger = Logger(subsystem: "com.jon2g.tiktokstreaksaver", category: "StreakWebViewRunner")
    private static let webViewSize = CGSize(width: 390, height: 844)
    private static let friendAutomationTimeout: TimeInterval = 90

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
    private var friendTimeoutWork: DispatchWorkItem?
    private var pendingFriendUsername: String?
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
        RunLogStore.append("storage_path=\(RunLogStore.storagePath())")
        Self.logger.info("run_start path=\(RunLogStore.storagePath(), privacy: .public)")

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
            runResult.success = false
            runResult.errorMessage = "No friends due for a streak message today"
            settings.addRunResult(runResult)
            StreakNotifications.notify(title: "Streak Saver", body: "No friends due for a streak message today.", isAlert: false)
            RunLogStore.append("no_friends_due")
            finish(success: false)
            return
        }

        scheduleOverallTimeout()

        let cookies = CookieImporter.loadExportedCookies()
        RunLogStore.append("cookie_check count=\(cookies.count)")
        Self.logger.info("cookie_check count=\(cookies.count)")
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

        let view = WKWebView(frame: CGRect(origin: .zero, size: Self.webViewSize), configuration: config)
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
        // Match Android StreakService: drop whole-line // comments, then compact whitespace.
        // Do NOT strip newlines alone — that turns the file header comment into an end-of-file comment
        // and prevents the automation IIFE from ever running (js_eval_ok with zero js bridge logs).
        let withoutCommentLines = script
            .split(omittingEmptySubsequences: false, whereSeparator: \.isNewline)
            .filter { line in
                !line.trimmingCharacters(in: .whitespaces).hasPrefix("//")
            }
            .joined(separator: "\n")
        return withoutCommentLines
            .replacingOccurrences(of: #"\s+"#, with: " ", options: .regularExpression)
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }

    public func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        guard let url = webView.url?.absoluteString.lowercased() else { return }
        RunLogStore.append("nav_finish \(url)")
        Self.logger.info("nav_finish \(url, privacy: .public)")

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
        scheduleFriendTimeout(for: friend)

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
            guard let self else { return }

            if let error {
                self.cancelFriendTimeout()
                self.pendingFriendUsername = nil
                RunLogStore.append("js_eval_error \(friend.username): \(error.localizedDescription)")
                self.recordFriendResult(friend: friend, success: false, error: error.localizedDescription)
                self.friendIndex += 1
                DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) { self.processNextFriend() }
                return
            }

            RunLogStore.append("js_eval_ok \(friend.username)")
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
            cancelFriendTimeout()
            pendingFriendUsername = nil
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

        if !runResult.success && successes == 0 && !runResult.friendResults.isEmpty {
            let failed = runResult.friendResults.filter { !$0.success }
            runResult.errorMessage = failed.map { result in
                let detail = result.errorMessage?.trimmingCharacters(in: .whitespacesAndNewlines) ?? "failed"
                return "@\(result.username): \(detail)"
            }.joined(separator: "; ")
            settings.setString(SharedConstants.lastRunFailureReasonKey, SharedConstants.failureSendError)
        } else if runResult.success {
            settings.setString(SharedConstants.lastRunFailureReasonKey, "")
        }

        settings.addRunResult(runResult)
        settings.setLastRun(Date())
        settings.setBool(SharedConstants.authRequiredKey, false)

        if runResult.success {
            RunLogStore.append("run_finish success=true sent=\(successes) total=\(runResult.friendResults.count)")
            Self.logger.info("run_finish success=true sent=\(successes) total=\(self.runResult.friendResults.count)")
        } else {
            RunLogStore.append("run_finish success=false sent=\(successes) total=\(runResult.friendResults.count) reason=\(settings.getString(SharedConstants.lastRunFailureReasonKey))")
            Self.logger.error("run_finish success=false sent=\(successes) total=\(self.runResult.friendResults.count)")
        }

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
        Self.logger.error("run_fail \(reason, privacy: .public): \(message, privacy: .public)")
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

        RunLogStore.mirrorSnippetToDefaults()

        let cb = completion
        completion = nil
        cb?(success)
    }

    private func attachWebViewToWindow(_ webView: WKWebView) {
        let scene = UIApplication.shared.connectedScenes
            .compactMap { $0 as? UIWindowScene }
            .first { $0.activationState == .foregroundActive || $0.activationState == .foregroundInactive }
            ?? UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first

        let size = Self.webViewSize
        let window: UIWindow
        if let scene {
            window = UIWindow(windowScene: scene)
        } else {
            window = UIWindow(frame: CGRect(origin: .zero, size: size))
        }
        window.windowLevel = .normal - 1
        // Full-size off-screen window so TikTok hydrates the messages DOM (1x1 windows stall JS).
        window.frame = CGRect(x: -size.width, y: 0, width: size.width, height: size.height)
        window.alpha = 0.01
        window.isHidden = false

        let host = UIViewController()
        host.view.frame = CGRect(origin: .zero, size: size)
        webView.frame = host.view.bounds
        webView.autoresizingMask = [.flexibleWidth, .flexibleHeight]
        host.view.addSubview(webView)
        window.rootViewController = host
        window.makeKeyAndVisible()
        hostWindow = window
        RunLogStore.append("webview_host size=\(Int(size.width))x\(Int(size.height))")
    }

    private func detachWebViewFromWindow() {
        hostWindow?.isHidden = true
        hostWindow?.rootViewController = nil
        hostWindow = nil
    }

    private func scheduleFriendTimeout(for friend: FriendConfig) {
        cancelFriendTimeout()
        pendingFriendUsername = friend.username
        let username = friend.username
        let work = DispatchWorkItem { [weak self] in
            guard let self, !self.didFinishRun else { return }
            guard self.pendingFriendUsername == username else { return }
            guard self.friendIndex < self.friends.count,
                  self.friends[self.friendIndex].username == username else { return }

            let timedOutFriend = self.friends[self.friendIndex]
            self.pendingFriendUsername = nil
            RunLogStore.append("friend_timeout \(username) after=\(Int(Self.friendAutomationTimeout))s")
            self.recordFriendResult(
                friend: timedOutFriend,
                success: false,
                error: "Automation timed out after \(Int(Self.friendAutomationTimeout))s")
            self.friendIndex += 1
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) { self.processNextFriend() }
        }
        friendTimeoutWork = work
        DispatchQueue.main.asyncAfter(deadline: .now() + Self.friendAutomationTimeout, execute: work)
    }

    private func scheduleOverallTimeout() {
        cancelOverallTimeout()
        let perFriendBudget = Self.friendAutomationTimeout + 15
        let overallSeconds = max(90, Double(friends.count) * perFriendBudget + 30)
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
        RunLogStore.append("overall_timeout_scheduled seconds=\(Int(overallSeconds))")
        DispatchQueue.main.asyncAfter(deadline: .now() + overallSeconds, execute: work)
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
        cancelFriendTimeout()
    }

    private func cancelFriendTimeout() {
        friendTimeoutWork?.cancel()
        friendTimeoutWork = nil
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
