using Microsoft.Maui.Controls.Shapes;
using TiktokStreakSaver.Services;

namespace TiktokStreakSaver.Pages;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class ProfilePage : ContentPage
{
    private readonly SessionService _sessionService;
    private readonly SettingsService _settingsService;
    private readonly UpdateService _updateService;
    private bool _suppressIntervalChanged = false;
    private bool _isCheckingUpdates = false;

    public ProfilePage()
    {
        InitializeComponent();
        _sessionService = new SessionService();
        _settingsService = new SettingsService();
        _updateService = new UpdateService();
    }

    private Color GetThemeColor(string key, string fallbackHex = "#92979E")
    {
        if (Application.Current != null && Application.Current.Resources.TryGetValue(key, out var resource) && resource is Color color)
            return color;
        return Color.FromArgb(fallbackHex);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        this.Opacity = 0;
        this.TranslationY = 12;
        await Task.WhenAll(
            this.FadeTo(1, 280, Easing.SinInOut),
            this.TranslateTo(0, 0, 280, Easing.SinInOut));

        LoadProfilePhoto();

        DisplayNameEntry.Text = _sessionService.GetDisplayName();

        ScheduleSwitch.IsToggled = _settingsService.IsScheduled();
        SkipUnreachableSwitch.IsToggled = _settingsService.GetSkipUnreachableUsers();
        RandomizeMessagesSwitch.IsToggled = _settingsService.GetRandomizeNormalMessages();
        SendOnBatteryLowSwitch.IsToggled = _settingsService.GetSendOnBatteryLow();

        FixedTimeSwitch.IsToggled = _settingsService.GetUseFixedTime();
        ScheduleTimePicker.Time = new TimeSpan(
            _settingsService.GetFixedTimeHour(),
            _settingsService.GetFixedTimeMinute(), 0);
        ScheduleOptionsPanel.IsVisible = ScheduleSwitch.IsToggled;
        TimePickerRow.IsVisible = FixedTimeSwitch.IsToggled;
        IntervalPanel.IsVisible = !FixedTimeSwitch.IsToggled;

        _suppressIntervalChanged = true;
        try
        {
            var hours = _settingsService.GetIntervalHours();
            IntervalHoursStepper.Value = hours;
            UpdateIntervalLabels();
        }
        finally
        {
            _suppressIntervalChanged = false;
        }

        VersionLabel.Text = $"v{AppInfo.Current.VersionString}";

#if IOS
        IosShortcutsSettingsPanel.IsVisible = true;
        AndroidSchedulingPanel.IsVisible = false;
#else
        IosShortcutsSettingsPanel.IsVisible = false;
        AndroidSchedulingPanel.IsVisible = true;
#endif

        UpdateLoginButtonState(_sessionService.IsSessionValid());
    }

    private void LoadProfilePhoto()
    {
        var photoPath = _sessionService.GetProfileImagePath();
        if (!string.IsNullOrEmpty(photoPath) && System.IO.File.Exists(photoPath))
        {
            ProfilePhoto.Source = ImageSource.FromFile(photoPath);
            ProfilePhoto.IsVisible = true;
            ProfileEmoji.IsVisible = false;
            ProfilePhoto.Clip = new EllipseGeometry
            {
                Center = new Point(28, 28),
                RadiusX = 28,
                RadiusY = 28
            };
        }
        else
        {
            ProfilePhoto.IsVisible = false;
            ProfileEmoji.IsVisible = true;
        }
    }

    private async void OnProfilePhotoTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Please pick a photo"
            });

            if (result != null)
            {
                var newFile = System.IO.Path.Combine(FileSystem.AppDataDirectory, result.FileName);
                var oldPath = _sessionService.GetProfileImagePath();
                if (!string.IsNullOrEmpty(oldPath) && oldPath != newFile && System.IO.File.Exists(oldPath))
                {
                    try { System.IO.File.Delete(oldPath); } catch { }
                }

                using (var stream = await result.OpenReadAsync())
                using (var newStream = System.IO.File.Create(newFile))
                    await stream.CopyToAsync(newStream);

                _sessionService.SetProfileImagePath(newFile);
                LoadProfilePhoto();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Photo", $"Could not pick photo: {ex.Message}", "OK");
        }
    }

    private void OnDisplayNameChanged(object? sender, EventArgs e)
    {
        _sessionService.SetDisplayName(DisplayNameEntry.Text ?? "User");
    }

    private async void UpdateLoginButtonState(bool isSessionValid)
    {
        await LoginButton.FadeTo(0.5, 100);

        if (isSessionValid)
        {
            LoginButton.Text = "Session OK";
            LoginButton.BackgroundColor = GetThemeColor("Success", "#22946E");
            LoginButton.IsEnabled = false;
            SessionDot.BackgroundColor = GetThemeColor("Success", "#22946E");
            SessionStatusLabel.Text = "Session active";
            var lastCheck = _sessionService.GetLastCheckTime();
            SessionLastCheckLabel.Text = lastCheck.HasValue ? $"Verified {lastCheck.Value:MMM dd, HH:mm}" : "";
        }
        else
        {
            LoginButton.Text = "Login to TikTok";
            LoginButton.BackgroundColor = GetThemeColor("Primary", "#FE2C55");
            LoginButton.IsEnabled = true;
            SessionDot.BackgroundColor = GetThemeColor("Error", "#9C2121");
            SessionStatusLabel.Text = "Not logged in";
            SessionLastCheckLabel.Text = "Tap below to login";
        }

        await LoginButton.FadeTo(1.0, 200);
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new LoginPage());
    }

    private async void OnShortcutTutorialClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new IosShortcutTutorialPage());
    }

    private void OnScheduleToggled(object? sender, ToggledEventArgs e)
    {
        ScheduleOptionsPanel.IsVisible = e.Value;
        _settingsService.SetScheduled(e.Value);
#if ANDROID
        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        if (e.Value)
            TiktokStreakSaver.Platforms.Android.StreakScheduler.ScheduleNextRun(context);
        else
            TiktokStreakSaver.Platforms.Android.StreakScheduler.CancelSchedule(context);
#endif
    }

    private void OnFixedTimeToggled(object? sender, ToggledEventArgs e)
    {
        _settingsService.SetUseFixedTime(e.Value);
        TimePickerRow.IsVisible = e.Value;
        IntervalPanel.IsVisible = !e.Value;
#if ANDROID
        if (_settingsService.IsScheduled())
        {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            TiktokStreakSaver.Platforms.Android.StreakScheduler.ScheduleNextRun(context);
        }
#endif
    }

    private void OnTimePickerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimePicker.Time)) return;
        var time = ScheduleTimePicker.Time;
        _settingsService.SetFixedTimeHour(time.Hours);
        _settingsService.SetFixedTimeMinute(time.Minutes);
#if ANDROID
        if (_settingsService.IsScheduled() && _settingsService.GetUseFixedTime())
        {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            TiktokStreakSaver.Platforms.Android.StreakScheduler.ScheduleNextRun(context);
        }
#endif
    }

    private void OnIntervalHoursChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_suppressIntervalChanged) return;
        var hours = (int)Math.Round(e.NewValue);
        _settingsService.SetIntervalHours(hours);
        UpdateIntervalLabels();
#if ANDROID
        if (_settingsService.IsScheduled())
        {
            var ctx = Platform.CurrentActivity ?? Android.App.Application.Context;
            TiktokStreakSaver.Platforms.Android.StreakScheduler.ScheduleNextRun(ctx);
        }
#endif
    }

    private void UpdateIntervalLabels()
    {
        var hours = _settingsService.GetIntervalHours();
        IntervalHoursValueLabel.Text = hours.ToString();
        IntervalSummaryLabel.Text = hours == 1
            ? "Every hour between automatic runs."
            : $"Every {hours} hours between automatic runs.";
    }

    private void OnSkipUnreachableToggled(object? sender, ToggledEventArgs e)
    {
        _settingsService.SetSkipUnreachableUsers(e.Value);
    }

    private void OnRandomizeMessagesToggled(object? sender, ToggledEventArgs e)
    {
        _settingsService.SetRandomizeNormalMessages(e.Value);
    }

    private void OnSendOnBatteryLowToggled(object? sender, ToggledEventArgs e)
    {
        _settingsService.SetSendOnBatteryLow(e.Value);
    }

    private async void OnAboutClicked(object? sender, EventArgs e)
    {
        string currentVersion = AppInfo.Current.VersionString;
        await Navigation.PushModalAsync(new AboutPopupPage(
            "About Streak Saver", currentVersion, string.Empty, false));
    }

    private async void OnCheckUpdatesClicked(object? sender, EventArgs e)
    {
        if (_isCheckingUpdates) return;
        _isCheckingUpdates = true;
        var originalText = CheckUpdatesButton.Text;
        try
        {
            CheckUpdatesButton.IsEnabled = false;
            CheckUpdatesButton.Text = "Checking…";

            var info = await _updateService.CheckForUpdatesAsync();
            if (info == null)
            {
                await DisplayAlert("Update Check", "Could not reach the update server. Please try again later.", "OK");
                return;
            }

            string currentVersion = AppInfo.Current.VersionString;
            string remoteVersion = info.LatestVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? info.LatestVersion.Substring(1)
                : info.LatestVersion;

            if (info.HasUpdate)
            {
                var downloadUrl =
#if IOS
                    info.IpaDownloadUrl ?? info.ReleaseUrl;
#else
                    info.ApkDownloadUrl;
#endif
                await Navigation.PushModalAsync(new AboutPopupPage(
                    "Update Available!", remoteVersion, info.Changelog, true, downloadUrl));
            }
            else
            {
                await DisplayAlert("Up to Date", $"You are already on the latest version (v{currentVersion}).", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Update Check", $"Update check failed: {ex.Message}", "OK");
        }
        finally
        {
            CheckUpdatesButton.Text = originalText;
            CheckUpdatesButton.IsEnabled = true;
            _isCheckingUpdates = false;
        }
    }

    private async void OnPrivacyPolicyClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new PrivacyPage());
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "This will clear your TikTok session. You'll need to login again before running automations.", "Logout", "Cancel");
        if (confirm)
        {
            _sessionService.ClearSession();
            UpdateLoginButtonState(false);
        }
    }
}
