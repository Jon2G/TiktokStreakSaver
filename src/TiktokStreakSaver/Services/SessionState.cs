namespace TiktokStreakSaver.Services;

/// <summary>Broadcast when TikTok session validity changes so all tabs refresh their UI.</summary>
public static class SessionState
{
    public static event EventHandler? Changed;

    public static void NotifyChanged() => Changed?.Invoke(null, EventArgs.Empty);
}
