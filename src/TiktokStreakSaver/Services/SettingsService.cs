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
    private const string SkipUnreachableUsersKey = "skip_unreachable_users";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Default message to send (kept from original; do NOT change to "Streak").
    /// </summary>
    public const string DefaultMessage = "Hey! Keeping our streak alive! \uD83D\uDD25";

    /// <summary>
    /// Default interval in hours.
    /// </summary>
    public const int DefaultIntervalHours = 23;

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

    // ── Randomized Normal Messages ──

    private const string RandomizeNormalMessagesKey = "randomize_normal_messages";

    /// <summary>
    /// 50 built-in short streak messages for randomized normal-mode sends.
    /// </summary>
    public static readonly List<string> BuiltInStreakMessages = new()
    {
        "Streak",
        "streak",
        "streakk",
        "streaak",
        "streaakkk",
        "streaaak",
        "strk",
        "Strk",
        "s",
        "S",
        "streaks",
        "Streaks",
        "streakss",
        "streak lol",
        "yo streak",
        "yoo streak",
        "yoo streakk",
        "hey streak",
        "hii streak",
        "hi streak",
        "heyy streak",
        "streak hii",
        "streak hi",
        "streak yo",
        "streak yoo",
        "streakkk",
        "strek",
        "streek",
        "streeek",
        "streeeek",
        "yo",
        "yoo",
        "yooo",
        "hey",
        "hii",
        "heyy",
        "heyyy",
        "here",
        "heree",
        "strkeee",
        "streak rn",
        "quick streak",
        "streakk lol",
        "streak lmao",
        "lol streak",
        "streaaakk",
        "streakkk lol",
        "daily streak",
        "streak streak",
        "ayo streak"
    };

    /// <summary>
    /// Get whether randomized built-in messages are enabled for normal mode
    /// </summary>
    public bool GetRandomizeNormalMessages()
    {
        return Preferences.Get(RandomizeNormalMessagesKey, false);
    }

    /// <summary>
    /// Set whether randomized built-in messages are enabled for normal mode
    /// </summary>
    public void SetRandomizeNormalMessages(bool enabled)
    {
        Preferences.Set(RandomizeNormalMessagesKey, enabled);
    }

    #endregion

    #region Scheduling

    /// <summary>
    /// Interval between automatic runs, in hours (clamped to 1..23).
    /// </summary>
    public int GetIntervalHours()
    {
        var v = Preferences.Get(IntervalHoursKey, DefaultIntervalHours);
        return Math.Clamp(v, 1, 23);
    }

    /// <summary>
    /// Set the interval in hours (clamped to 1..23).
    /// </summary>
    public void SetIntervalHours(int hours)
    {
        Preferences.Set(IntervalHoursKey, Math.Clamp(hours, 1, 23));
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

    /// <summary>
    /// Default off (matches fork). Users opt in via the Profile toggle.
    /// </summary>
    public bool IsScheduled()
    {
        return Preferences.Get(IsScheduledKey, false);
    }

    public void SetScheduled(bool scheduled)
    {
        Preferences.Set(IsScheduledKey, scheduled);
    }

    /// <summary>
    /// Default ON (kept from original): a missing friend should not abort the whole run.
    /// </summary>
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
        var intervalHours = GetIntervalHours();

        if (lastRun.HasValue)
            return lastRun.Value.AddHours(intervalHours);

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
