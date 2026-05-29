using TiktokStreakSaver.Services.Storage;

namespace TiktokStreakSaver.Pages;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class IosOnboardingPage : ContentPage
{
    private int _step;

    public IosOnboardingPage()
    {
        InitializeComponent();
        ShowStep(0);
    }

    private void ShowStep(int step)
    {
        _step = step;
        SecondaryButton.IsVisible = step > 0 && step < 3;

        switch (step)
        {
            case 0:
                StepTitleLabel.Text = "Welcome to Streak Saver (iOS)";
                StepBodyLabel.Text =
                    "On iOS, streaks run through the Shortcuts app and a home screen widget — not Android-style background alarms. " +
                    "You'll sign in once in the app, then schedule a daily Shortcut.";
                PrimaryButton.Text = "Continue";
                break;
            case 1:
                StepTitleLabel.Text = "Set up Shortcuts";
                StepBodyLabel.Text =
                    "Use the step-by-step guide with tabs for each screen on your phone. " +
                    "You can add your own GIFs or screen recordings to match what users will see.";
                PrimaryButton.Text = "Open setup guide";
                break;
            case 2:
                StepTitleLabel.Text = "AltStore & certificate";
                StepBodyLabel.Text =
                    "This app is installed via AltStore. Free certificates expire every 7 days. Keep AltStore refreshing in the background " +
                    "so the app and Shortcuts keep working.";
                PrimaryButton.Text = "I understand";
                break;
            case 3:
                StepTitleLabel.Text = "Notifications";
                StepBodyLabel.Text =
                    "Allow notifications so you know when a streak run finishes or when you need to log in again.";
                PrimaryButton.Text = "Enable notifications";
                break;
            default:
                CompleteOnboarding();
                break;
        }
    }

    private async void OnPrimaryClicked(object? sender, EventArgs e)
    {
        if (_step == 1)
        {
            var tutorial = new IosShortcutTutorialPage();
            tutorial.Completed += (_, _) => ShowStep(2);
            await Navigation.PushAsync(tutorial);
            return;
        }

        if (_step == 3)
        {
#if IOS
            await Platforms.iOS.Services.IosNotificationService.RequestPermissionAsync();
#endif
            ShowStep(4);
            return;
        }

        ShowStep(_step + 1);
    }

    private void OnSkipClicked(object? sender, EventArgs e) => ShowStep(_step + 1);

    private void CompleteOnboarding()
    {
        AppStorageProvider.Current.SetBool(AppConstants.IosOnboardingCompleteKey, true);
        if (Application.Current?.Windows.FirstOrDefault() is { } window)
            window.Page = new AppShell();
    }
}
