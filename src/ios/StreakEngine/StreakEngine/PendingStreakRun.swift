import Foundation

/// Queues a streak run for the main app. Widget / Shortcuts must not call WKWebView in the extension process.
public enum PendingStreakRun {
    private static let pendingKey = "pending_streak_run"

    public static func request() {
        let defaults = AppGroupSupport.userDefaults
        defaults.set(true, forKey: pendingKey)
        defaults.synchronize()
    }

    @discardableResult
    public static func consume() -> Bool {
        let defaults = AppGroupSupport.userDefaults
        let pending = defaults.bool(forKey: pendingKey)
        if pending {
            defaults.removeObject(forKey: pendingKey)
            defaults.synchronize()
        }
        return pending
    }
}
