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

    public static string CookiesFilePath =>
        Path.Combine(ContainerPath ?? FileSystem.AppDataDirectory, AppConstants.SharedCookiesFileName);

    public static string FriendsListFilePath =>
        Path.Combine(ContainerPath ?? FileSystem.AppDataDirectory, AppConstants.FriendsListFileName);

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
