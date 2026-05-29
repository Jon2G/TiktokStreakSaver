import Foundation
import UserNotifications

public enum StreakNotifications {
    public static func requestAuthorizationIfNeeded() {
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound, .badge]) { _, _ in }
    }

    public static func notify(title: String, body: String, isAlert: Bool) {
        let content = UNMutableNotificationContent()
        content.title = title
        content.body = body
        if isAlert {
            content.sound = .default
        }
        let request = UNNotificationRequest(
            identifier: UUID().uuidString,
            content: content,
            trigger: nil)
        UNUserNotificationCenter.current().add(request)
    }
}
