import Foundation

enum RunLogStore {
    private static let maxLines = 500
    private static let snippetLines = 10
    private static let lock = NSLock()

    static func clear() {
        lock.lock()
        defer { lock.unlock() }
        let url = logFileURL
        try? FileManager.default.removeItem(at: url)
    }

    static func append(_ message: String) {
        lock.lock()
        defer { lock.unlock() }

        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        let line = "[\(formatter.string(from: Date()))] \(message)\n"

        let url = logFileURL
        let directory = url.deletingLastPathComponent()
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)

        if FileManager.default.fileExists(atPath: url.path),
           let handle = try? FileHandle(forWritingTo: url) {
            handle.seekToEndOfFile()
            if let data = line.data(using: .utf8) {
                handle.write(data)
            }
            try? handle.close()
        } else {
            try? line.write(to: url, atomically: true, encoding: .utf8)
        }

        trimIfNeededUnlocked(at: url)
        mirrorSnippetToDefaultsUnlocked()
    }

    static func storagePath() -> String {
        logFileURL.deletingLastPathComponent().path
    }

    static func recentLines(count: Int) -> [String] {
        lock.lock()
        defer { lock.unlock() }
        return recentLinesUnlocked(count: count)
    }

    static func mirrorSnippetToDefaults() {
        lock.lock()
        defer { lock.unlock() }
        mirrorSnippetToDefaultsUnlocked()
    }

    private static var logFileURL: URL {
        SharedSettings.shared.containerURL.appendingPathComponent(SharedConstants.runLogsFileName)
    }

    private static func recentLinesUnlocked(count: Int) -> [String] {
        let url = logFileURL
        guard let text = try? String(contentsOf: url, encoding: .utf8) else { return [] }
        let lines = text.split(separator: "\n", omittingEmptySubsequences: true).map(String.init)
        guard lines.count > count else { return lines }
        return Array(lines.suffix(count))
    }

    private static func mirrorSnippetToDefaultsUnlocked() {
        let lines = recentLinesUnlocked(count: snippetLines)
        guard !lines.isEmpty else { return }
        AppGroupSupport.userDefaults.set(lines.joined(separator: "\n"), forKey: SharedConstants.lastRunLogSnippetKey)
        AppGroupSupport.userDefaults.synchronize()
    }

    private static func trimIfNeededUnlocked(at url: URL) {
        guard let text = try? String(contentsOf: url, encoding: .utf8) else { return }
        let lines = text.split(separator: "\n", omittingEmptySubsequences: false)
        guard lines.count > maxLines else { return }
        let trimmed = lines.suffix(maxLines).joined(separator: "\n") + "\n"
        try? trimmed.write(to: url, atomically: true, encoding: .utf8)
    }
}
