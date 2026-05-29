import AppIntents
import StreakEngine

/// Shortcuts / widget entry point. WKWebView automation runs in the main app, not the widget extension.
struct MaintainStreakIntent: AppIntent {
    static var title: LocalizedStringResource = "Maintain TikTok Streaks"
    static var description = IntentDescription(
        "Opens Streak Saver and sends streak messages to your enabled friends. Sign in inside the app first so your TikTok session is available.")
    /// Required: WKWebView cannot run inside the WidgetKit extension sandbox.
    static var openAppWhenRun: Bool = true

    func perform() async throws -> some IntentResult {
        PendingStreakRun.request()
        return .result()
    }
}

struct StreakShortcuts: AppShortcutsProvider {
    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: MaintainStreakIntent(),
            phrases: [
                "Maintain TikTok streaks with \(.applicationName)",
                "Send streak messages with \(.applicationName)",
                "Run streak saver with \(.applicationName)"
            ],
            shortTitle: "Maintain Streaks",
            systemImageName: "flame.fill")
    }
}
