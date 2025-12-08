using Microsoft.Maui.Controls.Shapes;
using TiktokStreakSaver.Models;
using TiktokStreakSaver.Services;

namespace TiktokStreakSaver;

public partial class MainPage : ContentPage
{
    private readonly SettingsService _settingsService;
    private readonly SessionService _sessionService;
    private bool _isCheckingSession = false;
    private bool _sessionCheckCompleted = false;

    public MainPage()
    {
        InitializeComponent();
        _settingsService = new SettingsService();
        _sessionService = new SessionService();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadSettings();
        LoadFriendsList();
        LoadHistory();
        UpdateStatus();
        
        // Check session status
        CheckSessionStatus();
    }

    private void CheckSessionStatus()
    {
        // If we already checked this session, just update the button state
        if (_sessionCheckCompleted)
        {
            UpdateLoginButtonState(_sessionService.IsSessionValid());
            return;
        }

        // Start session validation
        _isCheckingSession = true;
        UpdateLoginButtonState(false, isChecking: true);

#if ANDROID
        // Configure WebView for session check using helper
        TikTokWebViewHelper.ConfigureWebView(SessionCheckWebView);
        
        // Load messages page to check if we're logged in
        SessionCheckWebView.Source = TikTokWebViewHelper.MessagesUrl;
#else
        // On non-Android platforms, just check the saved session state
        _sessionCheckCompleted = true;
        UpdateLoginButtonState(_sessionService.IsSessionValid());
#endif
    }

    private void OnSessionCheckNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (!_isCheckingSession) return;
        
        _isCheckingSession = false;
        _sessionCheckCompleted = true;
        
        // Use helper to check login status
        var result = TikTokWebViewHelper.CheckLoginStatus(e.Url);
        
        // Update session service
        TikTokWebViewHelper.UpdateSessionStatus(_sessionService, result.IsLoggedIn);
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateLoginButtonState(result.IsLoggedIn);
        });
    }

    private void UpdateLoginButtonState(bool isSessionValid, bool isChecking = false)
    {
        if (isChecking)
        {
            LoginButton.Text = "Checking session...";
            LoginButton.BackgroundColor = Color.FromArgb("#888888");
            LoginButton.IsEnabled = false;
            SessionCheckingIndicator.IsVisible = true;
        }
        else if (isSessionValid)
        {
            LoginButton.Text = "✓  Session OK";
            LoginButton.BackgroundColor = Color.FromArgb("#4CAF50"); // Green
            LoginButton.IsEnabled = false;
            SessionCheckingIndicator.IsVisible = false;
        }
        else
        {
            LoginButton.Text = "🔐  Login to TikTok";
            LoginButton.BackgroundColor = Color.FromArgb("#FE2C55"); // Primary red
            LoginButton.IsEnabled = true;
            SessionCheckingIndicator.IsVisible = false;
        }
    }

    private void LoadSettings()
    {
        // Load message
        MessageEditor.Text = _settingsService.GetMessageText();

        // Load schedule state
        ScheduleSwitch.IsToggled = _settingsService.IsScheduled();
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
            StatusLabel.TextColor = Color.FromArgb("#4CAF50");
        }
        else if (friendsCount == 0)
        {
            StatusLabel.Text = "Add friends to get started";
            StatusLabel.TextColor = Color.FromArgb("#FFC107");
        }
        else
        {
            StatusLabel.Text = "Scheduler disabled";
            StatusLabel.TextColor = Color.FromArgb("#888888");
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
        var friends = _settingsService.GetFriendsList();

        // Clear existing friend items (except NoFriendsLabel)
        var itemsToRemove = FriendsListContainer.Children
            .Where(c => c != NoFriendsLabel)
            .ToList();

        foreach (var item in itemsToRemove)
        {
            FriendsListContainer.Children.Remove(item);
        }

        NoFriendsLabel.IsVisible = friends.Count == 0;

        foreach (var friend in friends)
        {
            var friendView = CreateFriendView(friend);
            FriendsListContainer.Children.Add(friendView);
        }
    }

    private View CreateFriendView(FriendConfig friend)
    {
        var border = new Border
        {
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#2A2A2A")
                : Color.FromArgb("#FFFFFF"),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Stroke = Colors.Transparent,
            Padding = new Thickness(12)
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
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
            FontAttributes = FontAttributes.Bold
        });
        
        infoStack.Children.Add(new Label
        {
            Text = $"@{friend.Username}",
            FontSize = 12,
            TextColor = Color.FromArgb("#888888")
        });

        if (friend.LastMessageSent.HasValue)
        {
            var successIcon = friend.SuccessCount > 0 ? "✓" : "";
            infoStack.Children.Add(new Label
            {
                Text = $"{successIcon} Last: {friend.LastMessageSent.Value:MMM dd}",
                FontSize = 11,
                TextColor = Color.FromArgb("#4CAF50")
            });
        }

        grid.Children.Add(infoStack);

        var toggleSwitch = new Switch
        {
            IsToggled = friend.IsEnabled,
            VerticalOptions = LayoutOptions.Center
        };
        toggleSwitch.Toggled += (s, e) =>
        {
            friend.IsEnabled = e.Value;
            _settingsService.UpdateFriend(friend);
            UpdateStatus();
        };
        Grid.SetColumn(toggleSwitch, 1);
        grid.Children.Add(toggleSwitch);

        var deleteButton = new Button
        {
            Text = "🗑️",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#F44336"),
            FontSize = 18,
            Padding = new Thickness(8),
            HeightRequest = 44,
            WidthRequest = 44,
            VerticalOptions = LayoutOptions.Center
        };
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
        var statusIcon = run.Success ? "✓" : "✗";
        var statusColor = run.Success ? Color.FromArgb("#4CAF50") : Color.FromArgb("#F44336");

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
            infoStack.Children.Add(new Label
            {
                Text = $"{successCount}/{totalCount} messages sent",
                FontSize = 12,
                TextColor = Color.FromArgb("#888888")
            });
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
#if ANDROID
        if (e.Value)
        {
            // Enable scheduling
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            TiktokStreakSaver.Platforms.Android.StreakScheduler.ScheduleNextRun(context);
        }
        else
        {
            // Disable scheduling
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            TiktokStreakSaver.Platforms.Android.StreakScheduler.CancelSchedule(context);
        }
#endif
        UpdateStatus();
    }

    private void OnMessageChanged(object? sender, TextChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.NewTextValue))
        {
            _settingsService.SetMessageText(e.NewTextValue);
        }
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

    private async void OnPermissionsClicked(object? sender, EventArgs e)
    {
#if ANDROID
        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        
        var actions = new List<string>
        {
            "Request Battery Optimization Exemption",
            "Request Exact Alarm Permission",
            "Request Notification Permission"
        };

        var action = await DisplayActionSheet("Permissions", "Cancel", null, actions.ToArray());

        switch (action)
        {
            case "Request Battery Optimization Exemption":
                TiktokStreakSaver.Platforms.Android.StreakScheduler.RequestBatteryOptimizationExemption(context);
                break;
            case "Request Exact Alarm Permission":
                TiktokStreakSaver.Platforms.Android.StreakScheduler.RequestExactAlarmPermission(context);
                break;
            case "Request Notification Permission":
                await RequestNotificationPermission();
                break;
        }
#else
        await DisplayAlert("Info", "Permissions are only required on Android", "OK");
#endif
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

    private async void OnRunNowClicked(object? sender, EventArgs e)
    {
        var friends = _settingsService.GetEnabledFriends();
        if (friends.Count == 0)
        {
            await DisplayAlert("No Friends", "Please add at least one friend before running.", "OK");
            return;
        }

        var confirm = await DisplayAlert("Run Now", 
            $"This will send your streak message to {friends.Count} friend{(friends.Count != 1 ? "s" : "")}. Continue?", 
            "Run", "Cancel");

        if (!confirm) return;

#if ANDROID
        // Request notification permission first on Android 13+
        await RequestNotificationPermission();

        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        TiktokStreakSaver.Platforms.Android.StreakScheduler.RunNow(context);
        
        await DisplayAlert("Started", "Streak service started. Check the notification for progress.", "OK");
#else
        await DisplayAlert("Info", "This feature is only available on Android", "OK");
#endif
    }
}
