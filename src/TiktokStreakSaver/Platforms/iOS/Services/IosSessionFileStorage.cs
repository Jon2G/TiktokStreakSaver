using System.Text.Json;
using Foundation;

namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>
/// Durable session_state.json in the App Group container (same pattern as friends_list.json).
/// UserDefaults bool reads for session_valid can be flaky across tab navigation; the file is source of truth on iOS.
/// </summary>
public static class IosSessionFileStorage
{
    private const string SessionValidKey = "session_valid";
    private static bool _migrationAttempted;

    private sealed record SessionStateFile(bool Valid, long LastCheckTicks);

    public static string FilePath => AppGroupPaths.SessionStateFilePath;

    public static bool ReadValid(out long lastCheckTicks)
    {
        lastCheckTicks = 0;
        EnsureMigrationIfNeeded();

        if (!File.Exists(FilePath))
            return false;

        try
        {
            var json = File.ReadAllText(FilePath);
            var state = JsonSerializer.Deserialize<SessionStateFile>(json);
            if (state == null)
                return false;

            lastCheckTicks = state.LastCheckTicks;
            return state.Valid;
        }
        catch
        {
            return false;
        }
    }

    public static bool WriteValid(bool valid, long lastCheckTicks)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new SessionStateFile(valid, lastCheckTicks));
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
            File.Move(temp, FilePath);

            if (!File.Exists(FilePath))
                return false;

            var written = JsonSerializer.Deserialize<SessionStateFile>(File.ReadAllText(FilePath));
            return written != null && written.Valid == valid && written.LastCheckTicks == lastCheckTicks;
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

    private static void EnsureMigrationIfNeeded()
    {
        if (_migrationAttempted && File.Exists(FilePath))
            return;

        _migrationAttempted = true;
        if (File.Exists(FilePath))
            return;

        var defaults = AppGroupPaths.IsAppGroupAvailable
            ? new NSUserDefaults(AppConstants.AppGroupId, NSUserDefaultsType.SuiteName)
              ?? NSUserDefaults.StandardUserDefaults
            : NSUserDefaults.StandardUserDefaults;

        if (!defaults.BoolForKey(SessionValidKey))
            return;

        var ticks = (long)defaults.DoubleForKey("session_last_check");
        if (ticks <= 0)
            ticks = DateTime.Now.Ticks;

        WriteValid(true, ticks);
    }
}
