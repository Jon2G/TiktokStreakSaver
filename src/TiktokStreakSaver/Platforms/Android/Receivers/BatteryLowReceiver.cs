using Android.Content;
using TiktokStreakSaver.Platforms.Android.Services;
using TiktokStreakSaver.Services;

namespace TiktokStreakSaver.Platforms.Android.Receivers;

/// <summary>
/// Runtime-registered receiver for <see cref="Intent.ActionBatteryLow"/>. Anticipates
/// today's streak run when the system warns the battery is running low, so the user's
/// streak still goes out before the device powers off.
///
/// Manifest registration of ACTION_BATTERY_LOW is blocked on Android 8+ (Oreo) — that's
/// why this class has no <c>[BroadcastReceiver]</c> attribute and is registered at
/// runtime from <see cref="MainApplication.OnCreate"/>.
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public class BatteryLowReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;
        if (intent.Action != Intent.ActionBatteryLow) return;

        try
        {
            var settings = new SettingsService();

            // 1) User toggle (default ON, but the user can disable it).
            if (!settings.GetSendOnBatteryLow())
            {
                System.Diagnostics.Debug.WriteLine("BatteryLowReceiver: user setting OFF — ignoring");
                return;
            }

            // 2) Automation must be enabled at all.
            if (!settings.IsScheduled()) return;

            // 3) Don't double-run if a session is already active.
            if (StreakService.IsRunning) return;

            // 4) Don't fire more than once per calendar day.
            if (settings.WasBatteryAnticipationUsedToday()) return;

            // 5) Today's streak hasn't actually been sent yet. Use a two-pronged check:
            //    - any enabled friend has not received a message today, OR
            //    - the last successful run wasn't today.
            var today = DateTime.Now.Date;
            var enabled = settings.GetEnabledFriends();
            bool anyFriendPending = enabled.Count == 0
                || enabled.Any(f => f.LastMessageSent?.Date != today);
            bool lastRunNotToday = settings.GetLastRunTime()?.Date != today;

            if (!anyFriendPending && !lastRunNotToday)
            {
                System.Diagnostics.Debug.WriteLine("BatteryLowReceiver: today's streak already sent — skipping");
                return;
            }

            settings.MarkBatteryAnticipationUsedToday();
            System.Diagnostics.Debug.WriteLine("BatteryLowReceiver: battery low — anticipating today's streak run");
            StreakScheduler.RunNow(context);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BatteryLowReceiver.OnReceive: {ex.Message}");
        }
    }
}
