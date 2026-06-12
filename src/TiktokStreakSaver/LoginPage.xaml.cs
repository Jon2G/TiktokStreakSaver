using TiktokStreakSaver.Services;
using TiktokStreakSaver.Services.Storage;

namespace TiktokStreakSaver;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class LoginPage : ContentPage
{
    private readonly SessionService _sessionService;
    private readonly SettingsService _settingsService;
    private bool _isLoggedIn;
    private bool _webViewTornDown;
    private bool _completionInProgress;

    public LoginPage()
    {
        InitializeComponent();
        _sessionService = new SessionService();
        _settingsService = new SettingsService();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _webViewTornDown = false;
        _isLoggedIn = false;
        _completionInProgress = false;
        LoadTikTok();
        await TryCompleteExistingSessionAsync();
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

    private async Task TryCompleteExistingSessionAsync()
    {
        await Task.Delay(800);
        if (_isLoggedIn || _completionInProgress)
            return;

#if IOS
        Platforms.iOS.Services.IosWebViewConfigurator.AttachWebView(TikTokWebView);
        if (!await TikTokWebViewHelper.HasValidSessionCookieAsync(TikTokWebView))
            return;

        _isLoggedIn = true;
        await Platforms.iOS.Services.IosWebViewConfigurator.ExportCookiesFromCurrentWebViewAsync();
        await SessionRefreshHelper.RefreshAndGetRunReadyAsync(_sessionService);
        await Done(showSuccessAlert: false);
#elif ANDROID
        if (!TikTokWebViewHelper.HasValidSessionCookie())
            return;

        _isLoggedIn = true;
        await Done(showSuccessAlert: false);
#endif
    }

    private async void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (_isLoggedIn || _completionInProgress) return;
        LoadingOverlay.IsVisible = false;

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

    private async Task Done(bool showSuccessAlert = true)
    {
        if (_completionInProgress)
            return;
        _completionInProgress = true;

        TearDownLoginWebView();

        if (_isLoggedIn)
        {
            if (!_sessionService.TrySetSessionValid(true, out var persistError))
            {
                _completionInProgress = false;
                await DisplayAlert("Could Not Save Session",
                    persistError ?? "Login succeeded but session state did not persist on this device.", "OK");
                return;
            }

            AppStorageProvider.Current.SetBool(AppConstants.AuthRequiredKey, false);

            bool justEnabled = false;
            if (!_settingsService.IsScheduled())
            {
#if ANDROID
                var context = Platform.CurrentActivity ?? Android.App.Application.Context;
                TiktokStreakSaver.Platforms.Android.StreakScheduler.ScheduleNextRun(context);
#endif
                justEnabled = true;
            }

            SessionState.NotifyChanged();

            if (showSuccessAlert)
            {
                var body = justEnabled
#if IOS
                    ? "You're logged in to TikTok! Set up a daily Shortcut (see Profile) to run streaks automatically."
#else
                    ? "You're logged in to TikTok! Background automation has been enabled — your streaks will run on the schedule set in Profile."
#endif
                    : "You're logged in to TikTok! The app will use this session for streak messaging.";
                await DisplayAlert("Logged In", body, "OK");
            }

            await Navigation.PopAsync();
        }
        else
        {
            _sessionService.TrySetSessionValid(false, out _);
            await DisplayAlert("Not Logged In",
                "Please login to TikTok first before continuing.", "OK");
            _completionInProgress = false;
        }
    }
}
