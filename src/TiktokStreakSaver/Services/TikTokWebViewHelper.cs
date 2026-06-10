namespace TiktokStreakSaver.Services;

/// <summary>
/// TikTok WebView configuration and login detection.
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public static class TikTokWebViewHelper
{
    public const string LoginUrl = "https://www.tiktok.com/login";
    public const string MessagesUrl = "https://www.tiktok.com/messages";

    [Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
    public class LoginStatusResult
    {
        public bool IsLoggedIn { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool IsValidUrl { get; set; }
    }

    /// <summary>Stop loading and release the login WebView so TikTok video/audio does not continue in the background.</summary>
    public static void TearDownLoginWebView(WebView webView)
    {
#if ANDROID
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (webView.Handler?.PlatformView is Android.Webkit.WebView awv)
            {
                awv.StopLoading();
                awv.OnPause();
                awv.PauseTimers();
                awv.LoadUrl("about:blank");
            }
            webView.Source = null;
            webView.Handler?.DisconnectHandler();
        });
#elif IOS
        Platforms.iOS.Services.IosWebViewConfigurator.TearDownLoginWebView(webView);
#else
        webView.Source = null;
#endif
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
#elif IOS
        Platforms.iOS.Services.IosWebViewConfigurator.Configure(webView, customUserAgent);
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

    public static bool HasValidSessionCookie()
    {
        try
        {
            var cookieManager = Android.Webkit.CookieManager.Instance;
            if (cookieManager == null) return false;

            string cookies1 = cookieManager.GetCookie("https://www.tiktok.com") ?? string.Empty;
            string cookies2 = cookieManager.GetCookie("https://tiktok.com") ?? string.Empty;

            return cookies1.Contains("sessionid=") || cookies2.Contains("sessionid=");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking session cookie: {ex.Message}");
            return false;
        }
    }

    public static void ClearAllCookies()
    {
        try
        {
            var cookieManager = Android.Webkit.CookieManager.Instance;
            if (cookieManager != null)
            {
                cookieManager.RemoveAllCookies(null);
                cookieManager.Flush();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing cookies: {ex.Message}");
        }
    }
#elif IOS
    public static bool HasValidSessionCookie() =>
        Platforms.iOS.Services.IosWebViewConfigurator.HasValidSessionCookie();

    public static Task<bool> HasValidSessionCookieAsync(WebView? webView = null) =>
        Platforms.iOS.Services.IosWebViewConfigurator.HasValidSessionCookieInWebViewAsync(webView);

    public static void ClearAllCookies()
    {
        Platforms.iOS.Services.IosWebViewConfigurator.ClearCookies();
    }
#else
    public static bool HasValidSessionCookie() => false;
    public static void ClearAllCookies() { }
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
#elif IOS
        if (isLoggedIn)
            Storage.AppStorageProvider.Current.SetBool(AppConstants.AuthRequiredKey, false);
        // Cookie export runs on LoginPage before Done(); avoid re-export after WebView teardown.
#endif
    }
}
