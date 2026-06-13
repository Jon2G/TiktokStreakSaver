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

        IosRunTrace.Append(
            $"run_now_start manual={manual} appGroup={AppGroupPaths.IsAppGroupAvailable} " +
            $"storage={AppGroupPaths.SharedStorageDirectory} logs={AppGroupPaths.RunLogsFilePath}");

        if (manual)
            IosManualRunFlags.SetForceManualRun(true);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runStartedAt = DateTime.UtcNow;

        if (!StreakEngineBridge.RunStreak(success =>
            {
                _isRunning = false;
                IosManualRunFlags.SetForceManualRun(false);
                var elapsedMs = (DateTime.UtcNow - runStartedAt).TotalMilliseconds;
                IosRunTrace.Append(
                    $"native_finished success={success} elapsed_ms={elapsedMs:F0} " +
                    $"reason={AppStorageProvider.Current.GetString(AppConstants.LastRunFailureReasonKey, string.Empty)}");
                tcs.TrySetResult(success);
            }))
        {
            _isRunning = false;
            IosManualRunFlags.SetForceManualRun(false);
            return new IosRunResult(
                IosRunStatus.EngineMissing,
                "Native streak engine is not available on this build. Rebuild the iOS app on macOS so StreakEngine.xcframework is embedded.",
                0,
                0);
        }

        using var timeoutCts = new CancellationTokenSource(RunTimeout);
        await using var _ = timeoutCts.Token.Register(() =>
        {
            if (tcs.TrySetResult(false))
            {
                AppStorageProvider.Current.SetString(AppConstants.LastRunFailureReasonKey, "timed_out");
                IosRunTrace.Append("run_now_timeout");
            }
        });

        bool success = await tcs.Task;

        EnsureFailureRecorded(settings, success);
        var result = MapResult(settings, success);

        IosRunTrace.Append(
            $"run_now_done success={success} status={result.Status} sent={result.Sent}/{result.Total} " +
            $"reason={AppStorageProvider.Current.GetString(AppConstants.LastRunFailureReasonKey, string.Empty)} " +
            $"history={settings.GetRunHistory().Count} logs={IosRunLogStore.GetRecentLines(3).Count}");

        SessionState.NotifyChanged();
        return result;
    }

    private static void EnsureFailureRecorded(SettingsService settings, bool success)
    {
        if (success)
            return;

        var history = settings.GetRunHistory();
        if (history.Count > 0)
            return;

        var reason = AppStorageProvider.Current.GetString(AppConstants.LastRunFailureReasonKey, string.Empty);
        var logTail = IosRunLogStore.GetRecentLines(8);
        var errorMessage = string.IsNullOrWhiteSpace(reason)
            ? "Streak run failed with no native history recorded."
            : reason;

        if (logTail.Count > 0)
            errorMessage += Environment.NewLine + string.Join(Environment.NewLine, logTail);

        settings.AddRunResult(new StreakRunResult
        {
            RunTime = DateTime.Now,
            Success = false,
            ErrorMessage = errorMessage
        });
    }

    private static IosRunResult MapResult(SettingsService settings, bool success)
    {
        var reason = AppStorageProvider.Current.GetString(AppConstants.LastRunFailureReasonKey, string.Empty);
        var history = settings.GetRunHistory();
        var latest = history.FirstOrDefault();
        var (sent, total) = CountLatestRun(latest);
        var details = BuildDiagnosticDetails(latest);

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

            var completedMessage = sent > 0
                ? $"Sent {sent} of {total} message{(total != 1 ? "s" : "")}."
                : "Run finished.";
            return new IosRunResult(IosRunStatus.Completed, completedMessage, sent, total);
        }

        if (string.IsNullOrWhiteSpace(reason)
            && sent == 0
            && total == 0
            && IosRunLogStore.GetNativeLogLineCount() == 0)
        {
            return new IosRunResult(
                IosRunStatus.Failed,
                "Native streak runner exited immediately with no logs. Rebuild the app so StreakEngine.xcframework is embedded.",
                sent,
                total,
                details);
        }

        var (status, message) = reason switch
        {
            "no_network" => (IosRunStatus.Offline, "No network connection."),
            "no_cookies" => (IosRunStatus.NoCookies, "TikTok session cookies are missing. Sign in again."),
            "login_required" => (IosRunStatus.NoCookies, "TikTok login required. Sign in again."),
            "no_friends_due" => (IosRunStatus.NoFriendsDue, "No friends are due for a streak message today."),
            "timed_out" => (IosRunStatus.TimedOut, "The streak run timed out. Try again."),
            "scene_not_ready" => (IosRunStatus.Failed, "The app window was not ready. Open Streak Saver and try again."),
            "script_missing" => (IosRunStatus.Failed, "Automation script missing from the app bundle."),
            "navigation_failed" => (IosRunStatus.Failed, "Could not load TikTok messages."),
            "send_error" => (IosRunStatus.Failed, BuildSendErrorMessage(latest)),
            _ => (IosRunStatus.Failed, ResolveGenericFailureMessage(reason, latest))
        };

        return new IosRunResult(status, message, sent, total, details);
    }

    private static string BuildSendErrorMessage(StreakRunResult? latest)
    {
        var friendErrors = GetFriendErrorSummary(latest, maxEntries: 2);
        if (!string.IsNullOrWhiteSpace(friendErrors))
            return $"Could not send to any friends. {friendErrors}";

        if (!string.IsNullOrWhiteSpace(latest?.ErrorMessage))
            return latest.ErrorMessage;

        return "Could not send to any friends.";
    }

    private static string ResolveGenericFailureMessage(string reason, StreakRunResult? latest)
    {
        if (!string.IsNullOrWhiteSpace(reason))
            return reason;

        if (!string.IsNullOrWhiteSpace(latest?.ErrorMessage))
            return latest.ErrorMessage;

        var friendErrors = GetFriendErrorSummary(latest, maxEntries: 2);
        if (!string.IsNullOrWhiteSpace(friendErrors))
            return friendErrors;

        return "Streak run failed.";
    }

    private static string? BuildDiagnosticDetails(StreakRunResult? latest)
    {
        var parts = new List<string>();

        var friendErrors = GetFriendErrorSummary(latest, maxEntries: 5);
        if (!string.IsNullOrWhiteSpace(friendErrors))
            parts.Add(friendErrors);

        var logTail = IosRunLogStore.GetRecentLines(5);
        if (logTail.Count > 0)
            parts.Add(string.Join(Environment.NewLine, logTail));

        if (parts.Count == 0)
            return null;

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    private static string GetFriendErrorSummary(StreakRunResult? latest, int maxEntries)
    {
        if (latest?.FriendResults == null || latest.FriendResults.Count == 0)
            return string.Empty;

        var failed = latest.FriendResults
            .Where(r => !r.Success)
            .Take(maxEntries)
            .Select(r =>
            {
                var detail = string.IsNullOrWhiteSpace(r.ErrorMessage) ? "failed" : r.ErrorMessage;
                return $"@{r.Username}: {detail}";
            })
            .ToList();

        if (failed.Count == 0)
            return string.Empty;

        var summary = string.Join("; ", failed);
        var remaining = latest.FriendResults.Count(r => !r.Success) - failed.Count;
        if (remaining > 0)
            summary += $" (+{remaining} more)";

        return summary;
    }

    private static (int Sent, int Total) CountLatestRun(StreakRunResult? latest)
    {
        if (latest?.FriendResults == null || latest.FriendResults.Count == 0)
            return (0, 0);

        var sent = latest.FriendResults.Count(r => r.Success);
        return (sent, latest.FriendResults.Count);
    }

    private static void ClearLastFailureReason() =>
        AppStorageProvider.Current.SetString(AppConstants.LastRunFailureReasonKey, string.Empty);
}
