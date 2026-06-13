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
        SecondaryButton.IsVisible = step > 0 && step < 4;
        SecondaryButton.Text = step == 1 ? "Read full details" : "Skip for now";

        switch (step)
        {
            case 0:
                StepTitleLabel.Text = "Welcome to Streak Saver (iOS)";
                StepBodyLabel.Text =
                    "This iOS build is experimental and not equivalent to Android. " +
                    "Streaks run through Shortcuts or the widget — not silent background alarms. " +
                    "The app must open briefly while TikTok runs in an invisible browser, and your iPhone must be unlocked when the automation fires.";
                PrimaryButton.Text = "Continue";
                break;
            case 1:
                StepTitleLabel.Text = "Know the limits";
                StepBodyLabel.Text =
                    "Shortcuts cannot launch Streak Saver while your phone is locked — you may see Unable to launch because the device couldn't be unlocked. " +
                    "iOS also suspends WebView automation in the background. For reliable daily streaks, use the Android app. " +
                    "Tap Read full details for Apple's official documentation links.";
                PrimaryButton.Text = "I understand — continue";
                break;
            case 2:
                StepTitleLabel.Text = "Set up Shortcuts";
                StepBodyLabel.Text =
                    "Use the step-by-step guide with tabs for each screen on your phone. " +
                    "Schedule the automation for a time you are usually awake and unlocked.";
                PrimaryButton.Text = "Open setup guide";
                break;
            case 3:
                StepTitleLabel.Text = "AltStore & certificate";
                StepBodyLabel.Text =
                    "This app is installed via AltStore. Free certificates expire every 7 days. Keep AltStore refreshing in the background " +
                    "so the app and Shortcuts keep working.";
                PrimaryButton.Text = "I understand";
                break;
            case 4:
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
        if (_step == 2)
        {
            var tutorial = new IosShortcutTutorialPage();
            tutorial.Completed += (_, _) => ShowStep(3);
            await Navigation.PushAsync(tutorial);
            return;
        }

        if (_step == 4)
        {
#if IOS
            await Platforms.iOS.Services.IosNotificationService.RequestPermissionAsync();
#endif
            ShowStep(5);
            return;
        }

        ShowStep(_step + 1);
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        if (_step == 1)
        {
            await Navigation.PushAsync(new IosLimitationsPage());
            return;
        }

        ShowStep(_step + 1);
    }

    private void CompleteOnboarding()
    {
        AppStorageProvider.Current.SetBool(AppConstants.IosOnboardingCompleteKey, true);
        if (Application.Current?.Windows.FirstOrDefault() is { } window)
            window.Page = new AppShell();
    }
}
