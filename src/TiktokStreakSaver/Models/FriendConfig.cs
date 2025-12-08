namespace TiktokStreakSaver.Models;

/// <summary>
/// Represents a friend configuration for sending streak messages
/// </summary>
public class FriendConfig
{
    /// <summary>
    /// Unique identifier for this friend entry
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// TikTok username of the friend (without @)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Display name for easier identification
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this friend is enabled for streak messages
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Last time a streak message was successfully sent to this friend
    /// </summary>
    public DateTime? LastMessageSent { get; set; }

    /// <summary>
    /// Number of successful messages sent
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed attempts
    /// </summary>
    public int FailureCount { get; set; }
}

/// <summary>
/// Represents the result of a streak run
/// </summary>
public class StreakRunResult
{
    public DateTime RunTime { get; set; } = DateTime.Now;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FriendsErrorMessage => string.Join(',',FriendResults?.Where(x => x.Failed)?.Select(x => x.ErrorMessage)??[]);
    public List<FriendMessageResult> FriendResults { get; set; } = new();
    public bool Failed=>!Success && (!string.IsNullOrEmpty(FriendsErrorMessage)||!string.IsNullOrEmpty(ErrorMessage));
}

/// <summary>
/// Result of sending a message to a specific friend
/// </summary>
public class FriendMessageResult
{
    public string FriendId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool Failed=>!Success && !string.IsNullOrEmpty(ErrorMessage);
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}



