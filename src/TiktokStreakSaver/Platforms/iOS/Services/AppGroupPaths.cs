namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>
/// Shared storage paths. Probes App Group once; on Simulator without a team ID, uses Library (same as MAUI AppDataDirectory).
/// </summary>
public static class AppGroupPaths
{
    private static bool _probed;
    private static bool _isAppGroupAvailable;
    private static string? _containerPath;

    public static bool IsAppGroupAvailable
    {
        get
        {
            ProbeIfNeeded();
            return _isAppGroupAvailable;
        }
    }

    public static string? ContainerPath
    {
        get
        {
            ProbeIfNeeded();
            return _containerPath;
        }
    }

    public static string SessionStateFilePath =>
        Path.Combine(SharedStorageDirectory, AppConstants.SessionStateFileName);

    public static string RunLogsFilePath =>
        Path.Combine(SharedStorageDirectory, AppConstants.RunLogsFileName);

    public static string RunHistoryFilePath =>
        Path.Combine(SharedStorageDirectory, AppConstants.RunHistoryFileName);

    public static string MauiRunTraceFilePath =>
        Path.Combine(SharedStorageDirectory, AppConstants.MauiRunTraceFileName);

    /// <summary>All directories where native or MAUI may write shared files.</summary>
    public static IReadOnlyList<string> GetAllStorageDirectories()
    {
        ProbeIfNeeded();
        var dirs = new List<string>();

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!dirs.Contains(normalized, StringComparer.Ordinal))
                dirs.Add(normalized);
        }

        Add(ContainerPath);
        Add(FileSystem.AppDataDirectory);

        try
        {
            var appSupport = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Add(Path.Combine(appSupport, AppConstants.PackageName));

            // Legacy native fallback before Library alignment (Application Support/{bundleId}).
            var library = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "..",
                "Library",
                "Application Support",
                AppConstants.PackageName);
            Add(Path.GetFullPath(library));
        }
        catch
        {
            // Ignore path construction failures.
        }

        return dirs;
    }

    public static IReadOnlyList<string> GetAllRunLogsFilePaths() =>
        GetAllStorageDirectories().Select(dir => Path.Combine(dir, AppConstants.RunLogsFileName)).ToList();

    public static IReadOnlyList<string> GetAllRunHistoryFilePaths() =>
        GetAllStorageDirectories().Select(dir => Path.Combine(dir, AppConstants.RunHistoryFileName)).ToList();

    public static IReadOnlyList<string> GetAllMauiTraceFilePaths() =>
        GetAllStorageDirectories().Select(dir => Path.Combine(dir, AppConstants.MauiRunTraceFileName)).ToList();

    /// <summary>App Group container when available; otherwise MAUI Library (matches native fallback).</summary>
    public static string SharedStorageDirectory
    {
        get
        {
            ProbeIfNeeded();
            return ContainerPath ?? FileSystem.AppDataDirectory;
        }
    }

    public static string CookiesFilePath =>
        Path.Combine(SharedStorageDirectory, AppConstants.SharedCookiesFileName);

    public static string FriendsListFilePath =>
        Path.Combine(SharedStorageDirectory, AppConstants.FriendsListFileName);

    private static void ProbeIfNeeded()
    {
        if (_probed)
            return;
        _probed = true;

        var url = Foundation.NSFileManager.DefaultManager.GetContainerUrl(AppConstants.AppGroupId);
        if (url != null)
        {
            _isAppGroupAvailable = true;
            _containerPath = url.Path;
        }
        else
        {
            _isAppGroupAvailable = false;
            _containerPath = null;
        }
    }
}
