namespace TiktokStreakSaver.Pages;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class PrivacyPage : ContentPage
{
    private const string PolicyUrl = "https://github.com/Jon2G/TiktokStreakSaver/blob/master/PRIVACY.md";
    private const string IssuesUrl = "https://github.com/Jon2G/TiktokStreakSaver/issues";

    public PrivacyPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        this.Opacity = 0;
        this.TranslationY = 12;
        await Task.WhenAll(
            this.FadeTo(1, 280, Easing.SinInOut),
            this.TranslateTo(0, 0, 280, Easing.SinInOut));
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnViewOnGitHubClicked(object? sender, EventArgs e)
    {
        try
        {
            await Browser.OpenAsync(PolicyUrl, BrowserLaunchMode.External);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Open Link", $"Could not open the link: {ex.Message}", "OK");
        }
    }

    private async void OnContactGitHubClicked(object? sender, EventArgs e)
    {
        try
        {
            await Browser.OpenAsync(IssuesUrl, BrowserLaunchMode.External);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Open Link", $"Could not open the link: {ex.Message}", "OK");
        }
    }
}
