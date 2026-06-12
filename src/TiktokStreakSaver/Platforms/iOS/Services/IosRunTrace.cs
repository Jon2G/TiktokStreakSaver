namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>MAUI-side trace log written independently of the native StreakEngine runner.</summary>
public static class IosRunTrace
{
    private const int MaxLines = 200;
    private static readonly object Lock = new();

    public static void Append(string message)
    {
        var line = $"[{DateTime.UtcNow:O}] {message}";
        lock (Lock)
        {
            foreach (var path in AppGroupPaths.GetAllMauiTraceFilePaths())
            {
                try
                {
                    var dir = Path.GetDirectoryName(path)!;
                    Directory.CreateDirectory(dir);
                    File.AppendAllText(path, line + Environment.NewLine);
                    TrimIfNeeded(path);
                }
                catch
                {
                    // Best-effort; try other paths.
                }
            }
        }
    }

    public static void Clear()
    {
        lock (Lock)
        {
            foreach (var path in AppGroupPaths.GetAllMauiTraceFilePaths())
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
    }

    public static List<string> GetRecentLines(int count)
    {
        if (count <= 0)
            return new List<string>();

        foreach (var path in AppGroupPaths.GetAllMauiTraceFilePaths())
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                var lines = File.ReadAllLines(path);
                if (lines.Length == 0)
                    continue;

                if (lines.Length <= count)
                    return lines.ToList();

                return lines.Skip(lines.Length - count).ToList();
            }
            catch
            {
                // Try next path.
            }
        }

        return new List<string>();
    }

    private static void TrimIfNeeded(string path)
    {
        try
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length <= MaxLines)
                return;

            var trimmed = lines.Skip(lines.Length - MaxLines).ToArray();
            File.WriteAllLines(path, trimmed);
        }
        catch
        {
            // Ignore trim failures.
        }
    }
}
