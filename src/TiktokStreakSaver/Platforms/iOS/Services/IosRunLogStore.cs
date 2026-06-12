namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>Reads streak run logs written by the native StreakEngine runner.</summary>
public static class IosRunLogStore
{
    private const int MaxLines = 500;

    public static List<string> GetLogs()
    {
        try
        {
            var path = RunLogsFilePath;
            if (!File.Exists(path))
                return new List<string>();

            var lines = File.ReadAllLines(path);
            if (lines.Length <= MaxLines)
                return lines.ToList();

            return lines.Skip(lines.Length - MaxLines).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    internal static string RunLogsFilePath =>
        Path.Combine(AppGroupPaths.ContainerPath ?? FileSystem.AppDataDirectory, AppConstants.RunLogsFileName);
}
