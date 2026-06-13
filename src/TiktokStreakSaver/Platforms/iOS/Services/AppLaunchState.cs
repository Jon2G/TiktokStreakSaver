namespace TiktokStreakSaver.Platforms.iOS.Services;

internal static class AppLaunchState
{
    private static bool _isColdLaunch = true;

    public static bool IsColdLaunch => _isColdLaunch;

    public static void MarkWarmLaunch() => _isColdLaunch = false;
}
