using AsyncAwaitBestPractices;
using TiktokStreakSaver.Services;

namespace TiktokStreakSaver;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class LoginPage : ContentPage
{
    private readonly SessionService _sessionService;
    private readonly SettingsService _settingsService;
    private bool _isLoggedIn = false;

    public LoginPage()
    {
        InitializeComponent();
        _sessionService = new SessionService();
        _settingsService = new SettingsService();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadTikTok();
    }

    private void LoadTikTok()
    {
        LoadingOverlay.IsVisible = true;

#if ANDROID
        // Use a modern Chrome desktop UA. Older randomized UAs (e.g. Firefox 3.6)
        // cause TikTok to serve degraded markup that breaks the chat header.
        var desktopUa = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
        TikTokWebViewHelper.ConfigureWebView(TikTokWebView, desktopUa);
        _sessionService.SetLoginUserAgent(desktopUa);
#endif

        TikTokWebView.Source = TikTokWebViewHelper.LoginUrl;
    }

    private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (_isLoggedIn) return;
        LoadingOverlay.IsVisible = false;

        // Cookie-based check is authoritative; URL heuristics are unreliable on TikTok.
        bool hasSession = TikTokWebViewHelper.HasValidSessionCookie();

        if (hasSession)
        {
            _isLoggedIn = true;
            Done().SafeFireAndForget();
        }
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

    private async Task Done()
    {
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
                ? "You're logged in to TikTok! Background automation has been enabled — your streaks will run on the schedule set in Profile."
                : "You're logged in to TikTok! The app will use this session for background messaging.";
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
