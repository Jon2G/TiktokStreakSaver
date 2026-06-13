import AppIntents

/// App Shortcuts metadata must live in the main app executable (static StreakAppIntents framework force-loaded by MAUI).
/// The widget extension only embeds MaintainStreakIntent for the widget button.
public struct StreakShortcuts: AppShortcutsProvider {
    public static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: MaintainStreakShortcutIntent(),
            phrases: [
                "Maintain TikTok streaks with \(.applicationName)",
                "Send streak messages with \(.applicationName)",
                "Run streak saver with \(.applicationName)"
            ],
            shortTitle: "Maintain Streaks",
            systemImageName: "flame.fill")
    }
}
