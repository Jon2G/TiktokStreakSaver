import Foundation

private var shortcutRunHandler: (() -> Void)?
private let shortcutBridgeLock = NSLock()

@_cdecl("StreakSaver_RegisterShortcutRunHandler")
public func StreakSaver_RegisterShortcutRunHandler(_ handler: (@convention(c) () -> Void)?) {
    shortcutBridgeLock.lock()
    defer { shortcutBridgeLock.unlock() }
    if let handler {
        shortcutRunHandler = { handler() }
    } else {
        shortcutRunHandler = nil
    }
}

/// Called from the main-app App Shortcut intent. Returns true when MAUI handled the request.
@_cdecl("StreakSaver_RequestRunFromShortcut")
public func StreakSaver_RequestRunFromShortcut() -> Bool {
    shortcutBridgeLock.lock()
    let handler = shortcutRunHandler
    shortcutBridgeLock.unlock()

    if let handler {
        if Thread.isMainThread {
            handler()
        } else {
            DispatchQueue.main.async(execute: handler)
        }
        return true
    }

    PendingStreakRun.request()
    return false
}

public enum StreakShortcutRequest {
    public static func fromAppShortcut() -> Bool {
        StreakSaver_RequestRunFromShortcut()
    }
}
