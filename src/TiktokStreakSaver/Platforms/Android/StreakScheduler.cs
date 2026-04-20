using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using TiktokStreakSaver;
using TiktokStreakSaver.Platforms.Android.Receivers;
using TiktokStreakSaver.Platforms.Android.Services;
using TiktokStreakSaver.Services;

namespace TiktokStreakSaver.Platforms.Android;

/// <summary>
/// Manages alarm scheduling for the streak service
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public static class StreakScheduler
{
    private const int AlarmRequestCode = 1001;

    /// <summary>
    /// Schedule the next streak run based on settings
    /// </summary>
    public static void ScheduleNextRun(Context context)
    {
        var settingsService = new SettingsService();
        var intervalMinutes = settingsService.GetIntervalMinutes();
        var lastRun = settingsService.GetLastRunTime();

        DateTime nextRunTime;
        if (lastRun.HasValue)
        {
            nextRunTime = lastRun.Value.AddMinutes(intervalMinutes);
            // If the calculated time is in the past, schedule for now + small delay
            if (nextRunTime < DateTime.Now)
            {
                nextRunTime = DateTime.Now.AddMinutes(1);
            }
        }
        else
        {
            // First run - schedule for interval from now
            nextRunTime = DateTime.Now.AddMinutes(intervalMinutes);
        }

        ScheduleAt(context, nextRunTime);
        settingsService.SetScheduled(true);
    }

    /// <summary>
    /// When the device is offline, retry the streak check in one hour without changing last-run time.
    /// </summary>
    public static void ScheduleRetryInOneHour(Context context)
    {
        ScheduleAt(context, DateTime.Now.AddHours(1));
        new SettingsService().SetScheduled(true);
    }

    /// <summary>
    /// Schedule a run at a specific time
    /// </summary>
    public static void ScheduleAt(Context context, DateTime triggerTime)
    {
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, typeof(AlarmReceiver));
        intent.SetAction(AlarmReceiver.ActionStreakAlarm);

        var pendingIntent = PendingIntent.GetBroadcast(
            context,
            AlarmRequestCode,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
        );

        if (pendingIntent == null) return;

        // Convert to milliseconds since epoch
        var triggerAtMillis = new DateTimeOffset(triggerTime).ToUnixTimeMilliseconds();

        // Use exact alarm for precise timing
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            // Android 12+ requires checking for exact alarm permission
            if (alarmManager.CanScheduleExactAlarms())
            {
                alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            }
            else
            {
                // Fall back to inexact alarm, but request permission
                alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            }
        }
        else if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
        }
        else if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
        {
            alarmManager.SetExact(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
        }
        else
        {
            alarmManager.Set(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
        }

        System.Diagnostics.Debug.WriteLine($"StreakScheduler: Scheduled next run at {triggerTime}");
    }

    /// <summary>
    /// Cancel any scheduled alarm
    /// </summary>
    public static void CancelSchedule(Context context)
    {
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, typeof(AlarmReceiver));
        intent.SetAction(AlarmReceiver.ActionStreakAlarm);

        var pendingIntent = PendingIntent.GetBroadcast(
            context,
            AlarmRequestCode,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
        );

        if (pendingIntent != null)
        {
            alarmManager.Cancel(pendingIntent);
        }

        var settingsService = new SettingsService();
        settingsService.SetScheduled(false);

        System.Diagnostics.Debug.WriteLine("StreakScheduler: Schedule cancelled");
    }

    /// <summary>
    /// Run the service immediately. Returns false if automation is already running.
    /// </summary>
    public static bool RunNow(Context context, bool isBurstMode = false)
    {
        if (StreakService.IsRunning)
            return false;

        var settingsService = new SettingsService();
        if (settingsService.IsScheduled())
        {
            CancelSchedule(context);
            settingsService.SetScheduled(true);
        }

        var serviceIntent = new Intent(context, typeof(StreakService));
        serviceIntent.PutExtra("IsBurstMode", isBurstMode);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            context.StartForegroundService(serviceIntent);
        else
            context.StartService(serviceIntent);

        return true;
    }

    /// <summary>
    /// Request the running StreakService to stop gracefully.
    /// </summary>
    public static void StopService(Context context)
    {
        var serviceIntent = new Intent(context, typeof(StreakService));
        serviceIntent.SetAction("STOP_SERVICE");

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            context.StartForegroundService(serviceIntent);
        else
            context.StartService(serviceIntent);
    }

    /// <summary>
    /// Check if exact alarms can be scheduled (Android 12+)
    /// </summary>
    public static bool CanScheduleExactAlarms(Context context)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.S)
        {
            return true;
        }

        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        return alarmManager?.CanScheduleExactAlarms() ?? false;
    }

    /// <summary>
    /// Open settings to allow exact alarms (Android 12+)
    /// </summary>
    public static void RequestExactAlarmPermission(Context context)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            var intent = new Intent(Settings.ActionRequestScheduleExactAlarm);
            var packageName = context.PackageName;
            if (!string.IsNullOrEmpty(packageName))
                intent.SetData(global::Android.Net.Uri.Parse($"package:{packageName}"));
            intent.SetFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
    }

    /// <summary>
    /// Request to ignore battery optimizations
    /// </summary>
    public static void RequestBatteryOptimizationExemption(Context context)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            var powerManager = (PowerManager?)context.GetSystemService(Context.PowerService);
            var packageName = context.PackageName;

            if (powerManager != null && packageName != null && !powerManager.IsIgnoringBatteryOptimizations(packageName))
            {
                var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations);
                intent.SetData(global::Android.Net.Uri.Parse($"package:{packageName}"));
                intent.SetFlags(ActivityFlags.NewTask);
                context.StartActivity(intent);
            }
        }
    }

    /// <summary>
    /// Check if app is exempted from battery optimization
    /// </summary>
    public static bool IsIgnoringBatteryOptimizations(Context context)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            var powerManager = (PowerManager?)context.GetSystemService(Context.PowerService);
            var packageName = context.PackageName;

            return powerManager?.IsIgnoringBatteryOptimizations(packageName) ?? false;
        }
        return true;
    }
}









