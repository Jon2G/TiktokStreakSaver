import Foundation

@_cdecl("StreakEngine_ConsumePendingRun")
public func StreakEngine_ConsumePendingRun() -> Bool {
    PendingStreakRun.consume()
}

@_cdecl("StreakEngine_Run")
public func StreakEngine_Run(_ callback: @escaping @convention(c) (Bool) -> Void) {
    let start = {
        StreakWebViewRunner.shared.run { success in
            callback(success)
        }
    }

    if Thread.isMainThread {
        start()
    } else {
        DispatchQueue.main.async(execute: start)
    }
}

@objc public final class StreakEngineRunner: NSObject {
    @objc public static func run(completion: @escaping (Bool) -> Void) {
        StreakWebViewRunner.shared.run(completion: completion)
    }
}
