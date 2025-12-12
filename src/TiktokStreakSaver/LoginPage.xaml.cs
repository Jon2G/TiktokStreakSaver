using AsyncAwaitBestPractices;
using RandomUserAgent;
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
        // Configure WebView using helper with random user agent
        TikTokWebViewHelper.ConfigureWebView(TikTokWebView, RandomUa.RandomUserAgent);
#endif

        TikTokWebView.Source = TikTokWebViewHelper.LoginUrl;
    }

    private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (_isLoggedIn)
        {
            return;
        }
        LoadingOverlay.IsVisible = false;

        // Use helper to check login status
        var result = TikTokWebViewHelper.CheckLoginStatus(e.Url);
        
        if (!result.IsValidUrl)
        {
            LoadTikTok();
            return;
        }

        if (result.IsLoggedIn)
        {
            _isLoggedIn = true;
            Done().SafeFireAndForget();
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (TikTokWebView.CanGoBack)
        {
            TikTokWebView.GoBack();
        }
        else
        {
            await Navigation.PopAsync();
        }
    }

    private void OnRefreshClicked(object? sender, EventArgs e)
    {
        LoadingOverlay.IsVisible = true;
        TikTokWebView.Reload();
    }

    private async Task Done()
    {
        // Update session status using helper
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



