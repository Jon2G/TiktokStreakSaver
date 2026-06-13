using System.Runtime.InteropServices;
using ObjCRuntime;

namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>
/// Loads StreakEngine.framework explicitly and invokes its C API.
/// DllImport("StreakEngine") alone is unreliable on iOS Simulator even when the framework is embedded.
/// </summary>
internal static class StreakEngineBridge
{
    private delegate void StreakCompletionDelegate([MarshalAs(UnmanagedType.I1)] bool success);
    private delegate void NativeRunDelegate(StreakCompletionDelegate callback);
    private delegate byte NativeConsumePendingRunDelegate();
    private delegate void NativeRegisterShortcutRunHandlerDelegate(NativeShortcutRunHandlerDelegate? handler);
    private delegate void NativeShortcutRunHandlerDelegate();

    private static readonly object LoadLock = new();
    private static IntPtr _libraryHandle;
    private static NativeRunDelegate? _run;
    private static NativeConsumePendingRunDelegate? _consumePendingRun;
    private static NativeRegisterShortcutRunHandlerDelegate? _registerShortcutRunHandler;
    private static string? _loadError;

    private static Action<bool>? _runCompleteAction;
    private static GCHandle _runCompleteActionHandle;
    private static GCHandle _runCallbackDelegateHandle;
    private static GCHandle _shortcutRunHandlerHandle;

    public static bool IsAvailable
    {
        get
        {
            EnsureLoaded();
            return _run != null;
        }
    }

    public static bool ConsumePendingRun()
    {
        if (!EnsureLoaded() || _consumePendingRun == null)
            return false;

        try
        {
            return _consumePendingRun() != 0;
        }
        catch (Exception ex)
        {
            IosRunTrace.Append($"native_consume_pending_failed {ex.Message}");
            return false;
        }
    }

    public static bool RegisterShortcutRunHandler(Action handler)
    {
        if (!EnsureLoaded() || _registerShortcutRunHandler == null)
            return false;

        try
        {
            if (_shortcutRunHandlerHandle.IsAllocated)
                _shortcutRunHandlerHandle.Free();

            var nativeHandler = new NativeShortcutRunHandlerDelegate(() => handler());
            _shortcutRunHandlerHandle = GCHandle.Alloc(nativeHandler);
            _registerShortcutRunHandler(nativeHandler);
            return true;
        }
        catch (Exception ex)
        {
            IosRunTrace.Append($"native_register_shortcut_failed {ex.Message}");
            return false;
        }
    }

    /// <returns>False when the native framework could not be loaded or invoked.</returns>
    public static bool RunStreak(Action<bool> onComplete)
    {
        if (onComplete is null)
            return false;

        if (!EnsureLoaded() || _run is null)
        {
            IosRunTrace.Append($"native_engine_missing {_loadError ?? "StreakEngine"}");
            return false;
        }

        FreeActiveCallbackHandles();

        try
        {
            _runCompleteAction = onComplete;
            _runCompleteActionHandle = GCHandle.Alloc(onComplete);

            var callback = new StreakCompletionDelegate(NativeRunCompleted);
            _runCallbackDelegateHandle = GCHandle.Alloc(callback);

            _run(callback);
            return true;
        }
        catch (Exception ex)
        {
            FreeActiveCallbackHandles();
            IosRunTrace.Append($"native_run_failed {ex.Message}");
            return false;
        }
    }

    [MonoPInvokeCallback(typeof(StreakCompletionDelegate))]
    private static void NativeRunCompleted(bool success)
    {
        try
        {
            IosRunTrace.Append($"native_callback success={success}");

            var action = _runCompleteAction;
            if (action is null)
            {
                IosRunTrace.Append("native_callback missing_action");
                return;
            }

            void Invoke() => action(success);

            if (MainThread.IsMainThread)
                Invoke();
            else
                MainThread.BeginInvokeOnMainThread(Invoke);
        }
        catch (Exception ex)
        {
            IosRunTrace.Append($"native_callback_error {ex}");
        }
        finally
        {
            FreeActiveCallbackHandles();
        }
    }

    private static void FreeActiveCallbackHandles()
    {
        _runCompleteAction = null;
        if (_runCompleteActionHandle.IsAllocated)
            _runCompleteActionHandle.Free();
        if (_runCallbackDelegateHandle.IsAllocated)
            _runCallbackDelegateHandle.Free();
    }

    private static bool EnsureLoaded()
    {
        if (_run != null)
            return true;

        lock (LoadLock)
        {
            if (_run != null)
                return true;

            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Frameworks", "StreakEngine.framework", "StreakEngine");
                if (!File.Exists(path))
                {
                    _loadError = $"framework not found at {path}";
                    return false;
                }

                _libraryHandle = NativeLibrary.Load(path);
                var runPtr = NativeLibrary.GetExport(_libraryHandle, "StreakEngine_Run");
                var consumePtr = NativeLibrary.GetExport(_libraryHandle, "StreakEngine_ConsumePendingRun");
                var registerShortcutPtr = NativeLibrary.GetExport(_libraryHandle, "StreakSaver_RegisterShortcutRunHandler");

                _run = Marshal.GetDelegateForFunctionPointer<NativeRunDelegate>(runPtr);
                _consumePendingRun = Marshal.GetDelegateForFunctionPointer<NativeConsumePendingRunDelegate>(consumePtr);
                _registerShortcutRunHandler = Marshal.GetDelegateForFunctionPointer<NativeRegisterShortcutRunHandlerDelegate>(registerShortcutPtr);
                IosRunTrace.Append($"native_engine_loaded path={path}");
                return true;
            }
            catch (Exception ex)
            {
                _loadError = ex.Message;
                return false;
            }
        }
    }
}
