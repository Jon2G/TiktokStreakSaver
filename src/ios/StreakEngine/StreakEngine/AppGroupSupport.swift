import Foundation

/// App Group container access. On Simulator without a development team, the group is unavailable —
/// we fall back to the app Library folder (matches MAUI FileSystem.AppDataDirectory).
enum AppGroupSupport {
    private static let lock = NSLock()
    private static var didProbe = false
    private static var appGroupContainer: URL?

    static var isAvailable: Bool {
        probeIfNeeded()
        return appGroupContainer != nil
    }

    static var storageDirectory: URL {
        probeIfNeeded()
        if let appGroupContainer {
            return appGroupContainer
        }
        return FileManager.default.urls(for: .libraryDirectory, in: .userDomainMask).first!
    }

    static var userDefaults: UserDefaults {
        if isAvailable, let suite = UserDefaults(suiteName: SharedConstants.appGroupId) {
            return suite
        }
        return UserDefaults.standard
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
