namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>
/// Runs a streak when the widget queued one via <see cref="PendingStreakRun"/> (main app only).
/// App Shortcuts use <see cref="IosShortcutRunBridge"/> directly.
/// </summary>
public static class IosPendingStreakRunService
{
    private static readonly int[] RetryDelaysMs = [0, 500, 1500, 3000];
    private static bool _handling;

    public static void TryRunPendingFromActivation()
    {
        if (_handling || IosStreakRunner.IsRunning || IosShortcutRunBridge.IsShortcutRunActive)
            return;

        _ = TryRunPendingWithRetriesAsync();
    }

    private static async Task TryRunPendingWithRetriesAsync()
    {
        foreach (var delayMs in RetryDelaysMs)
        {
            if (delayMs > 0)
                await Task.Delay(delayMs);

            if (_handling || IosStreakRunner.IsRunning || IosShortcutRunBridge.IsShortcutRunActive)
                return;

            if (!StreakEngineBridge.ConsumePendingRun())
                continue;

            _handling = true;
            try
            {
                IosRunTrace.Append($"widget_pending_run_start delay={delayMs}");
                await IosRunReadiness.WaitForForegroundAsync();
                await IosRunReadiness.RefreshSessionIfNeededAsync();

                var coldStartDelay = AppLaunchState.IsColdLaunch ? 1000 : 300;
                await Task.Delay(coldStartDelay);
                await IosStreakRunner.RunNowAsync(manual: false);
            }
            finally
            {
                _handling = false;
                AppLaunchState.MarkWarmLaunch();
            }

            return;
        }
    }
}
