using Android.App;
using Android.Content;
using Android.OS;
using TiktokStreakSaver;
using TiktokStreakSaver.Platforms.Android.Services;

namespace TiktokStreakSaver.Platforms.Android.Receivers;

[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
[BroadcastReceiver(Name = AppConstants.PackageName + ".Receivers.AlarmReceiver", Enabled = true, Exported = false)]
public class AlarmReceiver : BroadcastReceiver
{
    public const string ActionStreakAlarm = AppConstants.ActionStreakAlarm;

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        if (intent.Action == ActionStreakAlarm)
        {
            var serviceIntent = new Intent(context, typeof(StreakService));

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                context.StartForegroundService(serviceIntent);
            else
                context.StartService(serviceIntent);
        }
    }
}
