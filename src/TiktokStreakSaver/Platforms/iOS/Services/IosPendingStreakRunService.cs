namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>
/// Runs a streak when Shortcuts or the widget queued one via <see cref="PendingStreakRun"/> (main app only).
/// </summary>
public static class IosPendingStreakRunService
{
    private static bool _handling;

    public static void TryRunPendingFromActivation()
    {
        if (_handling || IosStreakRunner.IsRunning)
            return;

        if (!StreakEngineBridge.ConsumePendingRun())
            return;

        _handling = true;
        _ = RunQueuedAsync();
    }

    private static async Task RunQueuedAsync()
    {
        try
        {
            await Task.Delay(400);
            await IosStreakRunner.RunNowAsync(manual: false);
        }
        finally
        {
            _handling = false;
        }
    }
}
