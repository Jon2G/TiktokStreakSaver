using Foundation;

namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>
/// Durable friends_list.json in the App Group container (same pattern as cookies.json).
/// </summary>
public static class IosFriendsFileStorage
{
    private const string FriendsListKey = "friends_list";
    private static bool _migrationAttempted;

    public static string FilePath => AppGroupPaths.FriendsListFilePath;

    public static string? ReadJson(bool migrateFromDefaults = false)
    {
        EnsureMigrationIfNeeded(migrateFromDefaults);
        if (!File.Exists(FilePath))
            return null;

        try
        {
            return File.ReadAllText(FilePath);
        }
        catch
        {
            return null;
        }
    }

    public static bool WriteJson(string json)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
            File.Move(temp, FilePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Clear()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    private static void EnsureMigrationIfNeeded(bool migrateFromDefaults)
    {
        if (!migrateFromDefaults)
            return;

        if (_migrationAttempted && File.Exists(FilePath))
            return;

        _migrationAttempted = true;
        if (File.Exists(FilePath))
            return;

        var defaults = AppGroupPaths.IsAppGroupAvailable
            ? new NSUserDefaults(AppConstants.AppGroupId, NSUserDefaultsType.SuiteName)
              ?? NSUserDefaults.StandardUserDefaults
            : NSUserDefaults.StandardUserDefaults;

        var legacy = defaults.StringForKey(FriendsListKey);
        if (!string.IsNullOrWhiteSpace(legacy))
            WriteJson(legacy);
    }
}
