using Foundation;

namespace TiktokStreakSaver
{
    [Register("AppDelegate")]
    [Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
