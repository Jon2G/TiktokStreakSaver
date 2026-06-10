using System.Text.Json;
using Foundation;
using WebKit;

namespace TiktokStreakSaver.Platforms.iOS.Services;

public record ExportedCookie(string Name, string Value, string Domain, string Path, double? ExpiresDate);

public static class CookieSyncService
{
    public static async Task ExportCookiesAsync(WKWebView? webView = null)
    {
        var store = webView?.Configuration?.WebsiteDataStore?.HttpCookieStore
                    ?? WKWebsiteDataStore.DefaultDataStore.HttpCookieStore;

        var allCookies = await store.GetAllCookiesAsync();
        var list = allCookies
            .Where(c => (c.Domain ?? "").Contains("tiktok", StringComparison.OrdinalIgnoreCase))
            .Select(c => new ExportedCookie(
                c.Name ?? string.Empty,
                c.Value ?? string.Empty,
                c.Domain ?? string.Empty,
                c.Path ?? "/",
                c.ExpiresDate?.SecondsSince1970))
            .ToList();

        var distinct = list
            .GroupBy(c => $"{c.Domain}|{c.Name}|{c.Path}")
            .Select(g => g.First())
            .ToList();

        var json = JsonSerializer.Serialize(distinct);
        var path = AppGroupPaths.CookiesFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, json);
    }

    public static void ClearExportedCookies()
    {
        var path = AppGroupPaths.CookiesFilePath;
        if (File.Exists(path))
            File.Delete(path);
    }

    public static async Task ClearWebViewCookiesAsync(WKWebView? webView = null)
    {
        var store = webView?.Configuration?.WebsiteDataStore?.HttpCookieStore
                    ?? WKWebsiteDataStore.DefaultDataStore.HttpCookieStore;

        var allCookies = await store.GetAllCookiesAsync();
        foreach (var c in allCookies.Where(c => (c.Domain ?? "").Contains("tiktok", StringComparison.OrdinalIgnoreCase)))
            store.DeleteCookie(c, null);

        ClearExportedCookies();
    }
}
