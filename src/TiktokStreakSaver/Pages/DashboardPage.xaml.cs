using Microsoft.Maui.Controls.Shapes;
using TiktokStreakSaver.Models;
using TiktokStreakSaver.Services;
using TiktokStreakSaver.Services.Storage;
using TiktokStreakSaver.Views;

namespace TiktokStreakSaver.Pages;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class DashboardPage : ContentPage
{
    private readonly SettingsService _settingsService;
    private readonly SessionService _sessionService;
    private readonly UpdateService _updateService;
    private bool _isCheckingForUpdates = false;
    private bool _isAppInForeground = false;
    private IDispatcherTimer? _statusTimer;
    private readonly NormalProgressDrawable _normalProgressDrawable;

    public DashboardPage()
    {
        InitializeComponent();
        _settingsService = new SettingsService();
        _sessionService = new SessionService();
        _updateService = new UpdateService();

        _normalProgressDrawable = new NormalProgressDrawable();
        OverviewProgressGraphicsView.Drawable = _normalProgressDrawable;
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
        _isAppInForeground = true;

        this.Opacity = 0;
        this.TranslationY = 12;
        await Task.WhenAll(
            this.FadeTo(1, 280, Easing.SinInOut),
            this.TranslateTo(0, 0, 280, Easing.SinInOut));

        GreetingLabel.Text = $"Hi, {_sessionService.GetDisplayName()}";

        LoadProfilePhoto();
        UpdateSessionIndicator();

        LoadSettings();
        UpdateStatus();

        CheckGlobalSessionStatus();

        await EvaluatePermissionsAsync();

        if (_statusTimer == null)
        {
            _statusTimer = Dispatcher.CreateTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(1);
            _statusTimer.Tick += OnStatusTimerTick;
        }
        _statusTimer.Start();
        OnStatusTimerTick(null, EventArgs.Empty);

        _ = CheckStartupPopupAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isAppInForeground = false;
        if (_statusTimer != null)
        {
            _statusTimer.Stop();
            _statusTimer.Tick -= OnStatusTimerTick;
            _statusTimer = null;
        }
    }

    private void LoadProfilePhoto()
    {
        var photoPath = _sessionService.GetProfileImagePath();
        if (!string.IsNullOrEmpty(photoPath) && System.IO.File.Exists(photoPath))
        {
            ProfileAvatarImage.Source = ImageSource.FromFile(photoPath);
            ProfileAvatarImage.IsVisible = true;
            ProfileAvatarEmoji.IsVisible = false;
            ProfileAvatarImage.Clip = new EllipseGeometry
            {
                Center = new Point(22, 22),
                RadiusX = 22,
                RadiusY = 22
            };
        }
        else
        {
            ProfileAvatarImage.IsVisible = false;
            ProfileAvatarEmoji.IsVisible = true;
        }
    }

    private void UpdateSessionIndicator()
    {
        bool valid = _sessionService.IsSessionValid();
        MasterRunButton.IsEnabled = true;
        MasterRunButton.Opacity = 1.0;
        MasterRunButton.Text = valid ? "Run Now" : "Login Required";
    }

    private void CheckGlobalSessionStatus()
    {
#if IOS
        if (TikTokWebViewHelper.HasValidSessionCookie())
            _sessionService.SetSessionValid(true);
        else if (!_sessionService.IsSessionValid())
            _sessionService.SetSessionValid(false);
#else
        bool isValid = TikTokWebViewHelper.HasValidSessionCookie();
        _sessionService.SetSessionValid(isValid);
#endif
        UpdateSessionIndicator();
    }

    private void OnStatusTimerTick(object? sender, EventArgs e)
    {
        bool isRunning = false;
#if ANDROID
        isRunning = TiktokStreakSaver.Platforms.Android.Services.StreakService.IsRunning;
#elif IOS
        isRunning = Platforms.iOS.Services.IosStreakRunner.IsRunning;
#endif
        RunButtonsContainer.IsVisible = !isRunning;
        StopServiceButton.IsVisible = isRunning;

        MessageEditor.IsEnabled = !isRunning;
        MessageEditor.Opacity = isRunning ? 0.6 : 1.0;

        UpdateStatus();
    }

    private static string NormalizeVersion(string raw)
        => raw.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? raw.Substring(1) : raw;

    private async Task CheckStartupPopupAsync()
    {
        if (_isCheckingForUpdates) return;
        _isCheckingForUpdates = true;
        try
        {
            if (!_isAppInForeground) return;
            if (Navigation.ModalStack.Any(p => p is AboutPopupPage)) return;

            string currentVersion = NormalizeVersion(AppInfo.Current.VersionString);

            bool updateJustInstalled = Preferences.Default.Get("UpdateJustInstalled", false);
            if (updateJustInstalled)
            {
                Preferences.Default.Remove("UpdateJustInstalled");
                Preferences.Default.Set("LastAppVersionSeen", currentVersion);
                _isCheckingForUpdates = false;
                await CheckUpdateOnlyAsync();
                return;
            }

            string lastAppSeen = NormalizeVersion(Preferences.Default.Get("LastAppVersionSeen", string.Empty));
            if (string.IsNullOrEmpty(lastAppSeen) || lastAppSeen != currentVersion)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Navigation.PushModalAsync(new AboutPopupPage(
                        "Welcome to Streak Saver", currentVersion, string.Empty, false)));
                return;
            }

            _isCheckingForUpdates = false;
            await CheckUpdateOnlyAsync();
        }
        catch { }
        finally { _isCheckingForUpdates = false; }
    }

    private async Task CheckUpdateOnlyAsync()
    {
        if (_isCheckingForUpdates) return;
        _isCheckingForUpdates = true;
        try
        {
            if (!_isAppInForeground) return;
            if (Navigation.ModalStack.Any(p => p is AboutPopupPage)) return;

            string currentVersion = NormalizeVersion(AppInfo.Current.VersionString);
            string lastRemoteSeen = NormalizeVersion(Preferences.Default.Get("LastRemoteVersionSeen", string.Empty));

            var updateCheck = await _updateService.CheckForUpdatesAsync();
            if (updateCheck == null || !updateCheck.HasUpdate) return;

            string remoteVersion = NormalizeVersion(updateCheck.LatestVersion);
            if (remoteVersion == lastRemoteSeen || remoteVersion == currentVersion) return;
            if (Navigation.ModalStack.Any(p => p is AboutPopupPage)) return;

            var downloadUrl =
#if IOS
                updateCheck.IpaDownloadUrl ?? updateCheck.ReleaseUrl;
#else
                updateCheck.ApkDownloadUrl;
#endif
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Navigation.PushModalAsync(new AboutPopupPage(
                    "Update Available!", remoteVersion, updateCheck.Changelog, true, downloadUrl)));
        }
        catch { }
        finally { _isCheckingForUpdates = false; }
    }

    private void LoadSettings()
    {
        MessageEditor.Text = _settingsService.GetMessageText();

        var isRandomized = _settingsService.GetRandomizeNormalMessages();
        MessageEditor.IsEnabled = !isRandomized;
        MessageEditorBorder.Opacity = isRandomized ? 0.4 : 1.0;
        MessageEditorHint.Text = isRandomized
            ? "Randomized messages enabled — 50 built-in variants"
            : "Message sent to each friend during a streak run";
    }

    private void UpdateStatus()
    {
        var isScheduled = _settingsService.IsScheduled();
        var lastRun = _settingsService.GetLastRunTime();
        if (lastRun.HasValue)
        {
            var timeSince = DateTime.Now - lastRun.Value;
            if (timeSince.TotalMinutes < 60) LastRunLabel.Text = $"{(int)timeSince.TotalMinutes} minutes ago";
            else if (timeSince.TotalHours < 24) LastRunLabel.Text = $"{(int)timeSince.TotalHours} hours ago";
            else LastRunLabel.Text = lastRun.Value.ToString("MMM dd, HH:mm");
        }
        else LastRunLabel.Text = "Never";

        if (isScheduled)
        {
            NextRunLabel.Text = FormatNextRun(_settingsService.GetNextRunTime());
        }
        else NextRunLabel.Text = "Not scheduled";

        var history = _settingsService.GetRunHistory();
        var latestResult = history.FirstOrDefault();
        var currentEnabledFriends = _settingsService.GetEnabledFriends();

        bool ranToday = latestResult != null && latestResult.RunTime.Date == DateTime.Today;

        int sentToday = 0;
        int successPercent = 0;
        int remainingToday = currentEnabledFriends.Count;
        string progressText = $"0/{currentEnabledFriends.Count}";
        float progressFraction = 0f;

        if (ranToday && latestResult != null)
        {
            sentToday = latestResult.FriendResults.Count(r => r.Success);
            int totalAttempted = latestResult.FriendResults.Count;

            if (totalAttempted > 0)
                successPercent = (int)((double)sentToday / totalAttempted * 100);
            else
                successPercent = 0;

            remainingToday = Math.Max(0, currentEnabledFriends.Count - sentToday);
            progressText = $"{sentToday}/{currentEnabledFriends.Count}";
            progressFraction = currentEnabledFriends.Count > 0 ? (float)sentToday / currentEnabledFriends.Count : 0f;
        }

        OverviewSentLabel.Text = sentToday.ToString();
        OverviewSuccessLabel.Text = ranToday ? $"{successPercent}%" : "--";
        OverviewRemainingLabel.Text = remainingToday.ToString();

        OverviewProgressLabel.Text = progressText;
        _normalProgressDrawable.Progress = progressFraction;
        _normalProgressDrawable.IsDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
        OverviewProgressGraphicsView.Invalidate();
    }

    /// <summary>
    /// Human-readable next-run label.
    /// Imminent runs use a relative phrasing ("Now", "In 12 minutes"); future runs are anchored to
    /// the calendar day ("Today at 09:00", "Tomorrow at 09:00", "Wed, May 14 at 09:00") so the
    /// fixed-daily-schedule mode reads naturally even when the next run is up to a day away.
    /// </summary>
    private static string FormatNextRun(DateTime nextRun)
    {
        var now = DateTime.Now;
        var timeUntil = nextRun - now;

        if (timeUntil.TotalSeconds < 30)
            return "Now";
        if (timeUntil.TotalMinutes < 1)
            return "In <1 min";
        if (timeUntil.TotalMinutes < 60)
            return $"In {(int)timeUntil.TotalMinutes} min";

        var time = nextRun.ToString("HH:mm");
        var nextDate = nextRun.Date;
        var today = now.Date;

        if (nextDate == today)
            return $"Today at {time}";
        if (nextDate == today.AddDays(1))
            return $"Tomorrow at {time}";

        return nextRun.ToString("ddd, MMM dd") + $" at {time}";
    }

    private void OnMessageChanged(object? sender, TextChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.NewTextValue)) _settingsService.SetMessageText(e.NewTextValue);
    }

    private async void OnMasterRunClicked(object? sender, EventArgs e)
    {
        await MasterRunButton.ScaleTo(0.94, 60, Easing.CubicIn);
        await MasterRunButton.ScaleTo(1.0, 100, Easing.CubicOut);

        CheckGlobalSessionStatus();
        if (!_sessionService.IsSessionValid())
        {
            await Navigation.PushAsync(new LoginPage());
            return;
        }

        var friends = _settingsService.GetEnabledFriends();
        if (friends.Count == 0)
        {
            await DisplayAlert("No Friends", "Please add at least one friend before running.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Run Now",
            $"This will send your streak message to {friends.Count} friend{(friends.Count != 1 ? "s" : "")}. Continue?",
            "Run", "Cancel");
        if (!confirm) return;

#if ANDROID
        bool permissionGranted = await RequestNotificationPermission();
        if (!permissionGranted) return;

        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        bool started = TiktokStreakSaver.Platforms.Android.StreakScheduler.RunNow(context);
        if (started)
        {
            await DisplayAlert("Started", "Streak run started. Check the notification for progress.", "OK");
            UpdateStatus();
        }
        else
        {
            await DisplayAlert("Already Running", "A process is already running. Please wait for it to finish.", "OK");
        }
#elif IOS
        await Platforms.iOS.Services.IosNotificationService.RequestPermissionAsync();
        bool started = await Platforms.iOS.Services.IosStreakRunner.RunNowAsync();
        if (started)
        {
            await DisplayAlert("Started", "Streak run started. You'll get a notification when it finishes.", "OK");
            UpdateStatus();
        }
        else
        {
            await DisplayAlert("Already Running", "A streak run is already in progress.", "OK");
        }
#else
        await DisplayAlert("Info", "This feature is only available on Android", "OK");
#endif
    }

    private async void OnStopServiceClicked(object? sender, EventArgs e)
    {
        await StopServiceButton.ScaleTo(0.94, 60, Easing.CubicIn);
        await StopServiceButton.ScaleTo(1.0, 100, Easing.CubicOut);
#if ANDROID
        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        TiktokStreakSaver.Platforms.Android.StreakScheduler.StopService(context);
#endif
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        GreetingLabel.Text = $"Hi, {_sessionService.GetDisplayName()}";
        LoadProfilePhoto();
        UpdateSessionIndicator();
        LoadSettings();
        UpdateStatus();
        await EvaluatePermissionsAsync();
        await CheckUpdateOnlyAsync();
        MainRefreshView.IsRefreshing = false;
    }

    private async Task EvaluatePermissionsAsync()
    {
#if IOS
        var authRequired = AppStorageProvider.Current.GetBool(AppConstants.AuthRequiredKey);
        if (authRequired && _sessionService.IsSessionValid())
        {
            PermissionsPanel.IsVisible = true;
            BtnNotification.IsVisible = false;
            BtnExactAlarm.IsVisible = false;
            BtnBatteryOpt.IsVisible = false;
        }
        else
        {
            PermissionsPanel.IsVisible = false;
        }
        await Platforms.iOS.Services.IosNotificationService.RequestPermissionAsync();
#elif ANDROID
        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        bool exactAlarmGranted = TiktokStreakSaver.Platforms.Android.StreakScheduler.CanScheduleExactAlarms(context);
        bool batteryOptGranted = TiktokStreakSaver.Platforms.Android.StreakScheduler.IsIgnoringBatteryOptimizations(context);
        bool notificationGranted = true;
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            notificationGranted = (status == PermissionStatus.Granted);
        }
        BtnExactAlarm.IsVisible = !exactAlarmGranted;
        BtnBatteryOpt.IsVisible = !batteryOptGranted;
        BtnNotification.IsVisible = !notificationGranted;
        PermissionsPanel.IsVisible = !exactAlarmGranted || !batteryOptGranted || !notificationGranted;
#else
        await Task.CompletedTask;
        PermissionsPanel.IsVisible = false;
#endif
    }

    private void OnRequestExactAlarmClicked(object? sender, EventArgs e)
    {
#if ANDROID
        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        TiktokStreakSaver.Platforms.Android.StreakScheduler.RequestExactAlarmPermission(context);
#endif
    }

    private void OnRequestBatteryOptimizationClicked(object? sender, EventArgs e)
    {
#if ANDROID
        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        TiktokStreakSaver.Platforms.Android.StreakScheduler.RequestBatteryOptimizationExemption(context);
#endif
    }

    private async void OnRequestNotificationClicked(object? sender, EventArgs e)
    {
        await RequestNotificationPermission();
        await EvaluatePermissionsAsync();
    }

    private async Task<bool> RequestNotificationPermission()
    {
#if ANDROID
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
        {
            var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission Required", "Notification permission is required to show status while sending streaks.", "OK");
                return false;
            }
        }
#else
        await Task.CompletedTask;
#endif
        return true;
    }
}
