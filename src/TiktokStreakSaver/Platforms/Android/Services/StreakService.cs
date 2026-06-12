using Android.App;
using Android.Content;
using Android.OS;
using Android.Webkit;
using Android.Runtime;
using AndroidX.Core.App;
using Android.Content.PM;
using Java.Interop;
using TiktokStreakSaver.Models;
using TiktokStreakSaver.Services;
using TiktokStreakSaver.Platforms.Android;
using WebView = Android.Webkit.WebView;

namespace TiktokStreakSaver.Platforms.Android.Services;

[Service(Name = AppConstants.PackageName + ".Services.StreakService", ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public class StreakService : Service
{
    private const string ChannelId = "streak_service_channel";
    private const string ChannelName = "Streak Service";
    /// <summary>Ongoing foreground + completion / offline alerts that should be visible (not Low importance).</summary>
    private const string StatusChannelId = "streak_status_channel";
    private const string StatusChannelName = "Streak status";
    private const int NotificationId = 1001;

    private WebView? _webView;
    private Handler? _mainHandler;
    private SettingsService? _settingsService;
    private List<FriendConfig>? _friendsToProcess;
    private int _currentFriendIndex;
    private StreakRunResult? _runResult;
    private PowerManager.WakeLock? _wakeLock;
    private string _baseScript = string.Empty;
    private readonly List<string> _disabledUsernames = new();
    private const string UserNotFoundError = "User not found in chat list";

    // ── Randomized Normal Messages state ──
    private List<string>? _shuffledNormalMessages;
    private int _normalMessageIndex = 0;

    private readonly Random _rng = new();

    // ── Service lifecycle flags ──
    private bool _isCancelRequested = false;
    private bool _automationStarted = false;

    // ── Run-level mutex: prevents concurrent automation sessions ──
    private static volatile bool _isRunning = false;
    private static readonly object _runLock = new();

    /// <summary>
    /// True while an automation session is active. Checked by StreakScheduler.RunNow
    /// and OnStartCommand to prevent overlapping runs.
    /// </summary>
    public static bool IsRunning => _isRunning;

    private int _cooldownSkippedCount = 0;

    private int _failureAttemptsForCurrentFriend;
    private const int MaxSendAttemptsPerFriend = 4;
    private bool _allowSendRetries = true;

    private static List<string> _logs = new();

    public static List<string> GetLogs()
    {
        return _logs ?? new List<string>();
    }

    public static void ClearLogs()
    {
        _logs = new List<string>();
    }

    private static void AppLog(string phase, string username, string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] [{phase}] [{username}] {message}";
        _logs.Add(entry);
        System.Diagnostics.Debug.WriteLine(entry);
    }

    public override void OnCreate()
    {
        base.OnCreate();

        // Create notification channel FIRST before anything else
        CreateNotificationChannel();
        CreateStatusNotificationChannel();

        _mainHandler = new Handler(Looper.MainLooper!);
        _settingsService = new SettingsService();
        AcquireWakeLock();

        // Start foreground IMMEDIATELY in OnCreate to avoid ANR
        StartForegroundServiceImmediate();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == "STOP_SERVICE")
        {
            _isCancelRequested = true;
            AppLog("SYSTEM", "-", "Service stop requested by user");
            CompleteService(false, "Run stopped by user.");
            return StartCommandResult.NotSticky;
        }

        // Ensure we're in foreground mode (in case OnCreate didn't complete it)
        StartForegroundServiceImmediate();

        // ── Run-level mutex: ignore duplicate starts while a session is active ──
        // IMPORTANT: Do not StopSelf() here — a second start (e.g. alarm + Run now) would kill the in-flight run.
        lock (_runLock)
        {
            if (_isRunning)
            {
                AppLog("SYSTEM", "-", "OnStartCommand ignored — automation already running");
                return StartCommandResult.NotSticky;
            }
            _isRunning = true;
        }

        // Start the WebView automation on main thread
        _mainHandler?.Post(StartWebViewAutomation);

        // Sticky: if Android kills the service mid-run, the system will recreate
        // the service so the run can resume.
        return StartCommandResult.Sticky;
    }

    private void StartForegroundServiceImmediate()
    {
        try
        {
            var notification = CreateNotification("Preparing to send streaks...");

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                // Android 10+ requires specifying the foreground service type
                StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
            }
            else
            {
                StartForeground(NotificationId, notification);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StartForeground error: {ex.Message}");
        }
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        // Safety net: release the run-level mutex if the service is destroyed
        // without CompleteService being called (e.g. system kill while WebView is loading).
        lock (_runLock)
        {
            _isRunning = false;
        }
        ReleaseWakeLock();
        CleanupWebView();
        base.OnDestroy();
    }

    private void AcquireWakeLock()
    {
        var powerManager = (PowerManager?)GetSystemService(PowerService);
        _wakeLock = powerManager?.NewWakeLock(WakeLockFlags.Partial, "TiktokStreakSaver::StreakWakeLock");
        // 30 minute ceiling — generous upper bound for a normal run with a large friend list.
        _wakeLock?.Acquire(30L * 60 * 1000);
    }

    private void ReleaseWakeLock()
    {
        if (_wakeLock?.IsHeld == true)
        {
            _wakeLock.Release();
        }
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            if (notificationManager == null) return;

            // Check if channel already exists
            var existingChannel = notificationManager.GetNotificationChannel(ChannelId);
            if (existingChannel != null) return;

            var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.Low)
            {
                Description = "Notification channel for streak service"
            };
            channel.SetShowBadge(false);

            notificationManager?.CreateNotificationChannel(channel);
        }
    }

    private void CreateStatusNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            if (notificationManager == null) return;

            if (notificationManager.GetNotificationChannel(StatusChannelId) != null) return;

            var channel = new NotificationChannel(StatusChannelId, StatusChannelName, NotificationImportance.Default)
            {
                Description = "Run results and connection issues"
            };
            notificationManager.CreateNotificationChannel(channel);
        }
    }

    private Notification CreateNotification(string message)
    {
        var intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        var pendingIntent = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("TikTok Streak Saver")
            .SetContentText(message)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(message))
            .SetSmallIcon(Resource.Drawable.ic_notification)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate)
            .SetCategory(NotificationCompat.CategoryService)
            .SetPriority(NotificationCompat.PriorityLow)
            .SetProgress(0, 0, true);

        return builder.Build()!;
    }

    private void UpdateNotification(string message, int progress = -1, int max = 0)
    {
        var intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        var pendingIntent = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("TikTok Streak Saver")
            .SetContentText(message)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(message))
            .SetSmallIcon(Resource.Drawable.ic_notification)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate)
            .SetCategory(NotificationCompat.CategoryService)
            .SetPriority(NotificationCompat.PriorityLow);

        if (progress >= 0 && max > 0)
            builder!.SetProgress(max, progress, false);
        else
            builder!.SetProgress(0, 0, true);

        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        notificationManager?.Notify(NotificationId, builder.Build()!);
    }

    private async void StartWebViewAutomation()
    {
        try
        {
            _automationStarted = false;

            _currentFriendIndex = 0;
            _runResult = new StreakRunResult();
            _cooldownSkippedCount = 0;
            _logs.Clear();

            _friendsToProcess = new List<FriendConfig>();

            var allEnabled = _settingsService?.GetEnabledFriends() ?? new List<FriendConfig>();
            var today = DateTime.Now.Date;

            foreach (var friend in allEnabled)
            {
                if (friend.LastMessageSent.HasValue && friend.LastMessageSent.Value.Date == today)
                {
                    _cooldownSkippedCount++;
                    AppLog("SKIP", $"@{friend.Username}",
                        $"Already messaged today at {friend.LastMessageSent.Value:HH:mm}");
                }
                else
                {
                    _friendsToProcess.Add(friend);
                }
            }

            // Initialize randomized message pool if user opted in.
            if (_settingsService?.GetRandomizeNormalMessages() == true)
            {
                _shuffledNormalMessages = new List<string>(SettingsService.BuiltInStreakMessages);
                ShuffleList(_shuffledNormalMessages);
                _normalMessageIndex = 0;
                AppLog("SYSTEM", "-", $"Randomized messages enabled: {_shuffledNormalMessages.Count} variants loaded");
            }
            else
            {
                _shuffledNormalMessages = null;
            }

            AppLog("SYSTEM", "-",
                $"Starting automation: {_friendsToProcess.Count} to process, {_cooldownSkippedCount} skipped (already sent today)");

            if (_friendsToProcess.Count == 0)
            {
                var msg = _cooldownSkippedCount > 0
                    ? $"All {_cooldownSkippedCount} friends already messaged today"
                    : "No friends configured";
                CompleteService(_cooldownSkippedCount > 0, msg);
                return;
            }

            if (!NetworkConnectivity.HasWifiOrCellularInternet(this))
            {
                CompleteSkippedNoNetwork();
                return;
            }

            UpdateNotification("Preparing automation...");

            using var resourceStream = await FileSystem.OpenAppPackageFileAsync("tiktok_automation.js");
            using var reader = new StreamReader(resourceStream);
            this._baseScript = await reader.ReadToEndAsync();
            this._baseScript = string.Join("\n", this._baseScript.Split('\n').Where(line => !line.TrimStart().StartsWith("//")));
            this._baseScript = System.Text.RegularExpressions.Regex.Replace(this._baseScript, @"\s+", " ").Trim();

            _webView = new WebView(this);
            _webView.Settings.JavaScriptEnabled = true;
            _webView.Settings.DomStorageEnabled = true;
            _webView.Settings.DatabaseEnabled = true;
            _webView.Settings.CacheMode = CacheModes.Normal;

            // Reuse the UA captured at login time so cookies stay valid; fall back to a modern Chrome desktop UA.
            var sessionService = new SessionService();
            var loginUa = sessionService.GetLoginUserAgent()
                ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
            _webView.Settings.UserAgentString = loginUa;

            _webView.Settings.SetSupportZoom(true);
            _webView.Settings.BuiltInZoomControls = true;

            // Give the headless WebView a real viewport so TikTok's virtualized chat list
            // actually renders its children. Without dimensions the WebView is 0x0 and
            // lazy-rendered conversation items never appear.
            _webView.Settings.UseWideViewPort = true;
            _webView.Settings.LoadWithOverviewMode = true;
            _webView.Layout(0, 0, 1920, 1080);

            var cookieManager = CookieManager.Instance;
            cookieManager?.SetAcceptCookie(true);
            cookieManager?.SetAcceptThirdPartyCookies(_webView, true);

            _webView.SetWebViewClient(new StreakWebViewClient(this));
            _webView.AddJavascriptInterface(new StreakJsInterface(this), "StreakApp");
            _webView.LoadUrl("https://www.tiktok.com/messages?lang=en");

            _mainHandler!.PostDelayed(() =>
            {
                if (!(_webView?.Url ?? "").Contains("tiktok.com/messages"))
                {
                    _webView?.LoadUrl("https://www.tiktok.com/messages?lang=en");
                    _mainHandler.PostDelayed(() =>
                    {
                        if (!(_webView?.Url ?? "").Contains("tiktok.com/messages"))
                        {
                            CompleteService(false, "Could not navigate to tiktok.com/messages");
                        }
                    }, 5000);
                }
            }, 5000);
        }
        catch (Exception ex)
        {
            CompleteService(false, $"Error starting WebView: {ex.Message}");
        }
    }

    private void CleanupWebView()
    {
        _mainHandler?.Post(() =>
        {
            _webView?.StopLoading();
            _webView?.Destroy();
            _webView = null;
        });
    }

    internal void OnPageLoaded(string url)
    {
        // Check if we're on the messages page
        if (url.Contains("tiktok.com/messages"))
        {
            // Guard: only start the automation chain once (first page load).
            // Subsequent friends reuse the same SPA session — inject script only (no full reload).
            if (_automationStarted) return;
            _automationStarted = true;

            UpdateNotification("Connecting to TikTok...");
            AppLog("NAVIGATION", "-", "Messages page ready");
            // Wait a bit for the page to fully render, then start automation
            _mainHandler?.PostDelayed(ProcessNextFriend, 3000);
        }
        else if (url.Contains("login"))
        {
            AppLog("NAVIGATION", "-", "TikTok login required");
            // User needs to login
            CompleteService(false, "TikTok login required. Please login via the app first.");
        }
    }

    private void ProcessNextFriend()
    {
        if (_isCancelRequested) return;

        // When "Skip Unreachable Users" is OFF, abort the entire run on any per-user failure
        bool skipUnreachable = _settingsService?.GetSkipUnreachableUsers() ?? false;
        if (!skipUnreachable && _runResult is not null && _runResult.Failed)
        {
            CompleteService(false, $"Run stopped: {_runResult.ErrorMessage ?? _runResult.FriendsErrorMessage}");
            return;
        }

        if (_friendsToProcess == null || _currentFriendIndex >= _friendsToProcess.Count)
        {
            // All friends processed — mark success only if every friend succeeded
            var allSucceeded = _runResult?.FriendResults.All(r => r.Success) ?? false;
            var completionMessage = allSucceeded
                ? "All messages sent successfully"
                : $"{_runResult?.FriendResults.Count(r => r.Success) ?? 0} of {_runResult?.FriendResults.Count ?? 0} sent";
            CompleteService(allSucceeded, completionMessage);
            return;
        }

        var friend = _friendsToProcess[_currentFriendIndex];

        var logTarget = friend.IsGroup ? $"Group: {friend.DisplayName}" : $"@{friend.Username}";
        AppLog("PROCESS", logTarget, "Starting regular messaging");

        SendCurrentFriendMessage();
    }

    private void SendCurrentFriendMessage()
    {
        if (_isCancelRequested) return;

        var friend = _friendsToProcess![_currentFriendIndex];
        string message;

        if (_shuffledNormalMessages != null && _shuffledNormalMessages.Count > 0)
        {
            message = _shuffledNormalMessages[_normalMessageIndex % _shuffledNormalMessages.Count];
            _normalMessageIndex++;
            // Reshuffle when pool is exhausted so the same per-run sequence is not repeated.
            if (_normalMessageIndex >= _shuffledNormalMessages.Count)
            {
                ShuffleList(_shuffledNormalMessages);
                _normalMessageIndex = 0;
            }
        }
        else
        {
            message = _settingsService?.GetMessageText() ?? SettingsService.DefaultMessage;
        }

        var displayLabel = friend.IsGroup ? friend.DisplayName : $"@{friend.Username}";
        UpdateNotification($"{_currentFriendIndex + 1}/{_friendsToProcess.Count} \u2014 Processing: {displayLabel}",
            _currentFriendIndex, _friendsToProcess.Count);

        // For groups TikTok has no @handle, so we match the chat header by display name instead.
        var target = friend.IsGroup ? friend.DisplayName : friend.Username;
        if (string.IsNullOrWhiteSpace(target))
        {
            AppLog("FAIL", "-", friend.IsGroup ? "Group name is empty" : "Username is empty");
            _currentFriendIndex++;
            _mainHandler?.PostDelayed(ProcessNextFriend, 1000);
            return;
        }

        _allowSendRetries = true;
        var js = GetFriendMessageScript(target, message, friend.IsGroup);
        _webView?.EvaluateJavascript(js, null);
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private string GetFriendMessageScript(string target, string message, bool isGroup)
    {
        // Escape special characters for JavaScript string literals
        target ??= string.Empty;
        message ??= string.Empty;
        var escapedTarget = target.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
        var escapedMessage = message.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", "\\n");

        var automationScript = this._baseScript.Replace("[UserName]", escapedTarget);
        automationScript = automationScript.Replace("[Message]", escapedMessage);
        automationScript = automationScript.Replace("[IsGroup]", isGroup ? "true" : "false");
        return automationScript;
    }

    internal void OnMessageResult(string username, bool success, string error)
    {
        if (_isCancelRequested) return;
        if (_friendsToProcess == null || _settingsService == null) return;

        // The JS callback reports the target it was given: a username for DMs, or the
        // display name for groups (groups have no @handle on TikTok).
        var friend = _friendsToProcess.FirstOrDefault(f =>
            (f.IsGroup && f.DisplayName.Equals(username, StringComparison.OrdinalIgnoreCase)) ||
            (!f.IsGroup && f.Username.Equals(username, StringComparison.OrdinalIgnoreCase)));

        if (friend == null)
        {
            // Target reported by JS doesn't match any friend/group in the list. This can happen
            // when TikTok returns a different casing/format. Retry the current friend
            // a few times rather than silently advancing past it.
            AppLog("WARN", $"@{username}",
                "Target from JS callback did not match any entry in the list. Retrying current friend...");
            _failureAttemptsForCurrentFriend++;
            if (_failureAttemptsForCurrentFriend < MaxSendAttemptsPerFriend)
            {
                _mainHandler?.PostDelayed(SendCurrentFriendMessage, 3000);
            }
            else
            {
                AppLog("FAIL", $"@{username}", "Max retries exceeded for unmatched username");
                _failureAttemptsForCurrentFriend = 0;
                _currentFriendIndex++;
                _mainHandler?.PostDelayed(ProcessNextFriend, 3000);
            }
            return;
        }

        var label = friend.IsGroup ? $"Group: {friend.DisplayName}" : $"@{username}";
        if (!success)
        {
            _failureAttemptsForCurrentFriend++;
            if (_allowSendRetries && _failureAttemptsForCurrentFriend < MaxSendAttemptsPerFriend)
            {
                AppLog("RETRY", label,
                    $"Attempt {_failureAttemptsForCurrentFriend}/{MaxSendAttemptsPerFriend}: {error}");
                _mainHandler?.PostDelayed(SendCurrentFriendMessage, 3000);
                return;
            }

            friend.FailureCount++;
            AppLog("FAIL", label, error);

            bool skipUnreachable = _settingsService.GetSkipUnreachableUsers();
            if (skipUnreachable && error == UserNotFoundError)
            {
                friend.IsEnabled = false;
                _disabledUsernames.Add(label);
                AppLog("DISABLED", label, "Auto-disabled — not found in chat list");
            }
            _settingsService.UpdateFriend(friend);

            _runResult?.FriendResults.Add(new FriendMessageResult
            {
                FriendId = friend.Id,
                Username = username,
                Success = false,
                ErrorMessage = error
            });

            _failureAttemptsForCurrentFriend = 0;
            _currentFriendIndex++;
            UpdateNotification($"{_currentFriendIndex}/{_friendsToProcess.Count} : Failed: {label}", _currentFriendIndex, _friendsToProcess.Count);
            _mainHandler?.PostDelayed(ProcessNextFriend, 3000);
            return;
        }

        friend.SuccessCount++;
        friend.LastMessageSent = DateTime.Now;
        AppLog("SUCCESS", label, "Message sent");

        _settingsService.UpdateFriend(friend);

        _runResult?.FriendResults.Add(new FriendMessageResult
        {
            FriendId = friend.Id,
            Username = username,
            Success = true,
            ErrorMessage = null
        });

        AdvanceToNextFriend(username);
    }

    private void AdvanceToNextFriend(string username)
    {
        var prevFriend = _friendsToProcess != null && _currentFriendIndex < _friendsToProcess.Count
            ? _friendsToProcess[_currentFriendIndex] : null;
        var sentLabel = prevFriend?.IsGroup == true ? prevFriend.DisplayName : $"@{username}";

        _currentFriendIndex++;
        _failureAttemptsForCurrentFriend = 0;
        var completedCount = _currentFriendIndex;
        var totalCount = _friendsToProcess?.Count ?? 0;
        var resultText = $"{completedCount}/{totalCount} : Sent to {sentLabel}";
        UpdateNotification(resultText, completedCount, totalCount);

        if (_currentFriendIndex < totalCount)
        {
            AppLog("NAVIGATION", "-", "Next friend — injecting without reloading /messages");
            _mainHandler?.PostDelayed(ProcessNextFriend, 3000);
        }
        else
            _mainHandler?.PostDelayed(ProcessNextFriend, 1000);
    }

    private PendingIntent CreateMainActivityPendingIntent()
    {
        var intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        return PendingIntent.GetActivity(this, 1, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }

    private void CompleteSkippedNoNetwork()
    {
        try
        {
            if (_runResult != null && _settingsService != null)
            {
                _runResult.Success = false;
                _runResult.ErrorMessage = "Skipped: no Wi‑Fi or mobile data";
                _settingsService.AddRunResult(_runResult);
            }

            var attempt = StreakScheduler.TryScheduleRetryOrGiveUp(this, SettingsService.FailureReasonNoNetwork);
            var max = SettingsService.MaxRetriesPerDay;

            string title;
            string body;
            if (attempt > 0)
            {
                title = "TikTok Streak Saver — offline";
                body = $"No Wi‑Fi or mobile data. Streak run skipped; retrying in 1 hour (attempt {attempt}/{max}).";
                UpdateNotification($"No Wi‑Fi or mobile data — retry in 1 hour ({attempt}/{max})");
            }
            else
            {
                title = "TikTok Streak Saver — gave up for today";
                body = $"No Wi‑Fi or mobile data after {max} retries. Will try again on the next scheduled run.";
                UpdateNotification($"No Wi‑Fi or mobile data — {max} retries exhausted");
            }

            var finalNotification = new NotificationCompat.Builder(this, StatusChannelId)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(body))
                .SetSmallIcon(Resource.Drawable.ic_notification)
                .SetContentIntent(CreateMainActivityPendingIntent())
                .SetAutoCancel(true)
                .SetPriority(NotificationCompat.PriorityDefault)
                .Build()!;

            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.Notify(NotificationId + 1, finalNotification);
        }
        finally
        {
            lock (_runLock)
            {
                _isRunning = false;
            }

            CleanupWebView();
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
        }
    }

    private void CompleteService(bool success, string message)
    {
        try
        {
            // Update run result
            if (_runResult != null && _settingsService != null)
            {
                _runResult.Success = success;
                _runResult.ErrorMessage = success ? null : message;
                _settingsService.AddRunResult(_runResult);
                _settingsService.SetLastRunTime(DateTime.Now);
            }

            // Show completion notification
            var successCount = _runResult?.FriendResults.Count(r => r.Success) ?? 0;
            var totalSent = _runResult?.FriendResults.Count ?? 0;
            var skippedCount = totalSent - successCount;

            // Build human-readable summary including cooldown-skipped friends
            var cooldownNote = _cooldownSkippedCount > 0
                ? $", {_cooldownSkippedCount} already sent"
                : string.Empty;

            string finalText;
            if (success)
            {
                finalText = $"Done : {successCount}/{totalSent} sent successfully{cooldownNote}";
            }
            else if (totalSent > 0 && successCount > 0)
            {
                if (_disabledUsernames.Count > 0)
                    finalText = $"Done : {successCount}/{totalSent} sent, {_disabledUsernames.Count} disabled ({string.Join(", ", _disabledUsernames)}){cooldownNote}";
                else
                    finalText = $"Done : {successCount}/{totalSent} sent, {skippedCount} skipped{cooldownNote}";
            }
            else
            {
                if (_disabledUsernames.Count > 0)
                    finalText = $"Done : 0/{totalSent} sent, {_disabledUsernames.Count} disabled ({string.Join(", ", _disabledUsernames)}){cooldownNote}";
                else if (totalSent > 0)
                    finalText = $"Done : 0/{totalSent} sent, {skippedCount} failed{cooldownNote}";
                else
                    finalText = $"Stopped : {message}";
            }

            var finalNotification = new NotificationCompat.Builder(this, StatusChannelId)
                .SetContentTitle("TikTok Streak Saver")
                .SetContentText(finalText)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(finalText))
                .SetSmallIcon(Resource.Drawable.ic_notification)
                .SetContentIntent(CreateMainActivityPendingIntent())
                .SetAutoCancel(true)
                .SetPriority(NotificationCompat.PriorityDefault)
                .Build()!;

            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.Notify(NotificationId + 1, finalNotification);

            // Re-arm the scheduler if scheduling is enabled. Either:
            //   - everything succeeded → normal next-run slot, retry counter reset
            //   - anything failed → 1-hour retry (up to MaxRetriesPerDay), then normal slot
            if (_settingsService?.IsScheduled() == true)
            {
                bool allSucceeded = success
                    && (_runResult?.FriendResults.Count == 0
                        || _runResult.FriendResults.All(r => r.Success));

                if (allSucceeded)
                {
                    _settingsService.ResetTodayRetryCount();
                    _settingsService.SetLastRunFailed(false, null);
                    StreakScheduler.ScheduleNextRun(this);
                }
                else
                {
                    var attempt = StreakScheduler.TryScheduleRetryOrGiveUp(this, SettingsService.FailureReasonSendError);
                    if (attempt > 0)
                        AppLog("SYSTEM", "-", $"Run had errors — scheduled hourly retry {attempt}/{SettingsService.MaxRetriesPerDay}");
                    else
                        AppLog("SYSTEM", "-", $"Run had errors — retry budget exhausted, normal next-run slot scheduled");
                }
            }

            AppLog("SYSTEM", "-", $"Run complete: {(success ? "Success" : message)}");
        }
        finally
        {
            // ── Clear the run-level mutex on ALL exit paths ──
            lock (_runLock)
            {
                _isRunning = false;
            }

            CleanupWebView();
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
        }
    }

    /// <summary>
    /// WebView client for handling page events
    /// </summary>
    [Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
    private class StreakWebViewClient : WebViewClient
    {
        private readonly StreakService _service;

        public StreakWebViewClient(StreakService service)
        {
            _service = service;
        }

        public override void OnPageFinished(WebView? view, string? url)
        {
            base.OnPageFinished(view, url);
            if (!string.IsNullOrEmpty(url))
            {
                _service.OnPageLoaded(url);
            }
        }

        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request)
        {
            if (request?.Url is not null)
            {
                if ((request.Url.EncodedSchemeSpecificPart ?? "").StartsWith("//aweme"))
                {
                    return true;
                }
            }
            // Allow navigation within TikTok
            return false;
        }
    }

    /// <summary>
    /// JavaScript interface for communication from WebView
    /// </summary>
    [Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
    private class StreakJsInterface : Java.Lang.Object
    {
        private readonly StreakService _service;

        public StreakJsInterface(StreakService service)
        {
            _service = service;
        }

        [JavascriptInterface]
        [Export("onMessageSent")]
        public void OnMessageSent(string username, bool success, string error)
        {
            _service._mainHandler?.Post(() => _service.OnMessageResult(username, success, error));
        }

        [JavascriptInterface]
        [Export("log")]
        public void Log(string message)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            StreakService._logs.Add(entry);
        }
    }
}

