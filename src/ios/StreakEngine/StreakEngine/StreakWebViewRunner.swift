import Foundation
import Network
import WebKit

@objc public final class StreakWebViewRunner: NSObject, WKNavigationDelegate, WKScriptMessageHandler {
    public static let shared = StreakWebViewRunner()

    private var webView: WKWebView?
    private var friends: [FriendConfig] = []
    private var friendIndex = 0
    private var runResult = StreakRunResult(runTime: Date(), duration: nil, success: true, errorMessage: nil, friendResults: [])
    private var baseScript = ""
    private var completion: ((Bool) -> Void)?
    private var messagesLoaded = false
    private var shuffledMessages: [String] = []
    private var messageIndex = 0
    private let settings = SharedSettings.shared

    private override init() {
        super.init()
    }

    @objc public func run(completion: @escaping (Bool) -> Void) {
        self.completion = completion
        friendIndex = 0
        messagesLoaded = false
        runResult = StreakRunResult(runTime: Date(), duration: nil, success: true, errorMessage: nil, friendResults: [])

        let monitor = NWPathMonitor()
        let queue = DispatchQueue(label: "streak.network")
        monitor.pathUpdateHandler = { [weak self] path in
            monitor.cancel()
            guard let self else { return }
            if path.status != .satisfied {
                self.settings.setBool("last_run_failed", true)
                self.settings.setString("last_run_failure_reason", SharedConstants.failureNoNetwork)
                StreakNotifications.notify(
                    title: "Streak Saver — Offline",
                    body: "No network connection. Try again when online.",
                    isAlert: true)
                completion(false)
                return
            }
            DispatchQueue.main.async { self.startRun() }
        }
        monitor.start(queue: queue)
    }

    private func startRun() {
        let all = settings.getFriends().filter { $0.isEnabled }
        let today = Calendar.current.startOfDay(for: Date())
        friends = all.filter { friend in
            guard let last = friend.lastMessageSent else { return true }
            return Calendar.current.startOfDay(for: last) < today
        }

        if friends.isEmpty {
            StreakNotifications.notify(title: "Streak Saver", body: "No friends due for a streak message today.", isAlert: false)
            finish(success: true)
            return
        }

        guard !CookieImporter.loadExportedCookies().isEmpty else {
            settings.setBool(SharedConstants.authRequiredKey, true)
            StreakNotifications.notify(
                title: "Streak Saver — Login required",
                body: "Open the app and sign in to TikTok again.",
                isAlert: true)
            finish(success: false)
            return
        }

        if settings.getRandomizeMessages() {
            shuffledMessages = settings.builtInMessages().shuffled()
            messageIndex = 0
        }

        baseScript = loadAutomationScript()

        let config = WKWebViewConfiguration()
        config.defaultWebpagePreferences.allowsContentJavaScript = true
        config.userContentController.add(self, name: "streakBridge")
        let prefs = WKWebpagePreferences()
        prefs.allowsContentJavaScript = true
        config.defaultWebpagePreferences = prefs

        webView = WKWebView(frame: CGRect(x: 0, y: 0, width: 390, height: 844), configuration: config)
        webView?.customUserAgent = settings.getLoginUserAgent()
        webView?.navigationDelegate = self

        let cookies = CookieImporter.loadExportedCookies()
        CookieImporter.apply(cookies: cookies, to: config.websiteDataStore.httpCookieStore) { [weak self] in
            guard let self, let url = URL(string: SharedConstants.messagesUrl) else { return }
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
        if url.contains("/login") {
            settings.setBool(SharedConstants.authRequiredKey, true)
            runResult.success = false
            runResult.errorMessage = "TikTok login required"
            StreakNotifications.notify(
                title: "Streak Saver — Login required",
                body: "Session expired. Open the app to sign in again.",
                isAlert: true)
            finish(success: false)
            return
        }

        if url.contains("/messages") && !messagesLoaded {
            messagesLoaded = true
            processNextFriend()
        }
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

        if type == "done", friendIndex < friends.count {
            let friend = friends[friendIndex]
            let ok = (body["ok"] as? Bool) ?? false
            let err = body["err"] as? String
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
        let successes = runResult.friendResults.filter { $0.success }.count
        runResult.success = successes > 0 || runResult.friendResults.isEmpty
        if successes < runResult.friendResults.count && !runResult.friendResults.isEmpty {
            runResult.success = settings.getSkipUnreachable()
        }

        settings.addRunResult(runResult)
        settings.setLastRun(Date())
        settings.setBool(SharedConstants.authRequiredKey, false)

        StreakNotifications.notify(
            title: "Streak Saver — Done",
            body: "Sent \(successes) of \(runResult.friendResults.count) messages.",
            isAlert: false)
        finish(success: runResult.success)
    }

    private func finish(success: Bool) {
        webView?.navigationDelegate = nil
        webView?.configuration.userContentController.removeScriptMessageHandler(forName: "streakBridge")
        webView = nil
        let cb = completion
        completion = nil
        cb?(success)
    }
}
