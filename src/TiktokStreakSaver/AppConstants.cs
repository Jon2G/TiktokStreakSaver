namespace TiktokStreakSaver;

/// <summary>
/// Centralized package and intent constants (keep in sync with ApplicationId in csproj).
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public static class AppConstants
{
    public const string PackageName = "com.jon2g.tiktokstreaksaver";

    public const string ActionStreakAlarm = PackageName + ".ACTION_STREAK_ALARM";

    /// <summary>App Group for shared settings, cookies, and extension state.</summary>
    public const string AppGroupId = "group.com.jon2g.tiktokstreaksaver";

    public const string SharedCookiesFileName = "cookies.json";

    public const string FriendsListFileName = "friends_list.json";

    public const string SessionStateFileName = "session_state.json";

    public const string AuthRequiredKey = "auth_required";

    public const string ForceManualRunKey = "force_manual_run";

    public const string LastRunFailureReasonKey = "last_run_failure_reason";

    public const string RunLogsFileName = "run_logs.txt";

    public const string RunHistoryFileName = "run_history.json";

    public const string MauiRunTraceFileName = "maui_run_trace.txt";

    public const string LastRunLogSnippetKey = "last_run_log_snippet";

    public const string MessageTextFileName = "message_text.txt";

    public const string RandomizeMessagesFlagFileName = "randomize_normal_messages.bool";

    public const string IosOnboardingCompleteKey = "ios_onboarding_complete";

    public const string DesktopChromeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
}
