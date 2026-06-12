using TiktokStreakSaver.Services.Storage;

namespace TiktokStreakSaver.Platforms.iOS.Services;

public static class IosManualRunFlags
{
    private static IAppStorage Storage => AppStorageProvider.Current;

    public static void SetForceManualRun(bool value) =>
        Storage.SetBool(AppConstants.ForceManualRunKey, value);
}
