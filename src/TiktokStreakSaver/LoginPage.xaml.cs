using AsyncAwaitBestPractices;
using TiktokStreakSaver.Services;

namespace TiktokStreakSaver;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class LoginPage : ContentPage
{
    private readonly SessionService _sessionService;
    private bool _isLoggedIn = false;

    public LoginPage()
    {
        InitializeComponent();
        _sessionService = new SessionService();
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
            await DisplayAlert("Logged In",
                "You're logged in to TikTok! The app will use this session for background messaging.", "OK");
            await Navigation.PopAsync();
        }
        else
        {
            await DisplayAlert("Not Logged In",
                "Please login to TikTok first before continuing.", "OK");
        }
    }
}
