using TiktokStreakSaver.Services.Storage;

namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>Reads streak run logs written by the native StreakEngine runner.</summary>
public static class IosRunLogStore
{
    private const int MaxLines = 500;

    public static List<string> GetLogs() => GetRecentLines(MaxLines);

    public static int GetNativeLogLineCount()
    {
        var count = 0;
        foreach (var path in AppGroupPaths.GetAllRunLogsFilePaths())
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                count += File.ReadAllLines(path).Count(line => !string.IsNullOrWhiteSpace(line));
            }
            catch
            {
                // Ignore unreadable paths.
            }
        }

        return count;
    }

    public static List<string> GetRecentLines(int count)
    {
        if (count <= 0)
            return new List<string>();

        var combined = new List<string>();

        foreach (var path in AppGroupPaths.GetAllRunLogsFilePaths())
        {
            AppendLinesFromFile(combined, path, count);
            if (combined.Count >= count)
                break;
        }

        if (combined.Count == 0)
            combined.AddRange(GetSnippetFallback(count));

        var trace = IosRunTrace.GetRecentLines(Math.Min(count, 20));
        if (trace.Count > 0)
        {
            if (combined.Count > 0)
                combined.Add(string.Empty);
            combined.Add("--- MAUI trace ---");
            combined.AddRange(trace);
        }

        if (combined.Count <= count)
            return combined;

        return combined.Skip(combined.Count - count).ToList();
    }

    private static void AppendLinesFromFile(List<string> combined, string path, int maxTotal)
    {
        try
        {
            if (!File.Exists(path))
                return;

            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                combined.Add(line);
                if (combined.Count >= maxTotal)
                    return;
            }
        }
        catch
        {
            // Try other paths.
        }
    }

    private static List<string> GetSnippetFallback(int count)
    {
        try
        {
            var snippet = AppStorageProvider.Current.GetString(AppConstants.LastRunLogSnippetKey, string.Empty);
            if (string.IsNullOrWhiteSpace(snippet))
                return new List<string>();

            var lines = snippet.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length <= count)
                return lines.ToList();

            return lines.Skip(lines.Length - count).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static void Clear()
    {
        foreach (var path in AppGroupPaths.GetAllRunLogsFilePaths())
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

        try
        {
            AppStorageProvider.Current.SetString(AppConstants.LastRunLogSnippetKey, string.Empty);
        }
        catch
        {
            // Ignore storage errors.
        }

        IosRunTrace.Clear();
    }

    internal static string RunLogsFilePath =>
        AppGroupPaths.RunLogsFilePath;
}
