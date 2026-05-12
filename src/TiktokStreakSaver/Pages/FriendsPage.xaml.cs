using Microsoft.Maui.Controls.Shapes;
using TiktokStreakSaver.Models;
using TiktokStreakSaver.Services;

namespace TiktokStreakSaver.Pages;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public partial class FriendsPage : ContentPage
{
    private readonly SettingsService _settingsService;
    private bool _lastIsRunning = false;
    private IDispatcherTimer? _statusTimer;
    private bool _isGroupMode = false;

    public FriendsPage()
    {
        InitializeComponent();
        _settingsService = new SettingsService();
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
        LoadLists();

        if (_statusTimer == null)
        {
            _statusTimer = Dispatcher.CreateTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(1);
            _statusTimer.Tick += OnStatusTimerTick;
        }
        _statusTimer.Start();
        OnStatusTimerTick(null, EventArgs.Empty);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_statusTimer != null)
        {
            _statusTimer.Stop();
            _statusTimer.Tick -= OnStatusTimerTick;
            _statusTimer = null;
        }
    }

    private void OnStatusTimerTick(object? sender, EventArgs e)
    {
        bool isRunning = false;
#if ANDROID
        isRunning = TiktokStreakSaver.Platforms.Android.Services.StreakService.IsRunning;
#endif
        if (_lastIsRunning != isRunning)
        {
            _lastIsRunning = isRunning;
            LoadLists();

            SearchAndBulkRow.IsEnabled = !isRunning;
            SearchAndBulkRow.Opacity = isRunning ? 0.6 : 1.0;
            ActionButtonsGrid.IsEnabled = !isRunning;
            ActionButtonsGrid.Opacity = isRunning ? 0.6 : 1.0;

            GroupBulkRow.IsEnabled = !isRunning;
            GroupBulkRow.Opacity = isRunning ? 0.6 : 1.0;
            GroupActionButtonsGrid.IsEnabled = !isRunning;
            GroupActionButtonsGrid.Opacity = isRunning ? 0.6 : 1.0;

            if (AddFriendPanel.IsVisible && isRunning) AddFriendPanel.IsVisible = false;
            if (AddGroupPanel.IsVisible && isRunning) AddGroupPanel.IsVisible = false;
        }
    }

    private void OnRefreshing(object? sender, EventArgs e)
    {
        LoadLists();
        MainRefreshView.IsRefreshing = false;
    }

    private void LoadLists()
    {
        var allItems = _settingsService.GetFriendsList();
        var friendsAll = allItems.Where(f => !f.IsGroup).ToList();
        var groupsAll = allItems.Where(f => f.IsGroup).ToList();

        // ── Friends ──
        SearchAndBulkRow.IsVisible = friendsAll.Count > 0;
        var friendSearch = SearchFriendEntry.Text?.Trim() ?? string.Empty;
        var friendsToShow = string.IsNullOrEmpty(friendSearch)
            ? friendsAll
            : friendsAll.Where(f =>
                (f.Username != null && f.Username.Contains(friendSearch, StringComparison.OrdinalIgnoreCase)) ||
                (f.DisplayName != null && f.DisplayName.Contains(friendSearch, StringComparison.OrdinalIgnoreCase))
            ).ToList();

        var friendItemsToRemove = FriendsListContainer.Children.Where(c => c != NoFriendsLabel).ToList();
        foreach (var item in friendItemsToRemove) FriendsListContainer.Children.Remove(item);

        if (friendsAll.Count == 0)
        {
            NoFriendsLabel.Text = "No friends added. Tap 'Add' to begin.";
            NoFriendsLabel.IsVisible = true;
        }
        else if (friendsToShow.Count == 0 && !string.IsNullOrEmpty(friendSearch))
        {
            NoFriendsLabel.Text = $"No friends found matching '{friendSearch}'";
            NoFriendsLabel.IsVisible = true;
        }
        else
        {
            NoFriendsLabel.IsVisible = false;
        }
        foreach (var friend in friendsToShow) FriendsListContainer.Children.Add(CreateEntryView(friend));

        // ── Groups ──
        GroupBulkRow.IsVisible = groupsAll.Count > 0;
        var groupSearch = SearchGroupEntry.Text?.Trim() ?? string.Empty;
        var groupsToShow = string.IsNullOrEmpty(groupSearch)
            ? groupsAll
            : groupsAll.Where(g =>
                (g.DisplayName != null && g.DisplayName.Contains(groupSearch, StringComparison.OrdinalIgnoreCase))
            ).ToList();

        var groupItemsToRemove = GroupsListContainer.Children.Where(c => c != NoGroupsLabel).ToList();
        foreach (var item in groupItemsToRemove) GroupsListContainer.Children.Remove(item);

        if (groupsAll.Count == 0)
        {
            NoGroupsLabel.Text = "No groups added. Tap 'Add' to begin.";
            NoGroupsLabel.IsVisible = true;
        }
        else if (groupsToShow.Count == 0 && !string.IsNullOrEmpty(groupSearch))
        {
            NoGroupsLabel.Text = $"No groups found matching '{groupSearch}'";
            NoGroupsLabel.IsVisible = true;
        }
        else
        {
            NoGroupsLabel.IsVisible = false;
        }
        foreach (var group in groupsToShow) GroupsListContainer.Children.Add(CreateEntryView(group));

        UpdateStatsCard(allItems);
    }

    private void UpdateStatsCard(List<FriendConfig> allItems)
    {
        FriendsStatsCard.IsVisible = allItems.Count > 0;
        TotalFriendsLabel.Text = allItems.Count.ToString();
        EnabledFriendsLabel.Text = allItems.Count(f => f.IsEnabled).ToString();
        var today = DateTime.Now.Date;
        SentTodayLabel.Text = allItems.Count(f => f.LastMessageSent.HasValue && f.LastMessageSent.Value.Date == today).ToString();
    }

    private View CreateEntryView(FriendConfig friend)
    {
        var border = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = Colors.Transparent,
            Padding = new Thickness(14, 12),
            Opacity = 0,
            TranslationY = 10
        };
        border.SetAppThemeColor(Border.BackgroundColorProperty,
            GetThemeColor("Gray100", "#F3F4F6"),
            GetThemeColor("Gray900", "#111827"));
        _ = border.FadeTo(1, 300, Easing.CubicOut);
        _ = border.TranslateTo(0, 0, 300, Easing.CubicOut);

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

        var infoStack = new VerticalStackLayout { Spacing = 3 };
        var displayName = string.IsNullOrEmpty(friend.DisplayName) ? friend.Username : friend.DisplayName;
        infoStack.Children.Add(new Label { Text = displayName, FontSize = 15, FontFamily = "InterSemiBold" });
        var subtitleText = friend.IsGroup ? "Group" : $"@{friend.Username}";
        infoStack.Children.Add(new Label { Text = subtitleText, FontSize = 13, TextColor = GetThemeColor("Gray400", "#8B8F96") });
        if (friend.LastMessageSent.HasValue)
            infoStack.Children.Add(new Label { Text = $"Last sent: {friend.LastMessageSent.Value:MMM dd}", FontSize = 12, TextColor = GetThemeColor("Gray400", "#8B8F96") });
        grid.Children.Add(infoStack);

        var editButton = new Button { Text = "Edit", BackgroundColor = Colors.Transparent, FontSize = 12, Padding = new Thickness(8), HeightRequest = 44, VerticalOptions = LayoutOptions.Center, IsEnabled = !_lastIsRunning, Opacity = _lastIsRunning ? 0.6 : 1.0 };
        editButton.SetAppThemeColor(Button.TextColorProperty, GetThemeColor("Gray400"), GetThemeColor("Gray400"));
        editButton.Clicked += async (s, e) =>
        {
            var promptTitle = friend.IsGroup ? "Edit Group" : "Edit Friend";
            var promptMessage = friend.IsGroup
                ? "Enter the group name (must match the TikTok chat header):"
                : "Enter new display name:";
            var newName = await DisplayPromptAsync(promptTitle, promptMessage, initialValue: friend.DisplayName ?? friend.Username);
            if (newName != null) { friend.DisplayName = newName; _settingsService.UpdateFriend(friend); LoadLists(); }
        };
        Grid.SetColumn(editButton, 1); grid.Children.Add(editButton);

        var deleteButton = new Button { Text = "Delete", BackgroundColor = Colors.Transparent, FontSize = 12, Padding = new Thickness(8), HeightRequest = 44, VerticalOptions = LayoutOptions.Center, IsEnabled = !_lastIsRunning, Opacity = _lastIsRunning ? 0.6 : 1.0 };
        deleteButton.TextColor = GetThemeColor("DeleteColor", "#EE1D52");
        deleteButton.Clicked += async (s, e) =>
        {
            var noun = friend.IsGroup ? "group" : "friend";
            var confirm = await DisplayAlert($"Remove {noun}", $"Remove {displayName} from the list?", "Remove", "Cancel");
            if (confirm) { _settingsService.RemoveFriend(friend.Id); LoadLists(); }
        };
        Grid.SetColumn(deleteButton, 2); grid.Children.Add(deleteButton);

        var toggleSwitch = new Switch { IsToggled = friend.IsEnabled, VerticalOptions = LayoutOptions.Center, IsEnabled = !_lastIsRunning, Opacity = _lastIsRunning ? 0.6 : 1.0 };
        toggleSwitch.SetAppThemeColor(Switch.ThumbColorProperty, GetThemeColor("White"), GetThemeColor("White"));
        toggleSwitch.SetAppThemeColor(Switch.OnColorProperty, GetThemeColor("Primary", "#FE2C55"), GetThemeColor("Primary", "#FE2C55"));
        toggleSwitch.Toggled += (s, e) => { friend.IsEnabled = e.Value; _settingsService.UpdateFriend(friend); };
        Grid.SetColumn(toggleSwitch, 3); grid.Children.Add(toggleSwitch);

        border.Content = grid;
        return border;
    }

    // ─── Mode switching ──────────────────────────────────────────────────────

    private void OnFriendsModeTapped(object? sender, TappedEventArgs e)
    {
        if (!_isGroupMode) return;
        _isGroupMode = false;

        FriendsModeTabBorder.BackgroundColor = GetThemeColor("Primary", "#FE2C55");
        FriendsModeTabLabel.TextColor = GetThemeColor("White", "#FFFFFF");
        GroupsModeTabBorder.BackgroundColor = Colors.Transparent;
        GroupsModeTabLabel.TextColor = GetThemeColor("Gray600", "#5F636A");

        FriendsModeContainer.IsVisible = true;
        GroupsModeContainer.IsVisible = false;
    }

    private void OnGroupsModeTapped(object? sender, TappedEventArgs e)
    {
        if (_isGroupMode) return;
        _isGroupMode = true;

        GroupsModeTabBorder.BackgroundColor = GetThemeColor("Primary", "#FE2C55");
        GroupsModeTabLabel.TextColor = GetThemeColor("White", "#FFFFFF");
        FriendsModeTabBorder.BackgroundColor = Colors.Transparent;
        FriendsModeTabLabel.TextColor = GetThemeColor("Gray600", "#5F636A");

        FriendsModeContainer.IsVisible = false;
        GroupsModeContainer.IsVisible = true;
    }

    // ─── Friends actions ─────────────────────────────────────────────────────

    private void OnSearchFriendTextChanged(object? sender, TextChangedEventArgs e) => LoadLists();

    private void OnAddFriendClicked(object? sender, EventArgs e)
    {
        AddFriendPanel.IsVisible = true;
        NewFriendUsernameEntry.Text = string.Empty;
        NewFriendDisplayNameEntry.Text = string.Empty;
        NewFriendUsernameEntry.Focus();
    }

    private void OnCancelAddFriend(object? sender, EventArgs e) => AddFriendPanel.IsVisible = false;

    private async void OnSaveFriend(object? sender, EventArgs e)
    {
        var username = NewFriendUsernameEntry.Text?.Trim().TrimStart('@');
        var displayName = NewFriendDisplayNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(username)) { await DisplayAlert("Error", "Please enter a username", "OK"); return; }
        var existing = _settingsService.GetFriendsList();
        if (existing.Any(f => !f.IsGroup && f.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
        { await DisplayAlert("Error", "This friend is already in your list", "OK"); return; }
        var friend = new FriendConfig { Username = username, DisplayName = displayName ?? string.Empty, IsEnabled = true, IsGroup = false };
        _settingsService.AddFriend(friend);
        AddFriendPanel.IsVisible = false;
        LoadLists();
    }

    private void OnEnableAllClicked(object? sender, EventArgs e) => SetEnabledForAll(group: false, enabled: true);
    private void OnDisableAllClicked(object? sender, EventArgs e) => SetEnabledForAll(group: false, enabled: false);

    private void SetEnabledForAll(bool group, bool enabled)
    {
        var all = _settingsService.GetFriendsList();
        var subset = all.Where(f => f.IsGroup == group).ToList();
        if (subset.Count == 0) return;
        foreach (var f in subset) f.IsEnabled = enabled;
        _settingsService.SaveFriendsList(all);
        LoadLists();
    }

    private async void OnDeleteAllFriendsClicked(object? sender, EventArgs e)
    {
        var all = _settingsService.GetFriendsList();
        var groupsOnly = all.Where(f => f.IsGroup).ToList();
        if (all.Count == groupsOnly.Count) return;
        bool confirm = await DisplayAlert("Clear Friends",
            "Remove all direct messages? Group chats will not be affected.", "Clear", "Cancel");
        if (confirm)
        {
            _settingsService.SaveFriendsList(groupsOnly);
            LoadLists();
        }
    }

    // ─── Groups actions ──────────────────────────────────────────────────────

    private void OnSearchGroupTextChanged(object? sender, TextChangedEventArgs e) => LoadLists();

    private void OnAddGroupClicked(object? sender, EventArgs e)
    {
        AddGroupPanel.IsVisible = true;
        NewGroupNameEntry.Text = string.Empty;
        NewGroupDisplayNameEntry.Text = string.Empty;
        NewGroupNameEntry.Focus();
    }

    private void OnCancelAddGroup(object? sender, EventArgs e) => AddGroupPanel.IsVisible = false;

    private async void OnSaveGroup(object? sender, EventArgs e)
    {
        var matchName = NewGroupNameEntry.Text?.Trim();
        var displayName = NewGroupDisplayNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(matchName)) { await DisplayAlert("Error", "Please enter a group chat name", "OK"); return; }

        var existing = _settingsService.GetFriendsList();
        if (existing.Any(f => f.IsGroup && f.DisplayName.Equals(matchName, StringComparison.OrdinalIgnoreCase)))
        { await DisplayAlert("Error", "This group is already in your list", "OK"); return; }

        // For groups, DisplayName doubles as the matching key against the TikTok chat header.
        var group = new FriendConfig
        {
            Username = string.Empty,
            DisplayName = string.IsNullOrEmpty(displayName) ? matchName : matchName,
            IsGroup = true,
            IsEnabled = true
        };
        _settingsService.AddFriend(group);
        AddGroupPanel.IsVisible = false;
        LoadLists();
    }

    private void OnEnableAllGroupsClicked(object? sender, EventArgs e) => SetEnabledForAll(group: true, enabled: true);
    private void OnDisableAllGroupsClicked(object? sender, EventArgs e) => SetEnabledForAll(group: true, enabled: false);

    private async void OnDeleteAllGroupsClicked(object? sender, EventArgs e)
    {
        var all = _settingsService.GetFriendsList();
        var friendsOnly = all.Where(f => !f.IsGroup).ToList();
        if (all.Count == friendsOnly.Count) return;
        bool confirm = await DisplayAlert("Clear Groups",
            "Remove all group chats? Direct messages will not be affected.", "Clear", "Cancel");
        if (confirm)
        {
            _settingsService.SaveFriendsList(friendsOnly);
            LoadLists();
        }
    }

    // ─── Import / Export (friend list — both groups & friends are persisted together) ──

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
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.macOS, new[] { "json" } },
                })
            });
            if (result == null) return;
            string json;
            using (var stream = await result.OpenReadAsync())
            using (var reader = new System.IO.StreamReader(stream))
                json = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(json)) { await DisplayAlert("Import Failed", "The selected file is empty.", "OK"); return; }

            List<FriendConfig>? imported;
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                imported = System.Text.Json.JsonSerializer.Deserialize<List<FriendConfig>>(json, options);
            }
            catch { await DisplayAlert("Import Failed", "The file is not a valid friend list.", "OK"); return; }
            if (imported == null || imported.Count == 0) { await DisplayAlert("Import", "The file contains no entries.", "OK"); return; }

            var existing = _settingsService.GetFriendsList();
            int added = 0, updated = 0, skipped = 0;
            var seenFriends = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in imported)
            {
                if (entry.IsGroup)
                {
                    var name = (entry.DisplayName ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(name)) { skipped++; continue; }
                    entry.DisplayName = name;
                    entry.Username = string.Empty;
                    if (!seenGroups.Add(name)) { skipped++; continue; }
                    var match = existing.FirstOrDefault(f => f.IsGroup && f.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (match != null) { entry.Id = match.Id; existing[existing.IndexOf(match)] = entry; updated++; }
                    else { if (string.IsNullOrEmpty(entry.Id)) entry.Id = Guid.NewGuid().ToString(); existing.Add(entry); added++; }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(entry.Username) || entry.Username.Trim().TrimStart('@').Length < 2) { skipped++; continue; }
                    entry.Username = entry.Username.Trim().TrimStart('@');
                    entry.DisplayName = entry.DisplayName?.Trim() ?? string.Empty;
                    if (!seenFriends.Add(entry.Username)) { skipped++; continue; }
                    var match = existing.FirstOrDefault(f => !f.IsGroup && f.Username.Equals(entry.Username, StringComparison.OrdinalIgnoreCase));
                    if (match != null) { entry.Id = match.Id; existing[existing.IndexOf(match)] = entry; updated++; }
                    else { if (string.IsNullOrEmpty(entry.Id)) entry.Id = Guid.NewGuid().ToString(); existing.Add(entry); added++; }
                }
            }
            _settingsService.SaveFriendsList(existing);
            LoadLists();
            await DisplayAlert("Import Complete", $"Import complete.\n\nAdded: {added}\nUpdated: {updated}\nSkipped: {skipped}", "OK");
        }
        catch (Exception ex) { await DisplayAlert("Import Failed", $"Unexpected error: {ex.Message}", "OK"); }
    }

    private async void OnExportFriendsClicked(object? sender, EventArgs e)
    {
        try
        {
            var entries = _settingsService.GetFriendsList();
            if (entries.Count == 0) { await DisplayAlert("Export", "Your list is empty.", "OK"); return; }
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(entries, options);
            var fileName = $"streak_friends_{DateTime.Now:yyyyMMdd_HHmm}.json";
            var filePath = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
            await System.IO.File.WriteAllTextAsync(filePath, json);
            await Share.Default.RequestAsync(new ShareFileRequest { Title = "Export Friend List", File = new ShareFile(filePath, "application/json") });
        }
        catch (Exception ex) { await DisplayAlert("Export Failed", $"Could not export: {ex.Message}", "OK"); }
    }
}
