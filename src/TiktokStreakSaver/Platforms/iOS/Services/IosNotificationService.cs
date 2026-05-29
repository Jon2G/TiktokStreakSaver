using UserNotifications;

namespace TiktokStreakSaver.Platforms.iOS.Services;

public static class IosNotificationService
{
    public static Task RequestPermissionAsync()
    {
        var tcs = new TaskCompletionSource();
        UNUserNotificationCenter.Current.RequestAuthorization(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge,
            (granted, _) =>
            {
                StreakNotificationsBridge.RegisterCategories();
                tcs.TrySetResult();
            });
        return tcs.Task;
    }
}

internal static class StreakNotificationsBridge
{
    public static void RegisterCategories() { }
}
