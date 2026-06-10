using WebKit;

namespace TiktokStreakSaver.Platforms.iOS.Services;

public static class IosWebViewConfigurator
{
    private static WKWebView? _lastWebView;

    public static void Configure(WebView mauiWebView, string? customUserAgent)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (mauiWebView.Handler?.PlatformView is not WKWebView wk)
                return;

            _lastWebView = wk;
            var config = wk.Configuration;
            config.AllowsInlineMediaPlayback = true;
            config.Preferences.JavaScriptEnabled = true;
            if (!string.IsNullOrEmpty(customUserAgent))
                config.ApplicationNameForUserAgent = customUserAgent;
        });
    }

    public static bool HasValidSessionCookie() => HasExportedSessionCookie();

    /// <summary>Checks WKWebView cookie store (use on LoginPage right after TikTok sign-in).</summary>
    public static async Task<bool> HasValidSessionCookieInWebViewAsync(WebView? mauiWebView = null)
    {
        WKWebView? wk = null;
        if (mauiWebView?.Handler?.PlatformView is WKWebView attached)
            wk = attached;
        else
            wk = _lastWebView;

        if (wk != null && await CookieStoreHasSessionIdAsync(wk.Configuration.WebsiteDataStore.HttpCookieStore))
            return true;

        return HasExportedSessionCookie();
    }

    private static bool HasExportedSessionCookie()
    {
        try
        {
            var path = AppGroupPaths.CookiesFilePath;
            if (!File.Exists(path))
                return false;

            var json = File.ReadAllText(path);
            return json.Contains("\"sessionid\"", StringComparison.Ordinal) ||
                   json.Contains("sessionid", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CookieStoreHasSessionIdAsync(WKHttpCookieStore store)
    {
        var cookies = await store.GetAllCookiesAsync();
        return cookies.Any(c =>
            string.Equals(c.Name, "sessionid", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(c.Value));
    }

    public static void ClearCookies()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await CookieSyncService.ClearWebViewCookiesAsync(_lastWebView);
        });
    }

    public static async Task ExportCookiesFromCurrentWebViewAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await CookieSyncService.ExportCookiesAsync(_lastWebView);
        });
    }

    public static void AttachWebView(WebView mauiWebView)
    {
        if (mauiWebView.Handler?.PlatformView is WKWebView wk)
            _lastWebView = wk;
    }

    /// <summary>Stops TikTok playback when leaving LoginPage (WKWebView keeps playing audio if not torn down).</summary>
    public static void TearDownLoginWebView(WebView mauiWebView)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (mauiWebView.Handler?.PlatformView is WKWebView wk)
            {
                if (OperatingSystem.IsIOSVersionAtLeast(15))
                    wk.SetAllMediaPlaybackSuspended(true, () => { });
                wk.StopLoading();
                wk.NavigationDelegate = null;
                wk.LoadHtmlString("<!DOCTYPE html><html><head></head><body></body></html>", null);
                if (ReferenceEquals(_lastWebView, wk))
                    _lastWebView = null;
            }

            mauiWebView.Source = null;
            mauiWebView.Handler?.DisconnectHandler();
        });
    }
}
