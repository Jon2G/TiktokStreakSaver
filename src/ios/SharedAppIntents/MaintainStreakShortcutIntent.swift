import AppIntents
import StreakEngine

/// Main-app App Shortcut entry point. Routes through the MAUI streak runner when registered.
public struct MaintainStreakShortcutIntent: AppIntent {
    public static var title: LocalizedStringResource = "Maintain TikTok Streaks"
    public static var description = IntentDescription(
        "Opens Streak Saver and sends streak messages to your enabled friends. Sign in inside the app first so your TikTok session is available.")
    /// WKWebView automation requires a foreground app process on iOS.
    public static var openAppWhenRun: Bool = true

    public init() {}

    public func perform() async throws -> some IntentResult {
        await MainActor.run {
            if !StreakShortcutRequest.fromAppShortcut() {
                PendingStreakRun.request()
            }
        }
        return .result()
    }
}
