namespace TiktokStreakSaver.Platforms.iOS.Services;

/// <summary>
/// Invokes the native StreakEngine runner (App Intent logic) from the MAUI app.
/// </summary>
public static class IosStreakRunner
{
    private static bool _isRunning;

    public static bool IsRunning => _isRunning;

    public static Task<bool> RunNowAsync()
    {
        if (_isRunning)
            return Task.FromResult(false);

        _isRunning = true;
        var tcs = new TaskCompletionSource<bool>();

        StreakEngineBridge.RunStreak(success =>
        {
            _isRunning = false;
            tcs.TrySetResult(success);
        });

        return tcs.Task;
    }
}
