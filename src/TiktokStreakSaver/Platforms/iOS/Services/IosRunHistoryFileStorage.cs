using System.Text.Json;
using TiktokStreakSaver.Models;
using TiktokStreakSaver.Services;

namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>Durable run_history.json in the shared App Group container (same pattern as friends_list.json).</summary>
public static class IosRunHistoryFileStorage
{
    private const string RunHistoryKey = "run_history";
    private static bool _migrationAttempted;

    public static string FilePath => AppGroupPaths.RunHistoryFilePath;

    public static List<StreakRunResult> ReadHistory()
    {
        EnsureMigrationIfNeeded();
        foreach (var path in AppGroupPaths.GetAllRunHistoryFilePaths())
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                var history = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListStreakRunResult);
                if (history != null && history.Count > 0)
                    return history;
            }
            catch
            {
                // Try next candidate path.
            }
        }

        return new List<StreakRunResult>();
    }

    public static bool WriteHistory(List<StreakRunResult> history)
    {
        try
        {
            var json = JsonSerializer.Serialize(history, AppJsonContext.Default.ListStreakRunResult);
            return WriteJson(json);
        }
        catch
        {
            return false;
        }
    }

    public static bool WriteJson(string json)
    {
        var wrote = false;
        foreach (var path in AppGroupPaths.GetAllRunHistoryFilePaths())
        {
            if (TryWriteJson(path, json))
                wrote = true;
        }

        return wrote;
    }

    private static bool TryWriteJson(string path, string json)
    {
        try
        {
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            var temp = path + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Clear()
    {
        _migrationAttempted = false;
        foreach (var path in AppGroupPaths.GetAllRunHistoryFilePaths())
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort per path.
            }
        }
    }

    private static void EnsureMigrationIfNeeded()
    {
        if (_migrationAttempted)
            return;

        _migrationAttempted = true;
        if (ReadHistory().Count > 0)
            return;

        var defaults = AppGroupPaths.IsAppGroupAvailable
            ? new Foundation.NSUserDefaults(AppConstants.AppGroupId, Foundation.NSUserDefaultsType.SuiteName)
              ?? Foundation.NSUserDefaults.StandardUserDefaults
            : Foundation.NSUserDefaults.StandardUserDefaults;

        var legacy = defaults.StringForKey(RunHistoryKey);
        if (!string.IsNullOrWhiteSpace(legacy))
            WriteJson(legacy);
    }
}
