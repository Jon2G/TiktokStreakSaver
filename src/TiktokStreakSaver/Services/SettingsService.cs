using System.Text.Json;
using TiktokStreakSaver.Models;

namespace TiktokStreakSaver.Services;

/// <summary>
/// Service for managing app settings and persistent storage
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public class SettingsService
{
    private const string FriendsListKey = "friends_list";
    private const string MessageTextKey = "message_text";
    private const string LastRunKey = "last_run";
    private const string IsScheduledKey = "is_scheduled";
    private const string RunHistoryKey = "run_history";
    private const string IntervalHoursKey = "interval_hours";
    private const string IntervalMinutesKey = "interval_minutes";
    private const string SkipUnreachableUsersKey = "skip_unreachable_users";
    private const string BurstMessageTextKey = "burst_message_text"; // Legacy single line
    private const string BurstMessagesKey = "burst_messages_list";
    private const string BurstLastRunKey = "burst_last_run";
    private const string IsBurstModeActiveKey = "is_burst_mode_active";
    private const string BurstTargetUsernameKey = "burst_target_username";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Default message to send
    /// </summary>
    public const string DefaultMessage = "Hey! Keeping our streak alive! \uD83D\uDD25";

    /// <summary>
    /// Default interval in hours (legacy; <see cref="DefaultIntervalMinutes"/> is authoritative).
    /// </summary>
    public const int DefaultIntervalHours = 23;

    /// <summary>
    /// Default time between automatic streak runs (23 hours).
    /// </summary>
    public const int DefaultIntervalMinutes = DefaultIntervalHours * 60;

    /// <summary>Minimum gap between scheduled runs (15 minutes).</summary>
    public const int MinIntervalMinutes = 15;

    /// <summary>Maximum gap between scheduled runs (23h 59m — strictly under 24 hours).</summary>
    public const int MaxIntervalMinutes = 24 * 60 - 1;

    #region Friends List

    public List<FriendConfig> GetFriendsList()
    {
        try
        {
            var json = Preferences.Get(FriendsListKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return new List<FriendConfig>();

            return JsonSerializer.Deserialize<List<FriendConfig>>(json, JsonOptions) ?? new List<FriendConfig>();
        }
        catch
        {
            return new List<FriendConfig>();
        }
    }

    public void SaveFriendsList(List<FriendConfig> friends)
    {
        var json = JsonSerializer.Serialize(friends, JsonOptions);
        Preferences.Set(FriendsListKey, json);
    }

    public void AddFriend(FriendConfig friend)
    {
        var friends = GetFriendsList();
        friends.Add(friend);
        SaveFriendsList(friends);
    }

    public void RemoveFriend(string friendId)
    {
        var friends = GetFriendsList();
        friends.RemoveAll(f => f.Id == friendId);
        SaveFriendsList(friends);
    }

    public void UpdateFriend(FriendConfig friend)
    {
        var friends = GetFriendsList();
        var index = friends.FindIndex(f => f.Id == friend.Id);
        if (index >= 0)
        {
            friends[index] = friend;
            SaveFriendsList(friends);
        }
    }

    public List<FriendConfig> GetEnabledFriends()
    {
        return GetFriendsList().Where(f => f.IsEnabled).ToList();
    }

    #endregion

    #region Message Configuration

    public string GetMessageText()
    {
        return Preferences.Get(MessageTextKey, DefaultMessage);
    }

    public void SetMessageText(string message)
    {
        Preferences.Set(MessageTextKey, message);
    }

    public string GetBurstMessageText()
    {
        return Preferences.Get(BurstMessageTextKey, "Burst Message");
    }

    public void SetBurstMessageText(string message)
    {
        Preferences.Set(BurstMessageTextKey, message);
    }

    /// <summary>Burst templates; migrates from legacy <see cref="BurstMessageTextKey"/> when empty.</summary>
    public List<string> GetBurstMessages()
    {
        try
        {
            var json = Preferences.Get(BurstMessagesKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                var legacy = Preferences.Get(BurstMessageTextKey, string.Empty);
                if (!string.IsNullOrEmpty(legacy))
                    return new List<string> { legacy };
                return new List<string> { "Burst Message" };
            }

            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string> { "Burst Message" };
        }
        catch
        {
            return new List<string> { "Burst Message" };
        }
    }

    public void SetBurstMessages(List<string> messages)
    {
        if (messages == null || messages.Count == 0)
            messages = new List<string> { "Burst Message" };
        Preferences.Set(BurstMessagesKey, JsonSerializer.Serialize(messages, JsonOptions));
    }

    public bool IsBurstModeActive()
    {
        return Preferences.Get(IsBurstModeActiveKey, false);
    }

    public void SetBurstModeActive(bool active)
    {
        Preferences.Set(IsBurstModeActiveKey, active);
    }

    public string GetBurstTargetUsername()
    {
        return Preferences.Get(BurstTargetUsernameKey, string.Empty);
    }

    public void SetBurstTargetUsername(string username)
    {
        Preferences.Set(BurstTargetUsernameKey, username ?? string.Empty);
    }

    #endregion

    #region Scheduling

    /// <summary>
    /// Interval between automatic runs, in minutes. Migrates legacy <c>interval_hours</c> on first read.
    /// </summary>
    public int GetIntervalMinutes()
    {
        if (Preferences.ContainsKey(IntervalMinutesKey))
        {
            var v = Preferences.Get(IntervalMinutesKey, DefaultIntervalMinutes);
            return Math.Clamp(v, MinIntervalMinutes, MaxIntervalMinutes);
        }

        var legacyHours = Preferences.Get(IntervalHoursKey, DefaultIntervalHours);
        var migrated = Math.Clamp(legacyHours * 60, MinIntervalMinutes, MaxIntervalMinutes);
        SetIntervalMinutes(migrated);
        return migrated;
    }

    public void SetIntervalMinutes(int minutes)
    {
        var v = Math.Clamp(minutes, MinIntervalMinutes, MaxIntervalMinutes);
        Preferences.Set(IntervalMinutesKey, v);
    }

    public DateTime? GetLastRunTime()
    {
        var ticks = Preferences.Get(LastRunKey, 0L);
        return ticks > 0 ? new DateTime(ticks) : null;
    }

    public void SetLastRunTime(DateTime time)
    {
        Preferences.Set(LastRunKey, time.Ticks);
    }

    public DateTime? GetBurstLastRunTime()
    {
        var ticks = Preferences.Get(BurstLastRunKey, 0L);
        return ticks > 0 ? new DateTime(ticks) : null;
    }

    public void SetBurstLastRunTime(DateTime time)
    {
        Preferences.Set(BurstLastRunKey, time.Ticks);
    }

    public bool IsScheduled()
    {
        return Preferences.Get(IsScheduledKey, true);
    }

    public void SetScheduled(bool scheduled)
    {
        Preferences.Set(IsScheduledKey, scheduled);
    }

    public bool GetSkipUnreachableUsers()
    {
        return Preferences.Get(SkipUnreachableUsersKey, true);
    }

    public void SetSkipUnreachableUsers(bool skip)
    {
        Preferences.Set(SkipUnreachableUsersKey, skip);
    }

    public DateTime GetNextRunTime()
    {
        var lastRun = GetLastRunTime();
        var intervalMinutes = GetIntervalMinutes();

        if (lastRun.HasValue)
            return lastRun.Value.AddMinutes(intervalMinutes);

        return DateTime.Now;
    }

    #endregion

    #region Run History

    public List<StreakRunResult> GetRunHistory()
    {
        try
        {
            var json = Preferences.Get(RunHistoryKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return new List<StreakRunResult>();

            return JsonSerializer.Deserialize<List<StreakRunResult>>(json, JsonOptions) ?? new List<StreakRunResult>();
        }
        catch
        {
            return new List<StreakRunResult>();
        }
    }

    public void AddRunResult(StreakRunResult result)
    {
        var history = GetRunHistory();
        history.Insert(0, result);

        if (history.Count > 50)
            history = history.Take(50).ToList();

        var json = JsonSerializer.Serialize(history, JsonOptions);
        Preferences.Set(RunHistoryKey, json);
    }

    #endregion

    #region Clear Data

    public void ClearRunHistory()
    {
        Preferences.Remove(RunHistoryKey);
    }

    public void ClearAll()
    {
        Preferences.Clear();
    }

    #endregion
}
