using TiktokStreakSaver.Services;

namespace TiktokStreakSaver.Services;

/// <summary>
/// Helper class for TikTok WebView configuration and login detection
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public static class TikTokWebViewHelper
{
    public const string LoginUrl = "https://www.tiktok.com/login";
    public const string MessagesUrl = "https://www.tiktok.com/messages";

    /// <summary>
    /// Result of login status check
    /// </summary>
    public class LoginStatusResult
    {
        public bool IsLoggedIn { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool IsValidUrl { get; set; }
    }

    /// <summary>
    /// Configure a WebView for TikTok with proper settings
    /// </summary>
    public static void ConfigureWebView(WebView webView, string? customUserAgent = null)
    {
#if ANDROID
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var androidWebView = webView.Handler?.PlatformView as Android.Webkit.WebView;
            if (androidWebView != null)
            {
                ConfigureAndroidWebView(androidWebView, customUserAgent);
            }
        });
#endif
    }

#if ANDROID
    /// <summary>
    /// Configure an Android WebView directly
    /// </summary>
    public static void ConfigureAndroidWebView(Android.Webkit.WebView webView, string? customUserAgent = null)
    {
        webView.Settings.JavaScriptEnabled = true;
        webView.Settings.DomStorageEnabled = true;
        webView.Settings.DatabaseEnabled = true;
        webView.Settings.CacheMode = Android.Webkit.CacheModes.Normal;
        
        // Set user agent
        if (!string.IsNullOrEmpty(customUserAgent))
        {
            webView.Settings.UserAgentString = customUserAgent;
        }
        else
        {
            webView.Settings.UserAgentString = GetDefaultUserAgent();
        }
        
        // Enable cookies (critical for TikTok session)
        var cookieManager = Android.Webkit.CookieManager.Instance;
        cookieManager?.SetAcceptCookie(true);
        cookieManager?.SetAcceptThirdPartyCookies(webView, true);
    }

    /// <summary>
    /// Flush cookies to ensure they are persisted
    /// </summary>
    public static void FlushCookies()
    {
        Android.Webkit.CookieManager.Instance?.Flush();
    }
#endif

    /// <summary>
    /// Get default user agent for TikTok
    /// </summary>
    public static string GetDefaultUserAgent()
    {
        return "Mozilla/5.0 (Linux; Android 10; Mobile) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";
    }

    /// <summary>
    /// Check login status from a navigated URL
    /// </summary>
    public static LoginStatusResult CheckLoginStatus(string? url)
    {
        var result = new LoginStatusResult
        {
            Url = url ?? string.Empty
        };

        if (string.IsNullOrEmpty(url))
        {
            result.IsValidUrl = false;
            result.IsLoggedIn = false;
            return result;
        }

        var urlLower = url.ToLower();

        // Check if it's a valid HTTP URL
        if (!urlLower.StartsWith("http"))
        {
            result.IsValidUrl = false;
            result.IsLoggedIn = false;
            return result;
        }

        result.IsValidUrl = true;

        // Check if we're on login page (not logged in)
        if (urlLower.Contains("/login"))
        {
            result.IsLoggedIn = false;
            return result;
        }

        // Check if we're on an authenticated page (logged in)
        if (urlLower.Contains("tiktok.com/messages") ||
            urlLower.Contains("tiktok.com/foryou") ||
            urlLower.Contains("tiktok.com/@"))
        {
            result.IsLoggedIn = true;
            return result;
        }

        // Unknown page state - assume not logged in
        result.IsLoggedIn = false;
        return result;
    }

    /// <summary>
    /// Update session service with login status and optionally flush cookies
    /// </summary>
    public static void UpdateSessionStatus(SessionService sessionService, bool isLoggedIn)
    {
        sessionService.SetSessionValid(isLoggedIn);
        
#if ANDROID
        if (isLoggedIn)
        {
            FlushCookies();
        }
#endif
    }
}






