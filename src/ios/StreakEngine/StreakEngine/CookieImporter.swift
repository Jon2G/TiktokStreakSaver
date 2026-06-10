import Foundation
import WebKit

public enum CookieImporter {
    public static func loadExportedCookies() -> [ExportedCookie] {
        let url = SharedSettings.shared.cookiesFileURL
        guard FileManager.default.fileExists(atPath: url.path),
              let data = try? Data(contentsOf: url),
              let cookies = try? JSONDecoder().decode([ExportedCookie].self, from: data) else {
            return []
        }
        return cookies
    }

    public static func apply(cookies: [ExportedCookie], to store: WKHTTPCookieStore, completion: @escaping () -> Void) {
        let group = DispatchGroup()
        for item in cookies {
            var properties: [HTTPCookiePropertyKey: Any] = [
                .name: item.name,
                .value: item.value,
                .domain: item.domain,
                .path: item.path
            ]
            if let exp = item.expiresDate {
                properties[.expires] = Date(timeIntervalSince1970: exp)
            }
            guard let cookie = HTTPCookie(properties: properties) else { continue }
            group.enter()
            store.setCookie(cookie) { group.leave() }
        }
        group.notify(queue: .main, execute: completion)
    }
}
