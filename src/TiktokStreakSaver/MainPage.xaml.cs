using Microsoft.Maui.Controls.Shapes;
using TiktokStreakSaver.Models;
using TiktokStreakSaver.Services;

namespace TiktokStreakSaver;
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class MainPage : ContentPage
{
    private readonly SettingsService _settingsService;
    private readonly SessionService _sessionService;
    private readonly UpdateService _updateService;
    private bool _isCheckingSession = false;
    private bool _sessionCheckCompleted = false;
    private bool _isCheckingForUpdates = false;
    private bool _isAppInForeground = false;
    private IDispatcherTimer? _statusTimer;
    private bool _suppressIntervalChanged;
    private bool _suppressSettingsToggles;
    private const string InitialScheduleArmedKey = "initial_background_schedule_armed";

    public MainPage()
    {
        InitializeComponent();
        _settingsService = new SettingsService();
        _sessionService = new SessionService();
        _updateService = new UpdateService();
    }

    private Color GetThemeColor(string key, string fallbackHex = "#92979E")
    {
        if (Application.Current != null && Application.Current.Resources.TryGetValue(key, out var resource) && resource is Color color)
        {
            return color;
        }
        return Color.FromArgb(fallbackHex);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isAppInForeground = true;
        LoadSettings();
        LoadFriendsList();
        LoadHistory();
        UpdateStatus();
        
        await EvaluatePermissionsAsync();
        
#if ANDROID
        TryArmInitialScheduleIfNeeded();
#endif
        
        // Start status timer for tracking service
        if (_statusTimer == null)
        {
            _statusTimer = Dispatcher.CreateTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(1);
            _statusTimer.Tick += OnStatusTimerTick;
        }
        _statusTimer.Start();

        // Check session status
        CheckSessionStatus();

        // Check for updates or first-launch popup (non-blocking)
        _ = CheckStartupPopupAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isAppInForeground = false;
        _statusTimer?.Stop();
    }

    private void OnStatusTimerTick(object? sender, EventArgs e)
    {
        bool isRunning = false;
#if ANDROID
        isRunning = TiktokStreakSaver.Platforms.Android.Services.StreakService.IsRunning;
#endif

        if (isRunning)
        {
            RunButtonsContainer.IsVisible = false;
            StopServiceButton.IsVisible = true;
        }
        else
        {
            RunButtonsContainer.IsVisible = true;
            StopServiceButton.IsVisible = false;
            
            // Refresh labels if run just ended
            UpdateStatus();
        }
    }

    // ─── Normalizes "v1.6.0" → "1.6.0" to ensure consistent Preferences storage ─────
    private static string NormalizeVersion(string raw)
        => raw.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? raw.Substring(1) : raw;

    // ─── Full startup flow: Welcome check THEN update check ─────────────────────────
    private async Task CheckStartupPopupAsync()
    {
        if (_isCheckingForUpdates) return;
        _isCheckingForUpdates = true;

        try
        {
            if (!_isAppInForeground) return;
            if (Navigation.ModalStack.Any(p => p is AboutPopupPage)) return;

            string currentVersion = NormalizeVersion(AppInfo.Current.VersionString);

            // ── Guard: if user just completed an in-app update install, skip Welcome ──
            bool updateJustInstalled = Preferences.Default.Get("UpdateJustInstalled", false);
            if (updateJustInstalled)
            {
                Preferences.Default.Remove("UpdateJustInstalled");
                Preferences.Default.Set("LastAppVersionSeen", currentVersion);
                // Release flag so CheckUpdateOnlyAsync's own guard can pass
                _isCheckingForUpdates = false;
                await CheckUpdateOnlyAsync();
                return;
            }

            string lastAppSeen = NormalizeVersion(Preferences.Default.Get("LastAppVersionSeen", string.Empty));

            // ── Step 1: First-launch / new-install onboarding ──────────────────────
            if (string.IsNullOrEmpty(lastAppSeen) || lastAppSeen != currentVersion)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await Navigation.PushModalAsync(new AboutPopupPage(
                        "Welcome to Streak Saver",
                        currentVersion,
                        string.Empty,   // changelog not shown on welcome screen
                        false)));
                // Popup's OnCloseClicked saves LastAppVersionSeen; do not run update check yet.
                return;
            }

            // ── Step 2: Silent update check ────────────────────────────────────────
            _isCheckingForUpdates = false;
            await CheckUpdateOnlyAsync();
        }
        catch
        {
            // Swallow safely — do not block UI
        }
        finally
        {
            _isCheckingForUpdates = false;
        }
    }

    // ─── Update-only check (used by pull-to-refresh, never shows Welcome) ────────────
    private async Task CheckUpdateOnlyAsync()
    {
        if (_isCheckingForUpdates) return;
        _isCheckingForUpdates = true;

        try
        {
            if (!_isAppInForeground) return;
            if (Navigation.ModalStack.Any(p => p is AboutPopupPage)) return;

            string currentVersion  = NormalizeVersion(AppInfo.Current.VersionString);
            string lastRemoteSeen  = NormalizeVersion(Preferences.Default.Get("LastRemoteVersionSeen", string.Empty));

            var updateCheck = await _updateService.CheckForUpdatesAsync();
            if (updateCheck == null || !updateCheck.HasUpdate) return;

            string remoteVersion = NormalizeVersion(updateCheck.LatestVersion);

            // Show only if this exact remote version hasn't been dismissed already
            if (remoteVersion == lastRemoteSeen || remoteVersion == currentVersion) return;

            // Final guard: re-check modal stack after async HTTP call completes
            if (Navigation.ModalStack.Any(p => p is AboutPopupPage)) return;

            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Navigation.PushModalAsync(new AboutPopupPage(
                    "Update Available!",
                    remoteVersion,
                    updateCheck.Changelog,
                    true,
                    updateCheck.ApkDownloadUrl)));
        }
        catch
        {
            // Silent failure — network unavailable, etc.
        }
        finally
        {
            _isCheckingForUpdates = false;
        }
    }

    private async void OnCheckUpdatesClicked(object? sender, EventArgs e)
    {
        if (_isCheckingForUpdates) return;
        _isCheckingForUpdates = true;

        try
        {
            if (Navigation.ModalStack.Any(p => p is AboutPopupPage)) return;

            var updateCheck = await _updateService.CheckForUpdatesAsync();
            if (updateCheck != null && updateCheck.HasUpdate)
            {
                string remoteVersion = NormalizeVersion(updateCheck.LatestVersion);
                await Navigation.PushModalAsync(new AboutPopupPage(
                    "Update Available!",
                    remoteVersion,
                    updateCheck.Changelog,
                    true,
                    updateCheck.ApkDownloadUrl));
            }
            else
            {
                await DisplayAlert("Up to Date", "You are running the latest version of Streak Saver.", "OK");
            }
        }
        catch
        {
            await DisplayAlert("Network Error", "Could not check for updates. Please check your connection.", "OK");
        }
        finally
        {
            _isCheckingForUpdates = false;
        }
    }

    private void CheckSessionStatus()
    {
        // If we already checked this session, just update the button state
        if (_sessionCheckCompleted)
        {
            UpdateLoginButtonState(_sessionService.IsSessionValid());
            return;
        }

        // On first install, default to not logged in
        // Only trust saved session if user has previously logged in successfully
        var lastCheck = _sessionService.GetLastCheckTime();
        if (lastCheck == null)
        {
            // Never checked before - assume not logged in
            _sessionCheckCompleted = true;
            UpdateLoginButtonState(false);
            return;
        }

        // Start session validation
        _isCheckingSession = true;
        _navigationCount = 0;
        UpdateLoginButtonState(false, isChecking: true);

#if ANDROID
        // Configure WebView for session check using helper
        TikTokWebViewHelper.ConfigureWebView(SessionCheckWebView);
        
        // Load messages page to check if we're logged in
        SessionCheckWebView.Source = TikTokWebViewHelper.MessagesUrl;
        
        // Stop previous timer if still running (prevents stale fire)
        if (_sessionCheckTimeout != null)
        {
            _sessionCheckTimeout.Stop();
            _sessionCheckTimeout.Tick -= OnSessionCheckTimeout;
        }

        // Set a timeout - if no redirect after 10 seconds, check current state
        _sessionCheckTimeout = Dispatcher.CreateTimer();
        _sessionCheckTimeout.Interval = TimeSpan.FromSeconds(10);
        _sessionCheckTimeout.Tick += OnSessionCheckTimeout;
        _sessionCheckTimeout.Start();
#else
        // On non-Android platforms, just check the saved session state
        _sessionCheckCompleted = true;
        UpdateLoginButtonState(_sessionService.IsSessionValid());
#endif
    }

    private int _navigationCount = 0;
#if ANDROID
    private IDispatcherTimer? _sessionCheckTimeout;
#endif

#if ANDROID
    private void OnSessionCheckTimeout(object? sender, EventArgs e)
    {
        _sessionCheckTimeout?.Stop();
        
        if (_isCheckingSession)
        {
            // Timeout reached - assume not logged in for safety
            _isCheckingSession = false;
            _sessionCheckCompleted = true;
            _sessionService.SetSessionValid(false);
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateLoginButtonState(false);
            });
        }
    }
#endif

    private void OnSessionCheckNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (!_isCheckingSession) return;
        
        _navigationCount++;
        
        // Use helper to check login status
        var result = TikTokWebViewHelper.CheckLoginStatus(e.Url);
        
        // If redirected to login, we're definitely not logged in
        if (result.IsValidUrl && e.Url?.ToLower().Contains("/login") == true)
        {
#if ANDROID
            _sessionCheckTimeout?.Stop();
#endif
            _isCheckingSession = false;
            _sessionCheckCompleted = true;
            
            TikTokWebViewHelper.UpdateSessionStatus(_sessionService, false);
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateLoginButtonState(false);
            });
            return;
        }
        
        // If we're on messages page and this is at least the 2nd navigation (after potential redirect)
        // then we're likely logged in
        if (result.IsLoggedIn && _navigationCount >= 1)
        {
            // Wait a bit more to ensure no further redirect to login
            Task.Delay(2000).ContinueWith(_ =>
            {
                if (_isCheckingSession)
                {
#if ANDROID
                    _sessionCheckTimeout?.Stop();
#endif
                    _isCheckingSession = false;
                    _sessionCheckCompleted = true;
                    
                    TikTokWebViewHelper.UpdateSessionStatus(_sessionService, true);
                    
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        UpdateLoginButtonState(true);
                    });
                }
            });
        }
    }

    private void UpdateLoginButtonState(bool isSessionValid, bool isChecking = false)
    {
        if (isChecking)
        {
            LoginButton.Text = string.Empty;
            LoginButton.BackgroundColor = GetThemeColor("Gray400");
            LoginButton.IsEnabled = false;
            SessionCheckingIndicator.IsVisible = true;
            MasterRunButton.IsEnabled = false;
            MasterRunButton.Opacity = 0.5;
        }
        else if (isSessionValid)
        {
            LoginButton.Text = "Session OK";
            LoginButton.BackgroundColor = GetThemeColor("Success", "#22946E");
            LoginButton.IsEnabled = false;
            SessionCheckingIndicator.IsVisible = false;
            MasterRunButton.IsEnabled = true;
            MasterRunButton.Opacity = 1.0;
        }
        else
        {
            LoginButton.Text = "Login to TikTok";
            LoginButton.BackgroundColor = GetThemeColor("Primary", "#FE2C55");
            LoginButton.IsEnabled = true;
            SessionCheckingIndicator.IsVisible = false;
            MasterRunButton.IsEnabled = false;
            MasterRunButton.Opacity = 0.5;
        }
    }

    private void LoadSettings()
    {
        MessageEditor.Text = _settingsService.GetMessageText();

        _suppressSettingsToggles = true;
        try
        {
            ScheduleSwitch.IsToggled = _settingsService.IsScheduled();
            SkipUnreachableSwitch.IsToggled = _settingsService.GetSkipUnreachableUsers();
        }
        finally
        {
            _suppressSettingsToggles = false;
        }

        if (_settingsService.IsBurstModeActive())
            SetBurstModeUI();
        else
            SetNormalModeUI();

        LoadBurstMessages();
        BurstTargetUserEntry.Text = _settingsService.GetBurstTargetUsername();

        _suppressIntervalChanged = true;
        try
        {
            var totalMin = _settingsService.GetIntervalMinutes();
            IntervalHoursStepper.Value = totalMin / 60;
            IntervalMinutesStepper.Value = totalMin % 60;
            UpdateIntervalSummaryLabel();
            UpdateIntervalStepperLabels();
        }
        finally
        {
            _suppressIntervalChanged = false;
        }
    }

    private void OnNormalModeTapped(object? sender, TappedEventArgs e)
    {
        _settingsService.SetBurstModeActive(false);
        SetNormalModeUI();
    }

    private void OnBurstModeTapped(object? sender, TappedEventArgs e)
    {
        _settingsService.SetBurstModeActive(true);
        SetBurstModeUI();
    }

    private void SetNormalModeUI()
    {
        NormalModeTabBorder.BackgroundColor = GetThemeColor("Primary", "#FE2C55");
        NormalModeTabLabel.TextColor = GetThemeColor("White", "#FFFFFF");
        BurstModeTabBorder.BackgroundColor = Colors.Transparent;
        BurstModeTabLabel.TextColor = GetThemeColor("Gray600", "#4B5563");

        NormalModeContainer.IsVisible = true;
        BurstModeContainer.IsVisible = false;

        MasterRunButton.Text = "START REGULAR STREAK";
        MasterRunButton.BackgroundColor = GetThemeColor("Primary", "#FE2C55");
        MasterRunButton.TextColor = GetThemeColor("White", "#FFFFFF");
    }

    private void SetBurstModeUI()
    {
        BurstModeTabBorder.BackgroundColor = Color.FromArgb("#8B5CF6");
        BurstModeTabLabel.TextColor = Colors.White;
        NormalModeTabBorder.BackgroundColor = Colors.Transparent;
        NormalModeTabLabel.TextColor = GetThemeColor("Gray600", "#4B5563");

        NormalModeContainer.IsVisible = false;
        BurstModeContainer.IsVisible = true;

        MasterRunButton.Text = "START INFINITE BURST";
        MasterRunButton.BackgroundColor = Color.FromArgb("#8B5CF6");
        MasterRunButton.TextColor = Colors.White;
    }

    private void LoadBurstMessages()
    {
        BurstMessagesStack.Children.Clear();
        var msgs = _settingsService.GetBurstMessages();
        if (msgs.Count == 0) msgs.Add(SettingsService.DefaultMessage);
        foreach (var m in msgs)
            AddBurstMessageEditorUI(m);
        UpdateAddBurstMessageButtonVisibility();
    }

    private void AddBurstMessageEditorUI(string initialText)
    {
        var border = new Border
        {
            Stroke = GetThemeColor("BorderColorLight", "#E5E5E5"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Margin = new Thickness(0, 0, 0, 8)
        };
        border.SetAppThemeColor(Border.StrokeProperty,
            GetThemeColor("BorderColorLight", "#E5E5E5"),
            GetThemeColor("BorderColorDark", "#404040"));

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var editor = new Editor
        {
            Text = initialText,
            Placeholder = "Enter burst message...",
            HeightRequest = 60,
            Margin = new Thickness(8)
        };
        editor.TextChanged += OnBurstSettingsChanged;

        var removeBtn = new Button
        {
            Text = "X",
            BackgroundColor = Colors.Transparent,
            TextColor = GetThemeColor("DeleteColor", "#EF4444"),
            FontAttributes = FontAttributes.Bold,
            WidthRequest = 40,
            VerticalOptions = LayoutOptions.Center
        };
        removeBtn.Clicked += (_, _) =>
        {
            if (BurstMessagesStack.Children.Count > 1)
            {
                BurstMessagesStack.Children.Remove(border);
                SaveBurstSettings();
                UpdateAddBurstMessageButtonVisibility();
            }
            else
            {
                _ = DisplayAlert("Required", "Keep at least one burst message.", "OK");
            }
        };

        grid.Children.Add(editor);
        Grid.SetColumn(editor, 0);
        grid.Children.Add(removeBtn);
        Grid.SetColumn(removeBtn, 1);

        border.Content = grid;
        BurstMessagesStack.Children.Add(border);
    }

    private void OnAddBurstMessageClicked(object? sender, EventArgs e)
    {
        if (BurstMessagesStack.Children.Count < 5)
        {
            AddBurstMessageEditorUI("");
            SaveBurstSettings();
            UpdateAddBurstMessageButtonVisibility();
        }
    }

    private void UpdateAddBurstMessageButtonVisibility()
    {
        AddBurstMessageButton.IsVisible = BurstMessagesStack.Children.Count < 5;
    }

    private void OnBurstSettingsChanged(object? sender, TextChangedEventArgs e)
    {
        SaveBurstSettings();
    }

    private void SaveBurstSettings()
    {
        _settingsService.SetBurstTargetUsername(BurstTargetUserEntry.Text?.Trim() ?? "");

        var messages = new List<string>();
        foreach (Border border in BurstMessagesStack.Children)
        {
            if (border.Content is Grid g && g.Children.Count > 0 && g.Children[0] is Editor ed)
            {
                var text = ed.Text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(text))
                    messages.Add(text);
            }
        }

        if (messages.Count == 0)
            messages.Add(SettingsService.DefaultMessage);

        _settingsService.SetBurstMessages(messages);
    }

#if ANDROID
    private void TryArmInitialScheduleIfNeeded()
    {
        if (!_settingsService.IsScheduled()) return;
        if (Preferences.Default.Get(InitialScheduleArmedKey, false)) return;

        var ctx = Platform.CurrentActivity ?? Android.App.Application.Context;
        TiktokStreakSaver.Platforms.Android.StreakScheduler.ScheduleNextRun(ctx);
        Preferences.Default.Set(InitialScheduleArmedKey, true);
    }
#endif

    private async void OnOpenSettingsClicked(object? sender, EventArgs e)
    {
        BottomRunPanel.IsVisible = false;
        SettingsOverlay.IsVisible = true;
        SettingsDrawer.TranslationX = -320;
        await SettingsDrawer.TranslateTo(0, 0, 220, Easing.CubicOut);
    }

    private async void OnCloseSettingsClicked(object? sender, EventArgs e) => await CloseSettingsDrawerAsync();

    private async void OnSettingsScrimTapped(object? sender, TappedEventArgs e) => await CloseSettingsDrawerAsync();

    private async Task CloseSettingsDrawerAsync()
    {
        if (!SettingsOverlay.IsVisible) return;
        await SettingsDrawer.TranslateTo(-320, 0, 200, Easing.CubicIn);
        SettingsOverlay.IsVisible = false;
        BottomRunPanel.IsVisible = true;
    }

    private void UpdateIntervalStepperLabels()
    {
        IntervalHoursValueLabel.Text = ((int)IntervalHoursStepper.Value).ToString();
        IntervalMinutesValueLabel.Text = ((int)IntervalMinutesStepper.Value).ToString();
    }

    private void UpdateIntervalSummaryLabel()
    {
        var t = _settingsService.GetIntervalMinutes();
        var days = t / (24 * 60);
        var rem = t % (24 * 60);
        var h = rem / 60;
        var m = rem % 60;

        if (days > 0)
            IntervalSummaryLabel.Text = $"Every {days}d {h}h {m}m between automatic runs ({t:N0} min total).";
        else if (h > 0)
            IntervalSummaryLabel.Text = $"Every {h}h {m}m between automatic runs ({t:N0} min total).";
        else
            IntervalSummaryLabel.Text = $"Every {m} minutes between automatic runs.";
    }

    private void OnIntervalHoursChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_suppressIntervalChanged) return;
        UpdateIntervalStepperLabels();
        ApplyIntervalFromSteppers();
    }

    private void OnIntervalMinutesChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_suppressIntervalChanged) return;
        UpdateIntervalStepperLabels();
        ApplyIntervalFromSteppers();
    }

    private void ApplyIntervalFromSteppers()
    {
        var h = (int)IntervalHoursStepper.Value;
        var m = (int)IntervalMinutesStepper.Value;
        var total = h * 60 + m;
        var clamped = Math.Clamp(total, SettingsService.MinIntervalMinutes, SettingsService.MaxIntervalMinutes);
        if (clamped != total)
        {
            _suppressIntervalChanged = true;
            try
            {
                IntervalHoursStepper.Value = clamped / 60;
                IntervalMinutesStepper.Value = clamped % 60;
            }
            finally
            {
                _suppressIntervalChanged = false;
            }
        }

        _settingsService.SetIntervalMinutes(clamped);
        UpdateIntervalSummaryLabel();
        UpdateIntervalStepperLabels();

#if ANDROID
        if (_settingsService.IsScheduled())
        {
            var ctx = Platform.CurrentActivity ?? Android.App.Application.Context;
            TiktokStreakSaver.Platforms.Android.StreakScheduler.ScheduleNextRun(ctx);
        }
#endif
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var isScheduled = _settingsService.IsScheduled();
        var lastRun = _settingsService.GetLastRunTime();
        var friendsCount = _settingsService.GetEnabledFriends().Count;

        // Update status label
        if (isScheduled && friendsCount > 0)
        {
            StatusLabel.Text = $"Active • {friendsCount} friend{(friendsCount != 1 ? "s" : "")}";
            StatusLabel.TextColor = GetThemeColor("Success", "#22946E");
        }
        else if (friendsCount == 0)
        {
            StatusLabel.Text = "Add friends to get started";
            StatusLabel.TextColor = GetThemeColor("Gray500", "#666666");
        }
        else
        {
            StatusLabel.Text = "Scheduler disabled";
            StatusLabel.TextColor = GetThemeColor("Gray400", "#92979E");
        }

        // Update last run
        if (lastRun.HasValue)
        {
            var timeSince = DateTime.Now - lastRun.Value;
            if (timeSince.TotalMinutes < 60)
                LastRunLabel.Text = $"{(int)timeSince.TotalMinutes} minutes ago";
            else if (timeSince.TotalHours < 24)
                LastRunLabel.Text = $"{(int)timeSince.TotalHours} hours ago";
            else
                LastRunLabel.Text = lastRun.Value.ToString("MMM dd, HH:mm");
        }
        else
        {
            LastRunLabel.Text = "Never";
        }

        // Update Burst Last run
        var burstLastRun = _settingsService.GetBurstLastRunTime();
        if (burstLastRun.HasValue)
        {
            var timeSinceBurst = DateTime.Now - burstLastRun.Value;
            if (timeSinceBurst.TotalMinutes < 60)
                BurstLastRunLabel.Text = $"{(int)timeSinceBurst.TotalMinutes} minutes ago";
            else if (timeSinceBurst.TotalHours < 24)
                BurstLastRunLabel.Text = $"{(int)timeSinceBurst.TotalHours} hours ago";
            else
                BurstLastRunLabel.Text = burstLastRun.Value.ToString("MMM dd, HH:mm");
        }
        else
        {
            BurstLastRunLabel.Text = "Never";
        }

        // Update next run
        if (isScheduled)
        {
            var nextRun = _settingsService.GetNextRunTime();
            var timeUntil = nextRun - DateTime.Now;
            if (timeUntil.TotalMinutes < 60)
                NextRunLabel.Text = $"In {(int)timeUntil.TotalMinutes} minutes";
            else if (timeUntil.TotalHours < 24)
                NextRunLabel.Text = $"In {(int)timeUntil.TotalHours} hours";
            else
                NextRunLabel.Text = nextRun.ToString("MMM dd, HH:mm");
        }
        else
        {
            NextRunLabel.Text = "Not scheduled";
        }
    }

    private void LoadFriendsList()
    {
        var allFriends = _settingsService.GetFriendsList();

        SearchAndBulkRow.IsVisible = allFriends.Count > 0;
        
        var searchText = SearchFriendEntry.Text?.Trim() ?? string.Empty;
        var displayFriends = allFriends;

        if (!string.IsNullOrEmpty(searchText))
        {
            displayFriends = allFriends.Where(f => 
                (f.Username != null && f.Username.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                (f.DisplayName != null && f.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // Clear existing friend items (except NoFriendsLabel)
        var itemsToRemove = FriendsListContainer.Children
            .Where(c => c != NoFriendsLabel)
            .ToList();

        foreach (var item in itemsToRemove)
        {
            FriendsListContainer.Children.Remove(item);
        }

        if (allFriends.Count == 0)
        {
            NoFriendsLabel.Text = "No friends added. Tap 'Add' to begin.";
            NoFriendsLabel.IsVisible = true;
        }
        else if (displayFriends.Count == 0 && !string.IsNullOrEmpty(searchText))
        {
            NoFriendsLabel.Text = $"No friends found matching '{searchText}'";
            NoFriendsLabel.IsVisible = true;
        }
        else
        {
            NoFriendsLabel.IsVisible = false;
        }

        foreach (var friend in displayFriends)
        {
            var friendView = CreateFriendView(friend);
            FriendsListContainer.Children.Add(friendView);
        }
    }

    private View CreateFriendView(FriendConfig friend)
    {
        var border = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Stroke = Colors.Transparent,
            Padding = new Thickness(12)
        };
        border.SetAppThemeColor(Border.BackgroundColorProperty,
            GetThemeColor("ListItemLight", "#F4F4F4"),
            GetThemeColor("ListItemDark", "#282828"));

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        var infoStack = new VerticalStackLayout { Spacing = 2 };
        
        var displayName = string.IsNullOrEmpty(friend.DisplayName) ? friend.Username : friend.DisplayName;
        infoStack.Children.Add(new Label
        {
            Text = displayName,
            FontSize = 14,
            FontFamily = "InterSemiBold",
            FontAttributes = FontAttributes.Bold
        });
        
        infoStack.Children.Add(new Label
        {
            Text = $"@{friend.Username}",
            FontSize = 12,
            TextColor = GetThemeColor("Gray500", "#666666")
        });

        if (friend.LastMessageSent.HasValue)
        {
            infoStack.Children.Add(new Label
            {
                Text = $"Last sent: {friend.LastMessageSent.Value:MMM dd}",
                FontSize = 11,
                TextColor = GetThemeColor("Gray500", "#666666")
            });
        }

        grid.Children.Add(infoStack);

        var editButton = new Button
        {
            Text = "Edit",
            BackgroundColor = Colors.Transparent,
            FontSize = 12,
            Padding = new Thickness(8),
            HeightRequest = 44,
            VerticalOptions = LayoutOptions.Center
        };
        editButton.SetAppThemeColor(Button.TextColorProperty,
            GetThemeColor("Gray500", "#666666"),
            GetThemeColor("Gray400", "#92979E"));
        editButton.Clicked += async (s, e) =>
        {
            var newName = await DisplayPromptAsync("Edit Friend", "Enter new display name:", initialValue: friend.DisplayName ?? friend.Username);
            if (newName != null)
            {
                friend.DisplayName = newName;
                _settingsService.UpdateFriend(friend);
                LoadFriendsList();
            }
        };
        Grid.SetColumn(editButton, 1);
        grid.Children.Add(editButton);

        var deleteButton = new Button
        {
            Text = "Delete",
            BackgroundColor = Colors.Transparent,
            FontSize = 12,
            Padding = new Thickness(8),
            HeightRequest = 44,
            VerticalOptions = LayoutOptions.Center
        };
        deleteButton.TextColor = GetThemeColor("DeleteColor", "#EE1D52");
        deleteButton.Clicked += async (s, e) =>
        {
            var confirm = await DisplayAlert("Remove Friend", 
                $"Remove {displayName} from the list?", "Remove", "Cancel");
            if (confirm)
            {
                _settingsService.RemoveFriend(friend.Id);
                LoadFriendsList();
                UpdateStatus();
            }
        };
        Grid.SetColumn(deleteButton, 2);
        grid.Children.Add(deleteButton);
        
        var toggleSwitch = new Switch
        {
            IsToggled = friend.IsEnabled,
            VerticalOptions = LayoutOptions.Center
        };
        toggleSwitch.SetAppThemeColor(Switch.ThumbColorProperty,
            GetThemeColor("White", "#FFFFFF"),
            GetThemeColor("White", "#FFFFFF"));
        toggleSwitch.SetAppThemeColor(Switch.OnColorProperty,
            GetThemeColor("Primary", "#FE2C55"),
            GetThemeColor("Primary", "#FE2C55"));
        toggleSwitch.Toggled += (s, e) =>
        {
            friend.IsEnabled = e.Value;
            _settingsService.UpdateFriend(friend);
            UpdateStatus();
        };
        Grid.SetColumn(toggleSwitch, 3);
        grid.Children.Add(toggleSwitch);

        border.Content = grid;
        return border;
    }

    private void LoadHistory()
    {
        var history = _settingsService.GetRunHistory().Take(5).ToList();

        // Clear existing history items (except NoHistoryLabel)
        var itemsToRemove = HistoryContainer.Children
            .Where(c => c != NoHistoryLabel)
            .ToList();

        foreach (var item in itemsToRemove)
        {
            HistoryContainer.Children.Remove(item);
        }

        NoHistoryLabel.IsVisible = history.Count == 0;

        foreach (var run in history)
        {
            var historyView = CreateHistoryView(run);
            HistoryContainer.Children.Add(historyView);
        }
    }

    private View CreateHistoryView(StreakRunResult run)
    {
        var successCount = run.FriendResults.Count(r => r.Success);
        var totalCount = run.FriendResults.Count;
        var statusIcon = run.Success ? "OK" : "ERR";
        var statusColor = run.Success ? GetThemeColor("Success", "#22946E") : GetThemeColor("Error", "#9C2121");

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        var iconLabel = new Label
        {
            Text = statusIcon,
            FontSize = 16,
            TextColor = statusColor,
            VerticalOptions = LayoutOptions.Center
        };
        grid.Children.Add(iconLabel);

        var infoStack = new VerticalStackLayout { Spacing = 2 };
        infoStack.Children.Add(new Label
        {
            Text = run.RunTime.ToString("MMM dd, HH:mm"),
            FontSize = 14
        });
        
        if (totalCount > 0)
        {
            var skippedCount = totalCount - successCount;
            var infoLabel = new Label
            {
                Text = skippedCount > 0
                    ? $"{successCount}/{totalCount} sent, {skippedCount} skipped"
                    : $"{successCount}/{totalCount} messages sent",
                FontSize = 12
            };
            infoLabel.SetAppThemeColor(Label.TextColorProperty,
                GetThemeColor("Gray500", "#666666"),
                GetThemeColor("Gray400", "#92979E"));
            infoStack.Children.Add(infoLabel);
        }
        else if (!string.IsNullOrEmpty(run.ErrorMessage))
        {
            infoStack.Children.Add(new Label
            {
                Text = run.ErrorMessage,
                FontSize = 12,
                TextColor = statusColor,
                LineBreakMode = LineBreakMode.TailTruncation
            });
        }
        
        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);

        return grid;
    }

    private void OnScheduleToggled(object? sender, ToggledEventArgs e)
    {
        if (_suppressSettingsToggles) return;
#if ANDROID
        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        if (e.Value)
            TiktokStreakSaver.Platforms.Android.StreakScheduler.ScheduleNextRun(context);
        else
            TiktokStreakSaver.Platforms.Android.StreakScheduler.CancelSchedule(context);
#endif
        UpdateStatus();
    }

    private void OnSkipUnreachableToggled(object? sender, ToggledEventArgs e)
    {
        if (_suppressSettingsToggles) return;
        _settingsService.SetSkipUnreachableUsers(e.Value);
    }

    private void OnMessageChanged(object? sender, TextChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.NewTextValue))
        {
            _settingsService.SetMessageText(e.NewTextValue);
        }
    }

    private void OnSearchFriendTextChanged(object? sender, TextChangedEventArgs e)
    {
        LoadFriendsList();
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        // Reset session check so it will revalidate when returning
        _sessionCheckCompleted = false;
        await Navigation.PushAsync(new LoginPage());
    }

    private void OnAddFriendClicked(object? sender, EventArgs e)
    {
        AddFriendPanel.IsVisible = true;
        NewFriendUsernameEntry.Text = string.Empty;
        NewFriendDisplayNameEntry.Text = string.Empty;
        NewFriendUsernameEntry.Focus();
    }

    private void OnCancelAddFriend(object? sender, EventArgs e)
    {
        AddFriendPanel.IsVisible = false;
    }

    private async void OnSaveFriend(object? sender, EventArgs e)
    {
        var username = NewFriendUsernameEntry.Text?.Trim().TrimStart('@');
        var displayName = NewFriendDisplayNameEntry.Text?.Trim();

        if (string.IsNullOrEmpty(username))
        {
            await DisplayAlert("Error", "Please enter a username", "OK");
            return;
        }

        // Check for duplicate
        var existingFriends = _settingsService.GetFriendsList();
        if (existingFriends.Any(f => f.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
        {
            await DisplayAlert("Error", "This friend is already in your list", "OK");
            return;
        }

        var friend = new FriendConfig
        {
            Username = username,
            DisplayName = displayName ?? string.Empty,
            IsEnabled = true
        };

        _settingsService.AddFriend(friend);
        AddFriendPanel.IsVisible = false;
        LoadFriendsList();
        UpdateStatus();
    }

    private async Task EvaluatePermissionsAsync()
    {
#if ANDROID
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

    private async Task RequestNotificationPermission()
    {
#if ANDROID
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
        {
            var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission Required", 
                    "Notification permission is required to show status while sending streaks.", "OK");
            }
        }
#endif
    }

    private async void OnMasterRunClicked(object? sender, EventArgs e)
    {
        if (_settingsService.IsBurstModeActive())
        {
            SaveBurstSettings();
            var target = _settingsService.GetBurstTargetUsername().Trim().TrimStart('@');
            if (string.IsNullOrEmpty(target))
            {
                await DisplayAlert("Burst target", "Enter the TikTok username to burst (without @).", "OK");
                return;
            }

            var msgs = _settingsService.GetBurstMessages();
            if (msgs.Count == 0 || msgs.All(string.IsNullOrWhiteSpace))
            {
                await DisplayAlert("Burst messages", "Add at least one non-empty burst message.", "OK");
                return;
            }

            var confirm = await DisplayAlert(
                "Infinite burst",
                $"Send random burst messages to @{target} continuously until you tap Stop. Continue?",
                "Start",
                "Cancel");
            if (!confirm) return;

#if ANDROID
            await RequestNotificationPermission();
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            bool started = TiktokStreakSaver.Platforms.Android.StreakScheduler.RunNow(context, isBurstMode: true);
            if (started)
            {
                await DisplayAlert("Burst started", "Infinite burst is running. Check the notification for progress.", "OK");
                UpdateStatus();
            }
            else
            {
                await DisplayAlert("Already running", "A process is already running. Please wait for it to finish.", "OK");
            }
#else
            await DisplayAlert("Info", "This feature is only available on Android.", "OK");
#endif
            return;
        }

        var friends = _settingsService.GetEnabledFriends();
        if (friends.Count == 0)
        {
            await DisplayAlert("No friends", "Please add at least one friend before running.", "OK");
            return;
        }

        var confirmRegular = await DisplayAlert(
            "Run now",
            $"This will send your streak message to {friends.Count} friend{(friends.Count != 1 ? "s" : "")}. Continue?",
            "Run",
            "Cancel");
        if (!confirmRegular) return;

#if ANDROID
        await RequestNotificationPermission();
        var ctx = Platform.CurrentActivity ?? Android.App.Application.Context;
        bool startedRegular = TiktokStreakSaver.Platforms.Android.StreakScheduler.RunNow(ctx);
        if (startedRegular)
        {
            await DisplayAlert("Started", "Regular streak run started. Check the notification for progress.", "OK");
            UpdateStatus();
        }
        else
        {
            await DisplayAlert("Already running", "A process is already running. Please wait for it to finish.", "OK");
        }
#else
        await DisplayAlert("Info", "This feature is only available on Android.", "OK");
#endif
    }

    private void OnStopServiceClicked(object? sender, EventArgs e)
    {
#if ANDROID
        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        TiktokStreakSaver.Platforms.Android.StreakScheduler.StopService(context);
#endif
    }

    private async void OnImportFriendsClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select friend list JSON file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/json", "*/*" } },
                    { DevicePlatform.iOS,     new[] { "public.json" } },
                    { DevicePlatform.WinUI,   new[] { ".json" } },
                    { DevicePlatform.macOS,   new[] { "json" } },
                })
            });

            if (result == null) return; // user cancelled

            string json;
            using (var stream = await result.OpenReadAsync())
            using (var reader = new System.IO.StreamReader(stream))
            {
                json = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                await DisplayAlert("Import Failed", "The selected file is empty.", "OK");
                return;
            }

            List<FriendConfig>? imported;
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                imported = System.Text.Json.JsonSerializer.Deserialize<List<FriendConfig>>(json, options);
            }
            catch
            {
                await DisplayAlert("Import Failed", "The file is not a valid friend list. Check the format and try again.", "OK");
                return;
            }

            if (imported == null || imported.Count == 0)
            {
                await DisplayAlert("Import", "The file contains no friend entries.", "OK");
                return;
            }

            var existing = _settingsService.GetFriendsList();
            int added = 0, updated = 0, skipped = 0;
            var seenInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in imported)
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(entry.Username) || entry.Username.Trim().TrimStart('@').Length < 2)
                {
                    skipped++;
                    continue;
                }

                // Normalize username
                entry.Username = entry.Username.Trim().TrimStart('@');
                entry.DisplayName = entry.DisplayName?.Trim() ?? string.Empty;

                // Skip duplicates within the same import file (first occurrence wins)
                if (!seenInBatch.Add(entry.Username))
                {
                    skipped++;
                    continue;
                }

                var match = existing.FirstOrDefault(f =>
                    f.Username.Equals(entry.Username, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    // Overwrite existing entry (preserve Id so history links remain intact)
                    entry.Id = match.Id;
                    var idx = existing.IndexOf(match);
                    existing[idx] = entry;
                    updated++;
                }
                else
                {
                    // Ensure a fresh Id if none present
                    if (string.IsNullOrEmpty(entry.Id))
                        entry.Id = Guid.NewGuid().ToString();
                    existing.Add(entry);
                    added++;
                }
            }

            _settingsService.SaveFriendsList(existing);
            LoadFriendsList();
            UpdateStatus();

            var summary = $"Import complete.\n\nAdded: {added}  |  Updated: {updated}  |  Skipped: {skipped}";
            await DisplayAlert("Import Complete", summary, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import Failed", $"Unexpected error: {ex.Message}", "OK");
        }
    }

    private async void OnExportFriendsClicked(object? sender, EventArgs e)
    {
        try
        {
            var friends = _settingsService.GetFriendsList();

            if (friends.Count == 0)
            {
                await DisplayAlert("Export", "Your friend list is empty. Nothing to export.", "OK");
                return;
            }

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(friends, options);

            // Write to app-accessible cache directory, then share
            var fileName = $"streak_friends_{DateTime.Now:yyyyMMdd_HHmm}.json";
            var filePath = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
            await System.IO.File.WriteAllTextAsync(filePath, json);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export Friend List",
                File = new ShareFile(filePath, "application/json")
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export Failed", $"Could not export: {ex.Message}", "OK");
        }
    }

    private bool _isExportingLogs = false;

    private async void OnExportLogsClicked(object? sender, EventArgs e)
    {
        if (_isExportingLogs) return;
        _isExportingLogs = true;

        try
        {
#if ANDROID
            var logs = TiktokStreakSaver.Platforms.Android.Services.StreakService.GetLogs();
#else
            var logs = new List<string>();
#endif

            if (logs == null || logs.Count == 0)
            {
                await DisplayAlert("Export Logs", "No logs to export", "OK");
                return;
            }

            var textContent = string.Join(Environment.NewLine, logs);

            var fileName = $"streak_logs_{DateTime.Now:yyyyMMdd_HHmm}.txt";
            var filePath = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
            await System.IO.File.WriteAllTextAsync(filePath, textContent);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export System Logs",
                File = new ShareFile(filePath, "text/plain")
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export Failed", $"Could not export logs: {ex.Message}", "OK");
        }
        finally
        {
            _isExportingLogs = false;
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        LoadSettings();
        LoadFriendsList();
        LoadHistory();
        UpdateStatus();
        
        await EvaluatePermissionsAsync();
        
        // Passively check for updates only (never shows Welcome on refresh)
        // CheckUpdateOnlyAsync owns the _isCheckingForUpdates guard — safe to call directly
        await CheckUpdateOnlyAsync();
        
        MainRefreshView.IsRefreshing = false;
    }

    private void OnEnableAllClicked(object? sender, EventArgs e)
    {
        var friends = _settingsService.GetFriendsList();
        if (friends.Count == 0) return;

        foreach (var friend in friends)
            friend.IsEnabled = true;

        _settingsService.SaveFriendsList(friends);
        LoadFriendsList();
        UpdateStatus();
    }

    private void OnDisableAllClicked(object? sender, EventArgs e)
    {
        var friends = _settingsService.GetFriendsList();
        if (friends.Count == 0) return;

        foreach (var friend in friends)
            friend.IsEnabled = false;

        _settingsService.SaveFriendsList(friends);
        LoadFriendsList();
        UpdateStatus();
    }

    private async void OnDeleteAllFriendsClicked(object? sender, EventArgs e)
    {
        var friends = _settingsService.GetFriendsList();
        if (friends.Count == 0) return;

        bool confirm = await DisplayAlert("Clear All Friends", "Are you sure you want to remove all friends? This cannot be undone.", "Clear All", "Cancel");
        if (confirm)
        {
            foreach (var friend in friends)
            {
                _settingsService.RemoveFriend(friend.Id);
            }
            LoadFriendsList();
            UpdateStatus();
        }
    }

    private async void OnClearHistoryClicked(object? sender, EventArgs e)
    {
        var history = _settingsService.GetRunHistory();
        if (history.Count == 0) return;

        bool confirm = await DisplayAlert("Clear History", "Are you sure you want to clear your run history?", "Clear", "Cancel");
        if (confirm)
        {
            _settingsService.ClearRunHistory();
            LoadHistory();
        }
    }
}
