using TiktokStreakSaver.Models;

namespace TiktokStreakSaver.Services;

public static class IosShortcutTutorialCatalog
{
    public const string MediaFolder = "ios_shortcuts_tutorial";

    public static IReadOnlyList<IosShortcutTutorialStep> Steps { get; } =
    [
        new()
        {
            StepNumber = 1,
            TabTitle = "Open",
            Title = "Open the Shortcuts app",
            Description = "On your iPhone, open Shortcuts. Tap the Automation tab at the bottom (not the Shortcuts gallery).",
            MediaFileName = "step_01.gif"
        },
        new()
        {
            StepNumber = 2,
            TabTitle = "New",
            Title = "Create a personal automation",
            Description = "Tap the + button (or Create Personal Automation). Choose Time of Day as the trigger.",
            MediaFileName = "step_02.gif"
        },
        new()
        {
            StepNumber = 3,
            TabTitle = "Daily",
            Title = "Set it to run every day",
            Description = "Pick Daily, choose the time you want streaks to run (match the time in Profile if you use a fixed schedule), then tap Next.",
            MediaFileName = "step_03.gif"
        },
        new()
        {
            StepNumber = 4,
            TabTitle = "Action",
            Title = "Add the Streak Saver action",
            Description = "Tap Add Action, search for Maintain TikTok Streaks, and select it from Streak Saver.",
            MediaFileName = "step_04.gif"
        },
        new()
        {
            StepNumber = 5,
            TabTitle = "Run",
            Title = "Turn off “Ask Before Running”",
            Description = "On the confirmation screen, disable Ask Before Running so the automation can fire on its own. " +
                "Pick a daily time when your iPhone is usually unlocked — automations fail on a locked phone. " +
                "Streak Saver will come to the foreground briefly while messages send in an invisible browser. Tap Done to save.",
            MediaFileName = "step_05.gif"
        },
        new()
        {
            StepNumber = 6,
            TabTitle = "Widget",
            Title = "Optional: home screen widget",
            Description = "Long-press the home screen → Add Widget → find Streak Saver. The widget runs the same action when you tap it (phone must be unlocked).",
            MediaFileName = "step_06.gif"
        },
        new()
        {
            StepNumber = 7,
            TabTitle = "Limits",
            Title = "iPhone must be unlocked",
            Description = "Personal automations cannot launch Streak Saver while your phone is locked. If you see Unable to launch because the device couldn't be unlocked, " +
                "schedule the automation for when you are usually awake and unlocked, or use Run Now after unlocking. " +
                "This iOS build is experimental — Android is recommended for reliable background streaks. Profile → Why iOS is different for details.",
            MediaFileName = "step_06.gif"
        }
    ];

    public static string MediaAssetPath(string fileName)
        => $"{MediaFolder}/{fileName}";
}
