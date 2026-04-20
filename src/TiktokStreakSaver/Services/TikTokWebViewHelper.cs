namespace TiktokStreakSaver.Services;

/// <summary>
/// TikTok WebView configuration and login detection.
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public static class TikTokWebViewHelper
{
    public const string LoginUrl = "https://www.tiktok.com/login";
    public const string MessagesUrl = "https://www.tiktok.com/messages";

    public class LoginStatusResult
    {
        public bool IsLoggedIn { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool IsValidUrl { get; set; }
    }

    public static void ConfigureWebView(WebView webView, string? customUserAgent = null)
    {
#if ANDROID
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var androidWebView = webView.Handler?.PlatformView as Android.Webkit.WebView;
            if (androidWebView != null)
                ConfigureAndroidWebView(androidWebView, customUserAgent);
        });
#endif
    }

#if ANDROID
    public static void ConfigureAndroidWebView(Android.Webkit.WebView webView, string? customUserAgent = null)
    {
        webView.Settings.JavaScriptEnabled = true;
        webView.Settings.DomStorageEnabled = true;
        webView.Settings.DatabaseEnabled = true;
        webView.Settings.CacheMode = Android.Webkit.CacheModes.Normal;
        webView.Settings.UserAgentString = string.IsNullOrEmpty(customUserAgent)
            ? GetDefaultUserAgent()
            : customUserAgent;

        var cookieManager = Android.Webkit.CookieManager.Instance;
        cookieManager?.SetAcceptCookie(true);
        cookieManager?.SetAcceptThirdPartyCookies(webView, true);
    }

    public static void FlushCookies() => Android.Webkit.CookieManager.Instance?.Flush();
#endif

    public static string GetDefaultUserAgent() =>
        "Mozilla/5.0 (Linux; Android 10; Mobile) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    public static LoginStatusResult CheckLoginStatus(string? url)
    {
        var result = new LoginStatusResult { Url = url ?? string.Empty };

        if (string.IsNullOrEmpty(url))
        {
            result.IsValidUrl = false;
            result.IsLoggedIn = false;
            return result;
        }

        var urlLower = url.ToLower();
        if (!urlLower.StartsWith("http"))
        {
            result.IsValidUrl = false;
            result.IsLoggedIn = false;
            return result;
        }

        result.IsValidUrl = true;
        if (urlLower.Contains("/login"))
        {
            result.IsLoggedIn = false;
            return result;
        }

        if (urlLower.Contains("tiktok.com/messages") ||
            urlLower.Contains("tiktok.com/foryou") ||
            urlLower.Contains("tiktok.com/@"))
        {
            result.IsLoggedIn = true;
            return result;
        }

        result.IsLoggedIn = false;
        return result;
    }

    public static void UpdateSessionStatus(SessionService sessionService, bool isLoggedIn)
    {
        sessionService.SetSessionValid(isLoggedIn);
#if ANDROID
        if (isLoggedIn)
            FlushCookies();
#endif
    }
}
