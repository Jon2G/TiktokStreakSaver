using TiktokStreakSaver.Services;

namespace TiktokStreakSaver;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class LoginPage : ContentPage
{
    private readonly SessionService _sessionService;
    private readonly SettingsService _settingsService;
    private bool _isLoggedIn;
    private bool _webViewTornDown;

    public LoginPage()
    {
        InitializeComponent();
        _sessionService = new SessionService();
        _settingsService = new SettingsService();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _webViewTornDown = false;
        LoadTikTok();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        TearDownLoginWebView();
    }

    private void LoadTikTok()
    {
        LoadingOverlay.IsVisible = true;

        var desktopUa = AppConstants.DesktopChromeUserAgent;
#if ANDROID || IOS
        TikTokWebViewHelper.ConfigureWebView(TikTokWebView, desktopUa);
        _sessionService.SetLoginUserAgent(desktopUa);
#endif

        TikTokWebView.Source = TikTokWebViewHelper.LoginUrl;
    }

    private async void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (_isLoggedIn) return;
        LoadingOverlay.IsVisible = false;

        // Cookie check is authoritative. On iOS, read WKWebView cookies (export file is updated after login).
        bool hasSession;
#if IOS
        Platforms.iOS.Services.IosWebViewConfigurator.AttachWebView(TikTokWebView);
        hasSession = await TikTokWebViewHelper.HasValidSessionCookieAsync(TikTokWebView);
        if (!hasSession && TikTokWebViewHelper.CheckLoginStatus(e.Url).IsLoggedIn)
        {
            await Task.Delay(400);
            hasSession = await TikTokWebViewHelper.HasValidSessionCookieAsync(TikTokWebView);
        }
#else
        hasSession = TikTokWebViewHelper.HasValidSessionCookie();
#endif

        if (!hasSession)
            return;

        _isLoggedIn = true;
#if IOS
        await Platforms.iOS.Services.IosWebViewConfigurator.ExportCookiesFromCurrentWebViewAsync();
#endif
        await Done();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (TikTokWebView.CanGoBack)
            TikTokWebView.GoBack();
        else
            await Navigation.PopAsync();
    }

    private void OnRefreshClicked(object? sender, EventArgs e)
    {
        LoadingOverlay.IsVisible = true;
        TikTokWebView.Reload();
    }

    private void TearDownLoginWebView()
    {
        if (_webViewTornDown)
            return;
        _webViewTornDown = true;
        TikTokWebViewHelper.TearDownLoginWebView(TikTokWebView);
    }

    private async Task Done()
    {
        // Tear down WebView before session update so we don't touch WKWebView during cookie file I/O.
        TearDownLoginWebView();
        TikTokWebViewHelper.UpdateSessionStatus(_sessionService, _isLoggedIn);

        if (_isLoggedIn)
        {
            // Auto-enable background automation on the first login (idempotent: a no-op if the
            // user has already enabled it, so re-logging in won't disturb an existing schedule).
            bool justEnabled = false;
            if (!_settingsService.IsScheduled())
            {
#if ANDROID
                var context = Platform.CurrentActivity ?? Android.App.Application.Context;
                TiktokStreakSaver.Platforms.Android.StreakScheduler.ScheduleNextRun(context);
#else
                _settingsService.SetScheduled(true);
#endif
                justEnabled = true;
            }

            var body = justEnabled
#if IOS
                ? "You're logged in to TikTok! Set up a daily Shortcut (see Profile) to run streaks automatically."
#else
                ? "You're logged in to TikTok! Background automation has been enabled — your streaks will run on the schedule set in Profile."
#endif
                : "You're logged in to TikTok! The app will use this session for streak messaging.";
            await DisplayAlert("Logged In", body, "OK");
            await Navigation.PopAsync();
        }
        else
        {
            await DisplayAlert("Not Logged In",
                "Please login to TikTok first before continuing.", "OK");
        }
    }
}
