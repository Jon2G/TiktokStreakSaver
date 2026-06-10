import Foundation

/// App Group container access. On Simulator without a development team, the group is unavailable —
/// we fall back to Application Support (matches MAUI FileSystem.AppDataDirectory).
enum AppGroupSupport {
    private static let lock = NSLock()
    private static var didProbe = false
    private static var appGroupContainer: URL?
    private static let migratedKey = "app_group_migrated_v1"

    static var isAvailable: Bool {
        probeIfNeeded()
        return appGroupContainer != nil
    }

    static var storageDirectory: URL {
        probeIfNeeded()
        if let appGroupContainer {
            return appGroupContainer
        }
        return mauiApplicationSupportDirectory()
    }

    static var userDefaults: UserDefaults {
        if isAvailable, let suite = UserDefaults(suiteName: SharedConstants.appGroupId) {
            return suite
        }
        return UserDefaults.standard
    }

    static func migrateFromStandardIfNeeded() {
        guard isAvailable, let suite = UserDefaults(suiteName: SharedConstants.appGroupId) else { return }
        if suite.bool(forKey: migratedKey) { return }

        let standard = UserDefaults.standard
        let keys = [
            SharedConstants.friendsListKey,
            SharedConstants.messageTextKey,
            SharedConstants.runHistoryKey,
            SharedConstants.authRequiredKey,
            SharedConstants.loginUserAgentKey,
            "session_valid",
            "session_display_name"
        ]

        for key in keys {
            if let existing = suite.string(forKey: key), !existing.isEmpty { continue }
            if let value = standard.string(forKey: key), !value.isEmpty {
                suite.set(value, forKey: key)
            }
        }

        suite.set(true, forKey: migratedKey)
        suite.synchronize()
    }

    private static func mauiApplicationSupportDirectory() -> URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? FileManager.default.urls(for: .libraryDirectory, in: .userDomainMask).first!
        let bundleId = Bundle.main.bundleIdentifier ?? "com.jon2g.tiktokstreaksaver"
        let dir = base.appendingPathComponent(bundleId, isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }

    private static func probeIfNeeded() {
        lock.lock()
        defer { lock.unlock() }
        guard !didProbe else { return }
        didProbe = true
        appGroupContainer = FileManager.default.containerURL(
            forSecurityApplicationGroupIdentifier: SharedConstants.appGroupId)
    }
}
