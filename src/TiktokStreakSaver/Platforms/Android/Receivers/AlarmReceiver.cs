using Android.App;
using Android.Content;
using Android.OS;
using TiktokStreakSaver.Platforms.Android.Services;

namespace TiktokStreakSaver.Platforms.Android.Receivers;

[BroadcastReceiver(Name = "com.companyname.tiktokstreaksaver.Receivers.AlarmReceiver", Enabled = true, Exported = false)]
public class AlarmReceiver : BroadcastReceiver
{
    public const string ActionStreakAlarm = "com.companyname.tiktokstreaksaver.ACTION_STREAK_ALARM";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        if (intent.Action == ActionStreakAlarm)
        {
            // Start the foreground service
            var serviceIntent = new Intent(context, typeof(StreakService));
            
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                context.StartForegroundService(serviceIntent);
            }
            else
            {
                context.StartService(serviceIntent);
            }
        }
    }
}



