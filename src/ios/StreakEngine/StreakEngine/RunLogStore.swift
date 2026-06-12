import Foundation

enum RunLogStore {
    private static let maxLines = 500
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

        trimIfNeeded(at: url)
    }

    private static var logFileURL: URL {
        SharedSettings.shared.containerURL.appendingPathComponent(SharedConstants.runLogsFileName)
    }

    private static func trimIfNeeded(at url: URL) {
        guard let text = try? String(contentsOf: url, encoding: .utf8) else { return }
        let lines = text.split(separator: "\n", omittingEmptySubsequences: false)
        guard lines.count > maxLines else { return }
        let trimmed = lines.suffix(maxLines).joined(separator: "\n") + "\n"
        try? trimmed.write(to: url, atomically: true, encoding: .utf8)
    }
}
