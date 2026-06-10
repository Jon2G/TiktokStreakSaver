import Foundation

public struct FriendConfig: Codable {
    public var id: String
    public var username: String
    public var displayName: String
    public var isEnabled: Bool
    public var lastMessageSent: Date?
    public var successCount: Int
    public var failureCount: Int
    public var isGroup: Bool

    enum CodingKeys: String, CodingKey {
        case id = "Id"
        case username = "Username"
        case displayName = "DisplayName"
        case isEnabled = "IsEnabled"
        case lastMessageSent = "LastMessageSent"
        case successCount = "SuccessCount"
        case failureCount = "FailureCount"
        case isGroup = "IsGroup"
    }
}

public struct FriendMessageResult: Codable {
    public var friendId: String
    public var username: String
    public var success: Bool
    public var errorMessage: String?
    public var timestamp: Date

    enum CodingKeys: String, CodingKey {
        case friendId = "FriendId"
        case username = "Username"
        case success = "Success"
        case errorMessage = "ErrorMessage"
        case timestamp = "Timestamp"
    }
}

public struct StreakRunResult: Codable {
    public var runTime: Date
    public var duration: String?
    public var success: Bool
    public var errorMessage: String?
    public var friendResults: [FriendMessageResult]

    enum CodingKeys: String, CodingKey {
        case runTime = "RunTime"
        case duration = "Duration"
        case success = "Success"
        case errorMessage = "ErrorMessage"
        case friendResults = "FriendResults"
    }
}

public struct ExportedCookie: Codable {
    public let name: String
    public let value: String
    public let domain: String
    public let path: String
    public let expiresDate: Double?
}

enum SharedJsonCodec {
    private static let fractionalFormatter: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter
    }()

    private static let plainFormatter: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime]
        return formatter
    }()

    static func decodeDate(from string: String) -> Date? {
        fractionalFormatter.date(from: string) ?? plainFormatter.date(from: string)
    }

    static let encoder: JSONEncoder = {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        encoder.dateEncodingStrategy = .custom { date, encoder in
            var container = encoder.singleValueContainer()
            let string = fractionalFormatter.string(from: date)
            try container.encode(string)
        }
        return encoder
    }()

    static let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            if let string = try? container.decode(String.self), let date = decodeDate(from: string) {
                return date
            }
            if let seconds = try? container.decode(Double.self) {
                return Date(timeIntervalSinceReferenceDate: seconds)
            }
            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Unsupported date format")
        }
        return decoder
    }()
}

public final class SharedSettings {
    public static let shared = SharedSettings()

    private let defaults: UserDefaults

    private init() {
        AppGroupSupport.migrateFromStandardIfNeeded()
        defaults = AppGroupSupport.userDefaults
    }

    public var containerURL: URL {
        AppGroupSupport.storageDirectory
    }

    public var cookiesFileURL: URL {
        containerURL.appendingPathComponent(SharedConstants.cookiesFileName)
    }

    public var friendsListFileURL: URL {
        containerURL.appendingPathComponent(SharedConstants.friendsListFileName)
    }

    public func getString(_ key: String, default defaultValue: String = "") -> String {
        defaults.string(forKey: key) ?? defaultValue
    }

    public func setString(_ key: String, _ value: String) {
        defaults.set(value, forKey: key)
        defaults.synchronize()
    }

    public func getBool(_ key: String, default defaultValue: Bool = false) -> Bool {
        if defaults.object(forKey: key) == nil { return defaultValue }
        return defaults.bool(forKey: key)
    }

    public func setBool(_ key: String, _ value: Bool) {
        defaults.set(value, forKey: key)
        defaults.synchronize()
    }

    public func getFriends() -> [FriendConfig] {
        migrateFriendsToFileIfNeeded()
        let url = friendsListFileURL
        if let data = try? Data(contentsOf: url),
           let friends = try? SharedJsonCodec.decoder.decode([FriendConfig].self, from: data) {
            return friends
        }

        let json = getString(SharedConstants.friendsListKey)
        guard !json.isEmpty, let data = json.data(using: .utf8),
              let friends = try? SharedJsonCodec.decoder.decode([FriendConfig].self, from: data) else { return [] }
        saveFriendsToFile(json)
        return friends
    }

    public func saveFriends(_ friends: [FriendConfig]) {
        guard let data = try? SharedJsonCodec.encoder.encode(friends),
              let json = String(data: data, encoding: .utf8) else { return }
        saveFriendsToFile(json)
        setString(SharedConstants.friendsListKey, json)
    }

    private func saveFriendsToFile(_ json: String) {
        let url = friendsListFileURL
        let temp = url.appendingPathExtension("tmp")
        do {
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(),
                withIntermediateDirectories: true
            )
            try json.write(to: temp, atomically: true, encoding: .utf8)
            if FileManager.default.fileExists(atPath: url.path) {
                try FileManager.default.removeItem(at: url)
            }
            try FileManager.default.moveItem(at: temp, to: url)
        } catch {
            // Best-effort; UserDefaults mirror remains for MAUI reads during transition.
        }
    }

    private func migrateFriendsToFileIfNeeded() {
        let url = friendsListFileURL
        guard !FileManager.default.fileExists(atPath: url.path) else { return }
        let json = getString(SharedConstants.friendsListKey)
        guard !json.isEmpty else { return }
        saveFriendsToFile(json)
    }

    public func getMessageText() -> String {
        let msg = getString(SharedConstants.messageTextKey)
        return msg.isEmpty ? SharedConstants.defaultMessage : msg
    }

    public func getRandomizeMessages() -> Bool {
        getBool(SharedConstants.randomizeMessagesKey)
    }

    public func getSkipUnreachable() -> Bool {
        getBool(SharedConstants.skipUnreachableKey, default: true)
    }

    public func getLoginUserAgent() -> String {
        let ua = getString(SharedConstants.loginUserAgentKey)
        if !ua.isEmpty { return ua }
        return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
    }

    public func builtInMessages() -> [String] {
        [
            "Streak", "streak", "streakk", "streaak", "streaakkk", "streaaak", "strk", "Strk", "s", "S",
            "streaks", "Streaks", "streakss", "streak lol", "yo streak", "yoo streak", "yoo streakk",
            "hey streak", "hii streak", "hi streak", "heyy streak", "streak hii", "streak hi", "streak yo",
            "streak yoo", "streakkk", "strek", "streek", "streeek", "streeeek", "yo", "yoo", "yooo",
            "hey", "hii", "heyy", "heyyy", "here", "heree", "strkeee", "streak rn", "quick streak",
            "streakk lol", "streak lmao", "lol streak", "streaaakk", "streakkk lol", "daily streak",
            "streak streak", "ayo streak"
        ]
    }

    public func addRunResult(_ result: StreakRunResult) {
        var history = getRunHistory()
        history.insert(result, at: 0)
        if history.count > 50 { history = Array(history.prefix(50)) }
        guard let data = try? SharedJsonCodec.encoder.encode(history),
              let json = String(data: data, encoding: .utf8) else { return }
        setString(SharedConstants.runHistoryKey, json)
    }

    public func getRunHistory() -> [StreakRunResult] {
        let json = getString(SharedConstants.runHistoryKey)
        guard !json.isEmpty, let data = json.data(using: .utf8) else { return [] }
        return (try? SharedJsonCodec.decoder.decode([StreakRunResult].self, from: data)) ?? []
    }

    public func setLastRun(_ date: Date) {
        defaults.set(Double(date.timeIntervalSince1970 * 10_000_000 + 621_355_968_000_000_000), forKey: SharedConstants.lastRunKey)
        defaults.synchronize()
    }
}
