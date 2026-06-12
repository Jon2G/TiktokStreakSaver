namespace TiktokStreakSaver.Platforms.iOS.Services;

public enum IosRunStatus
{
    Completed,
    AlreadyRunning,
    NoCookies,
    Offline,
    NoFriendsDue,
    TimedOut,
    EngineMissing,
    Failed
}

public sealed record IosRunResult(
    IosRunStatus Status,
    string? Message,
    int Sent,
    int Total,
    string? Details = null)
{
    public bool IsSuccess => Status == IosRunStatus.Completed && Sent > 0;
}
