import AppIntents
import StreakEngine

/// Shortcuts / widget entry point. WKWebView automation runs in the main app, not the widget extension.
public struct MaintainStreakIntent: AppIntent {
    public static var title: LocalizedStringResource = "Maintain TikTok Streaks"
    public static var description = IntentDescription(
        "Opens Streak Saver and sends streak messages to your enabled friends. Sign in inside the app first so your TikTok session is available.")
    /// Required: WKWebView cannot run inside the WidgetKit extension sandbox.
    public static var openAppWhenRun: Bool = true

    public init() {}

    public func perform() async throws -> some IntentResult {
        PendingStreakRun.request()
        return .result()
    }
}
