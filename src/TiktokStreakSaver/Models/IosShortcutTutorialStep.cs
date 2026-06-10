namespace TiktokStreakSaver.Models;

/// <summary>
/// One step in the iOS Shortcuts setup walkthrough.
/// Add matching media under Resources/Raw/ios_shortcuts_tutorial/ (see README there).
/// </summary>
public sealed class IosShortcutTutorialStep
{
    public required int StepNumber { get; init; }
    public required string TabTitle { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    /// <summary>File name only, e.g. step_01.gif — resolved under ios_shortcuts_tutorial/.</summary>
    public string? MediaFileName { get; init; }
}
