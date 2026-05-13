using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using TiktokStreakSaver.Platforms.Android.Receivers;
using TiktokStreakSaver.Platforms.Android.Services;

namespace TiktokStreakSaver
{
    [Application]
    [Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override void OnCreate()
        {
            base.OnCreate();

            try
            {
                NetworkChangeMonitor.Register(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainApplication: NetworkChangeMonitor registration failed — {ex.Message}");
            }

            try
            {
                var batteryFilter = new IntentFilter(Intent.ActionBatteryLow);
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                {
                    // Android 13+ requires an explicit export flag for runtime-registered receivers.
                    // ACTION_BATTERY_LOW is a system-only broadcast, so we mark this as not exported.
                    RegisterReceiver(new BatteryLowReceiver(), batteryFilter, ReceiverFlags.NotExported);
                }
                else
                {
                    RegisterReceiver(new BatteryLowReceiver(), batteryFilter);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainApplication: BatteryLowReceiver registration failed — {ex.Message}");
            }
        }
    }
}
