import AppIntents

/// Registers App Intents metadata with the main app binary when StreakAppIntents is force-loaded.
public struct StreakAppIntentsPackage: AppIntentsPackage {
    public static var includedPackages: [AppIntentsPackage.Type] { [] }
}
