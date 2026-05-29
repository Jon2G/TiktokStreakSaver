import Foundation

@_cdecl("StreakEngine_ConsumePendingRun")
public func StreakEngine_ConsumePendingRun() -> Bool {
    PendingStreakRun.consume()
}

@_cdecl("StreakEngine_Run")
public func StreakEngine_Run(_ callback: @escaping @convention(c) (Bool) -> Void) {
    DispatchQueue.main.async {
        StreakWebViewRunner.shared.run { success in
            callback(success)
        }
    }
}

@objc public final class StreakEngineRunner: NSObject {
    @objc public static func run(completion: @escaping (Bool) -> Void) {
        StreakWebViewRunner.shared.run(completion: completion)
    }
}
