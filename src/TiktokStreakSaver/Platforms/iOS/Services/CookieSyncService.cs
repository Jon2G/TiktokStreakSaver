using System.Text.Json;
using Foundation;
using TiktokStreakSaver.Services;
using WebKit;

namespace TiktokStreakSaver.Platforms.iOS.Services;

public record ExportedCookie(string Name, string Value, string Domain, string Path, double? ExpiresDate);

public static class CookieSyncService
{
    internal static bool IsTikTokRelatedDomain(string? domain)
    {
        if (string.IsNullOrEmpty(domain))
            return false;

        return domain.Contains("tiktok", StringComparison.OrdinalIgnoreCase)
            || domain.Contains("byteoversea", StringComparison.OrdinalIgnoreCase)
            || domain.Contains("musical.ly", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task ExportCookiesAsync(WKWebView? webView = null)
    {
        var store = webView?.Configuration?.WebsiteDataStore?.HttpCookieStore
                    ?? WKWebsiteDataStore.DefaultDataStore.HttpCookieStore;

        var allCookies = await store.GetAllCookiesAsync();
        var list = allCookies
            .Where(c => IsTikTokRelatedDomain(c.Domain))
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

        var json = JsonSerializer.Serialize(distinct, AppJsonSerialization.Cookies);
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

    /// <summary>
    /// Probes WK store and existing cookies.json without clobbering a valid on-disk export
    /// when the default store is empty on cold start.
    /// </summary>
    public static async Task<bool> RefreshSessionCookiesPreservingExportAsync()
    {
        bool hadSessionInFile = HasSessionIdInExport();

        bool readyFromStore = await MainThread.InvokeOnMainThreadAsync(
            IosWebViewConfigurator.HasValidSessionCookieInDefaultStoreAsync);

        if (readyFromStore)
        {
            await MainThread.InvokeOnMainThreadAsync(async () => await ExportCookiesAsync());
        }
        else if (!hadSessionInFile)
        {
            await MainThread.InvokeOnMainThreadAsync(async () => await ExportCookiesAsync());
        }

        return readyFromStore || HasSessionIdInExport();
    }

    /// <summary>True when cookies.json exists and contains a TikTok sessionid.</summary>
    public static bool HasSessionIdInExport()
    {
        try
        {
            var path = AppGroupPaths.CookiesFilePath;
            if (!File.Exists(path))
                return false;

            var json = File.ReadAllText(path);
            return json.Contains("\"sessionid\"", StringComparison.OrdinalIgnoreCase)
                || json.Contains("\"name\":\"sessionid\"", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static async Task ClearWebViewCookiesAsync(WKWebView? webView = null)
    {
        var store = webView?.Configuration?.WebsiteDataStore?.HttpCookieStore
                    ?? WKWebsiteDataStore.DefaultDataStore.HttpCookieStore;

        var allCookies = await store.GetAllCookiesAsync();
        foreach (var c in allCookies.Where(c => IsTikTokRelatedDomain(c.Domain)))
            store.DeleteCookie(c, null);

        ClearExportedCookies();
    }
}
