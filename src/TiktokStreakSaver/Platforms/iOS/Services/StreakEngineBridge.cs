using System.Runtime.InteropServices;

namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>
/// P/Invoke bridge to StreakEngineObjC C API exported from the native framework.
/// </summary>
internal static class StreakEngineBridge
{
    private delegate void StreakCompletionDelegate(bool success);

    [DllImport("StreakEngine", EntryPoint = "StreakEngine_ConsumePendingRun")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool ConsumePendingRunInternal();

    [DllImport("StreakEngine", EntryPoint = "StreakEngine_Run")]
    private static extern void RunInternal(StreakCompletionDelegate callback);

    public static bool ConsumePendingRun()
    {
        try
        {
            return ConsumePendingRunInternal();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    public static void RunStreak(Action<bool> onComplete)
    {
        try
        {
            RunInternal(success => onComplete(success));
        }
        catch (DllNotFoundException)
        {
            onComplete(false);
        }
    }
}
