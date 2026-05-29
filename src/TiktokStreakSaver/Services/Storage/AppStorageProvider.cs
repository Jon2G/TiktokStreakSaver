namespace TiktokStreakSaver.Services.Storage;

public static class AppStorageProvider
{
    private static IAppStorage? _instance;

    public static IAppStorage Current => _instance ??= Create();

    public static void SetInstance(IAppStorage storage) => _instance = storage;

    private static IAppStorage Create()
    {
#if IOS
        return new Platforms.iOS.Services.AppGroupAppStorage();
#else
        return new MauiPreferencesAppStorage();
#endif
    }
}
