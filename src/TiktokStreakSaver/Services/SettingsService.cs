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
    private const string UseFixedTimeKey = "use_fixed_time";
    private const string FixedTimeHourKey = "fixed_time_hour";
    private const string FixedTimeMinuteKey = "fixed_time_minute";

    // ── Resilience: retry tracking + event-driven recovery flags ──
    private const string RetryCountTodayKey = "retry_count_today";
    private const string RetryCountDateKey = "retry_count_date";
    private const string LastRunFailedKey = "last_run_failed";
    private const string LastRunFailureReasonKey = "last_run_failure_reason";
    private const string BatteryAnticipationDateKey = "battery_anticipation_date";
    private const string SendOnBatteryLowKey = "send_on_battery_low";

    /// <summary>
    /// Maximum hourly retries per calendar day, in addition to the original scheduled run.
    /// </summary>
    public const int MaxRetriesPerDay = 3;

    /// <summary>Failure-reason value: the run was skipped because there was no Wi‑Fi/cellular.</summary>
    public const string FailureReasonNoNetwork = "no_network";

    /// <summary>Failure-reason value: the run completed but one or more messages failed to send.</summary>
    public const string FailureReasonSendError = "send_error";

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
        if (GetUseFixedTime())
        {
            var now = DateTime.Now;
            var today = now.Date.AddHours(GetFixedTimeHour()).AddMinutes(GetFixedTimeMinute());
            return today > now ? today : today.AddDays(1);
        }

        var lastRun = GetLastRunTime();
        var intervalHours = GetIntervalHours();

        if (lastRun.HasValue)
            return lastRun.Value.AddHours(intervalHours);

        return DateTime.Now;
    }

    /// <summary>
    /// When true, scheduling uses a fixed daily clock time (Hour:Minute) instead of
    /// the rolling interval based on last run.
    /// </summary>
    public bool GetUseFixedTime() => Preferences.Get(UseFixedTimeKey, false);

    public void SetUseFixedTime(bool value) => Preferences.Set(UseFixedTimeKey, value);

    /// <summary>
    /// Hour of day (0-23) for fixed daily schedule.
    /// </summary>
    public int GetFixedTimeHour() => Preferences.Get(FixedTimeHourKey, DateTime.Now.Hour);

    public void SetFixedTimeHour(int hour) => Preferences.Set(FixedTimeHourKey, Math.Clamp(hour, 0, 23));

    /// <summary>
    /// Minute (0-59) for fixed daily schedule.
    /// </summary>
    public int GetFixedTimeMinute() => Preferences.Get(FixedTimeMinuteKey, DateTime.Now.Minute);

    public void SetFixedTimeMinute(int minute) => Preferences.Set(FixedTimeMinuteKey, Math.Clamp(minute, 0, 59));

    #endregion

    #region Resilience: retry counter, failure flag, battery toggle

    /// <summary>
    /// Current count of hourly retries used today. Auto-resets when the stored date
    /// stamp is not today's date (so the counter naturally rolls over at midnight).
    /// </summary>
    public int GetTodayRetryCount()
    {
        var storedDateTicks = Preferences.Get(RetryCountDateKey, 0L);
        var today = DateTime.Now.Date.Ticks;
        if (storedDateTicks != today)
            return 0;
        return Preferences.Get(RetryCountTodayKey, 0);
    }

    /// <summary>
    /// Increments today's retry counter (rolling the date stamp if it's a new day)
    /// and returns the new value.
    /// </summary>
    public int IncrementTodayRetryCount()
    {
        var today = DateTime.Now.Date.Ticks;
        var storedDateTicks = Preferences.Get(RetryCountDateKey, 0L);
        var current = storedDateTicks == today ? Preferences.Get(RetryCountTodayKey, 0) : 0;
        var next = current + 1;
        Preferences.Set(RetryCountDateKey, today);
        Preferences.Set(RetryCountTodayKey, next);
        return next;
    }

    /// <summary>Reset today's retry counter (typically on a fully successful run).</summary>
    public void ResetTodayRetryCount()
    {
        Preferences.Set(RetryCountDateKey, DateTime.Now.Date.Ticks);
        Preferences.Set(RetryCountTodayKey, 0);
    }

    /// <summary>
    /// Flag indicating that the last attempted run failed and a recovery (e.g. on
    /// network restore) may be worth attempting.
    /// </summary>
    public bool GetLastRunFailed() => Preferences.Get(LastRunFailedKey, false);

    /// <summary>Set / clear the last-run-failed flag, optionally tagging the reason.</summary>
    public void SetLastRunFailed(bool failed, string? reason)
    {
        Preferences.Set(LastRunFailedKey, failed);
        if (failed && !string.IsNullOrEmpty(reason))
            Preferences.Set(LastRunFailureReasonKey, reason);
        else
            Preferences.Remove(LastRunFailureReasonKey);
    }

    /// <summary>Reason tag set the last time the run failed, or null when cleared.</summary>
    public string? GetLastRunFailureReason()
    {
        var v = Preferences.Get(LastRunFailureReasonKey, string.Empty);
        return string.IsNullOrEmpty(v) ? null : v;
    }

    /// <summary>
    /// True if the battery-low anticipation already fired today (we only fire it once
    /// per calendar day to avoid loops if the device keeps oscillating around 15%).
    /// </summary>
    public bool WasBatteryAnticipationUsedToday()
    {
        var storedTicks = Preferences.Get(BatteryAnticipationDateKey, 0L);
        return storedTicks == DateTime.Now.Date.Ticks;
    }

    /// <summary>Mark today as already used for battery-low anticipation.</summary>
    public void MarkBatteryAnticipationUsedToday()
    {
        Preferences.Set(BatteryAnticipationDateKey, DateTime.Now.Date.Ticks);
    }

    /// <summary>
    /// User toggle: when ON, the app will start the streak run early if Android fires
    /// the system battery-low broadcast and today's streak hasn't been sent yet.
    /// Default ON.
    /// </summary>
    public bool GetSendOnBatteryLow() => Preferences.Get(SendOnBatteryLowKey, true);

    public void SetSendOnBatteryLow(bool value) => Preferences.Set(SendOnBatteryLowKey, value);

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
