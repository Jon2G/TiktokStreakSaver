using TiktokStreakSaver.Services;

namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>Shared foreground/session gates for shortcut and widget-triggered runs.</summary>
internal static class IosRunReadiness
{
    private static readonly TimeSpan ForegroundWaitTimeout = TimeSpan.FromSeconds(8);

    public static async Task WaitForForegroundAsync()
    {
        var deadline = DateTime.UtcNow + ForegroundWaitTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var state = UIKit.UIApplication.SharedApplication.ApplicationState;
            if (state == UIKit.UIApplicationState.Active)
                return;

            await Task.Delay(100);
        }

        IosRunTrace.Append("foreground_wait_timeout");
    }

    public static async Task RefreshSessionIfNeededAsync()
    {
        var session = new SessionService();
        await SessionRefreshHelper.RefreshAndGetRunReadyAsync(session);
    }
}
