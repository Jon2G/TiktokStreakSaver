using System.Text.Json;
using TiktokStreakSaver.Models;

namespace TiktokStreakSaver.Services;

/// <summary>
/// Service for managing app settings and persistent storage
/// </summary>
public class SettingsService
{
    private const string FriendsListKey = "friends_list";
    private const string MessageTextKey = "message_text";
    private const string LastRunKey = "last_run";
    private const string IsScheduledKey = "is_scheduled";
    private const string RunHistoryKey = "run_history";
    private const string IntervalHoursKey = "interval_hours";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Default message to send
    /// </summary>
    public const string DefaultMessage = "Hey! Keeping our streak alive! 🔥";

    /// <summary>
    /// Default interval in hours
    /// </summary>
    public const int DefaultIntervalHours = 23;

    #region Friends List

    /// <summary>
    /// Get the list of configured friends
    /// </summary>
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

    /// <summary>
    /// Save the friends list
    /// </summary>
    public void SaveFriendsList(List<FriendConfig> friends)
    {
        var json = JsonSerializer.Serialize(friends, JsonOptions);
        Preferences.Set(FriendsListKey, json);
    }

    /// <summary>
    /// Add a new friend to the list
    /// </summary>
    public void AddFriend(FriendConfig friend)
    {
        var friends = GetFriendsList();
        friends.Add(friend);
        SaveFriendsList(friends);
    }

    /// <summary>
    /// Remove a friend from the list
    /// </summary>
    public void RemoveFriend(string friendId)
    {
        var friends = GetFriendsList();
        friends.RemoveAll(f => f.Id == friendId);
        SaveFriendsList(friends);
    }

    /// <summary>
    /// Update a friend's configuration
    /// </summary>
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

    /// <summary>
    /// Get enabled friends only
    /// </summary>
    public List<FriendConfig> GetEnabledFriends()
    {
        return GetFriendsList().Where(f => f.IsEnabled).ToList();
    }

    #endregion

    #region Message Configuration

    /// <summary>
    /// Get the message text to send
    /// </summary>
    public string GetMessageText()
    {
        return Preferences.Get(MessageTextKey, DefaultMessage);
    }

    /// <summary>
    /// Set the message text to send
    /// </summary>
    public void SetMessageText(string message)
    {
        Preferences.Set(MessageTextKey, message);
    }

    #endregion

    #region Scheduling

    /// <summary>
    /// Get the interval in hours
    /// </summary>
    public int GetIntervalHours()
    {
        return Preferences.Get(IntervalHoursKey, DefaultIntervalHours);
    }

    /// <summary>
    /// Set the interval in hours
    /// </summary>
    public void SetIntervalHours(int hours)
    {
        Preferences.Set(IntervalHoursKey, hours);
    }

    /// <summary>
    /// Get the last run timestamp
    /// </summary>
    public DateTime? GetLastRunTime()
    {
        var ticks = Preferences.Get(LastRunKey, 0L);
        return ticks > 0 ? new DateTime(ticks) : null;
    }

    /// <summary>
    /// Set the last run timestamp
    /// </summary>
    public void SetLastRunTime(DateTime time)
    {
        Preferences.Set(LastRunKey, time.Ticks);
    }

    /// <summary>
    /// Get whether the scheduler is enabled
    /// </summary>
    public bool IsScheduled()
    {
        return Preferences.Get(IsScheduledKey, false);
    }

    /// <summary>
    /// Set whether the scheduler is enabled
    /// </summary>
    public void SetScheduled(bool scheduled)
    {
        Preferences.Set(IsScheduledKey, scheduled);
    }

    /// <summary>
    /// Calculate the next run time based on last run and interval
    /// </summary>
    public DateTime GetNextRunTime()
    {
        var lastRun = GetLastRunTime();
        var intervalHours = GetIntervalHours();

        if (lastRun.HasValue)
        {
            return lastRun.Value.AddHours(intervalHours);
        }

        // If never run, schedule for now
        return DateTime.Now;
    }

    #endregion

    #region Run History

    /// <summary>
    /// Get the run history (last 50 runs)
    /// </summary>
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

    /// <summary>
    /// Add a run result to history
    /// </summary>
    public void AddRunResult(StreakRunResult result)
    {
        var history = GetRunHistory();
        history.Insert(0, result);

        // Keep only last 50 runs
        if (history.Count > 50)
        {
            history = history.Take(50).ToList();
        }

        var json = JsonSerializer.Serialize(history, JsonOptions);
        Preferences.Set(RunHistoryKey, json);
    }

    #endregion

    #region Clear Data

    /// <summary>
    /// Clear all settings
    /// </summary>
    public void ClearAll()
    {
        Preferences.Clear();
    }

    #endregion
}



