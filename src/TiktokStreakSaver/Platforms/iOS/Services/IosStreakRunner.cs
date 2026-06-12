using TiktokStreakSaver;
using TiktokStreakSaver.Models;
using TiktokStreakSaver.Services;
using TiktokStreakSaver.Services.Storage;

namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>
/// Invokes the native StreakEngine runner (App Intent logic) from the MAUI app.
/// </summary>
public static class IosStreakRunner
{
    private static bool _isRunning;
    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(120);

    public static bool IsRunning => _isRunning;

    /// <param name="manual">When true, sends to all enabled friends (Run Now button).</param>
    public static async Task<IosRunResult> RunNowAsync(bool manual = false)
    {
        if (_isRunning)
            return new IosRunResult(IosRunStatus.AlreadyRunning, "A streak run is already in progress.", 0, 0);

        _isRunning = true;
        var settings = new SettingsService();
        ClearLastFailureReason();

        if (manual)
            IosManualRunFlags.SetForceManualRun(true);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            StreakEngineBridge.RunStreak(success =>
            {
                _isRunning = false;
                IosManualRunFlags.SetForceManualRun(false);
                tcs.TrySetResult(success);
            });
        }
        catch (DllNotFoundException)
        {
            _isRunning = false;
            IosManualRunFlags.SetForceManualRun(false);
            return new IosRunResult(
                IosRunStatus.EngineMissing,
                "Native streak engine is not available on this build.",
                0,
                0);
        }

        using var timeoutCts = new CancellationTokenSource(RunTimeout);
        await using var _ = timeoutCts.Token.Register(() =>
        {
            if (tcs.TrySetResult(false))
                AppStorageProvider.Current.SetString(AppConstants.LastRunFailureReasonKey, "timed_out");
        });

        bool success = await tcs.Task;

        var result = MapResult(settings, success);
        SessionState.NotifyChanged();
        return result;
    }

    private static IosRunResult MapResult(SettingsService settings, bool success)
    {
        var reason = AppStorageProvider.Current.GetString(AppConstants.LastRunFailureReasonKey, string.Empty);
        var (sent, total) = CountLatestRun(settings.GetRunHistory());

        if (success)
        {
            ClearLastFailureReason();
            if (sent == 0 && total == 0 && reason == "no_friends_due")
            {
                return new IosRunResult(
                    IosRunStatus.NoFriendsDue,
                    "No friends are due for a streak message today.",
                    0,
                    0);
            }

            var message = sent > 0
                ? $"Sent {sent} of {total} message{(total != 1 ? "s" : "")}."
                : "Run finished.";
            return new IosRunResult(IosRunStatus.Completed, message, sent, total);
        }

        return reason switch
        {
            "no_network" => new IosRunResult(IosRunStatus.Offline, "No network connection.", sent, total),
            "no_cookies" => new IosRunResult(
                IosRunStatus.NoCookies,
                "TikTok session cookies are missing. Sign in again.",
                sent,
                total),
            "login_required" => new IosRunResult(
                IosRunStatus.NoCookies,
                "TikTok login required. Sign in again.",
                sent,
                total),
            "no_friends_due" => new IosRunResult(
                IosRunStatus.NoFriendsDue,
                "No friends are due for a streak message today.",
                sent,
                total),
            "timed_out" => new IosRunResult(
                IosRunStatus.TimedOut,
                "The streak run timed out. Try again.",
                sent,
                total),
            "script_missing" => new IosRunResult(
                IosRunStatus.Failed,
                "Automation script missing from the app bundle.",
                sent,
                total),
            "navigation_failed" => new IosRunResult(
                IosRunStatus.Failed,
                "Could not load TikTok messages.",
                sent,
                total),
            _ => new IosRunResult(
                IosRunStatus.Failed,
                string.IsNullOrWhiteSpace(reason) ? "Streak run failed." : reason,
                sent,
                total)
        };
    }

    private static (int Sent, int Total) CountLatestRun(List<StreakRunResult> history)
    {
        var latest = history.FirstOrDefault();
        if (latest?.FriendResults == null || latest.FriendResults.Count == 0)
            return (0, 0);

        var sent = latest.FriendResults.Count(r => r.Success);
        return (sent, latest.FriendResults.Count);
    }

    private static void ClearLastFailureReason() =>
        AppStorageProvider.Current.SetString(AppConstants.LastRunFailureReasonKey, string.Empty);
}
