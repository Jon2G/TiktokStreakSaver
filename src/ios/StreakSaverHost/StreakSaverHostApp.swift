import UIKit

/// Minimal host app so Xcode creates the main-app provisioning profile
/// (com.jon2g.tiktokstreaksaver) used by the MAUI publish step.
@main
final class AppDelegate: UIResponder, UIApplicationDelegate {
    func application(
        _ application: UIApplication,
        didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?
    ) -> Bool {
        true
    }
}
