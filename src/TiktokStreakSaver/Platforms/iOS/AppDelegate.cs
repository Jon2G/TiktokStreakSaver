using Foundation;
using TiktokStreakSaver.Platforms.iOS.Services;

namespace TiktokStreakSaver
{
    [Register("AppDelegate")]
    [Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override bool FinishedLaunching(UIKit.UIApplication application, NSDictionary launchOptions)
        {
            var result = base.FinishedLaunching(application, launchOptions);
            IosShortcutRunBridge.Register();
            IosPendingStreakRunService.TryRunPendingFromActivation();
            return result;
        }

        public override void OnActivated(UIKit.UIApplication application)
        {
            base.OnActivated(application);
            IosPendingStreakRunService.TryRunPendingFromActivation();
        }
    }
}
