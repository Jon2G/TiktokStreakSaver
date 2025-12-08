namespace TiktokStreakSaver.Services;

/// <summary>
/// Service for managing TikTok session state
/// </summary>
public class SessionService
{
    private const string SessionValidKey = "session_valid";
    private const string SessionLastCheckKey = "session_last_check";

    /// <summary>
    /// Get whether the session was valid on last check
    /// </summary>
    public bool IsSessionValid()
    {
        return Preferences.Get(SessionValidKey, false);
    }

    /// <summary>
    /// Set whether the session is valid
    /// </summary>
    public void SetSessionValid(bool valid)
    {
        Preferences.Set(SessionValidKey, valid);
        Preferences.Set(SessionLastCheckKey, DateTime.Now.Ticks);
    }

    /// <summary>
    /// Get when the session was last checked
    /// </summary>
    public DateTime? GetLastCheckTime()
    {
        var ticks = Preferences.Get(SessionLastCheckKey, 0L);
        return ticks > 0 ? new DateTime(ticks) : null;
    }

    /// <summary>
    /// Clear session data (logout)
    /// </summary>
    public void ClearSession()
    {
        Preferences.Set(SessionValidKey, false);
        Preferences.Remove(SessionLastCheckKey);
    }
}

