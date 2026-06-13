using TiktokStreakSaver.Services;

namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>
/// Handles App Shortcut runs invoked from native MaintainStreakShortcutIntent via StreakShortcutBridge.
/// </summary>
public static class IosShortcutRunBridge
{
    private static bool _registered;
    private static bool _running;

    public static void Register()
    {
        if (_registered)
            return;

        if (!StreakEngineBridge.RegisterShortcutRunHandler(OnShortcutRunRequested))
        {
            IosRunTrace.Append("shortcut_bridge_register_failed");
            return;
        }

        _registered = true;
        IosRunTrace.Append("shortcut_bridge_registered");
    }

    public static bool IsShortcutRunActive => _running;

    private static void OnShortcutRunRequested()
    {
        if (_running || IosStreakRunner.IsRunning)
        {
            IosRunTrace.Append("shortcut_run_skipped already_running");
            return;
        }

        _running = true;
        _ = RunFromShortcutAsync();
    }

    private static async Task RunFromShortcutAsync()
    {
        try
        {
            IosRunTrace.Append("shortcut_run_start");
            IosShortcutRunUi.TryShowRunningOverlay();

            await IosRunReadiness.WaitForForegroundAsync();
            await IosRunReadiness.RefreshSessionIfNeededAsync();

            await Task.Delay(500);
            await IosStreakRunner.RunNowAsync(manual: false);
        }
        catch (Exception ex)
        {
            IosRunTrace.Append($"shortcut_run_error {ex.Message}");
        }
        finally
        {
            _running = false;
            IosShortcutRunUi.TryHideRunningOverlay();
            SessionState.NotifyChanged();
        }
    }
}
