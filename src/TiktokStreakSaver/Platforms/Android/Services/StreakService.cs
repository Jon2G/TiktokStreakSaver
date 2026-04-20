using System.Diagnostics;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Webkit;
using Android.Runtime;
using AndroidX.Core.App;
using System.Text.Json;
using Android.Content.PM;
using Java.Interop;
using Microsoft.Maui.Controls.Internals;
using RandomUserAgent;
using TiktokStreakSaver;
using TiktokStreakSaver.Models;
using TiktokStreakSaver.Services;
using TiktokStreakSaver.Platforms.Android;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using WebView = Android.Webkit.WebView;

namespace TiktokStreakSaver.Platforms.Android.Services;

[Service(Name = AppConstants.PackageName + ".Services.StreakService", ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public class StreakService : Service
{
    private const string ChannelId = "streak_service_channel";
    private const string ChannelName = "Streak Service";
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

    // ── Burst Mode state (simple 1-5 chunk mode) ──
    private bool _isBurstMode = false;
    private int _burstTargetChunks = 1;
    private int _burstCurrentChunk = 0;

    // ── Burst Chat Mode state (BurstChatService-based) ──
    private BurstChatService? _burstChatService;
    private List<string>? _pendingBurstMessages;
    private int _burstMessageIndex;
    private string? _currentBurstUsername;
    private volatile bool _burstCancelled = false;

    // ── Service lifecycle flags ──
    private bool _isCancelRequested = false;
    private bool _automationStarted = false;
    private bool _needsPageReload = false;

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

        _mainHandler = new Handler(Looper.MainLooper!);
        _settingsService = new SettingsService();
        _burstChatService = new BurstChatService();
        AcquireWakeLock();

        // Start foreground IMMEDIATELY in OnCreate to avoid ANR
        StartForegroundServiceImmediate();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == "STOP_SERVICE")
        {
            _isCancelRequested = true;
            _burstCancelled = true;
            AppLog("SYSTEM", "-", "Service stop requested by user");
            CompleteService(false, "Run stopped by user.");
            return StartCommandResult.NotSticky;
        }

        // Handle Burst Mode flag
        _isBurstMode = intent?.GetBooleanExtra("IsBurstMode", false) ?? false;

        // Ensure we're in foreground mode (in case OnCreate didn't complete it)
        StartForegroundServiceImmediate();

        // ── Run-level mutex: reject if another automation session is already active ──
        lock (_runLock)
        {
            if (_isRunning)
            {
                AppLog("SYSTEM", "-", "OnStartCommand rejected — automation already running");
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
                return StartCommandResult.NotSticky;
            }
            _isRunning = true;
        }

        // Start the WebView automation on main thread
        _mainHandler?.Post(StartWebViewAutomation);

        return StartCommandResult.NotSticky;
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
        ReleaseWakeLock();
        CleanupWebView();
        base.OnDestroy();
    }

    private void AcquireWakeLock()
    {
        var powerManager = (PowerManager?)GetSystemService(PowerService);
        _wakeLock = powerManager?.NewWakeLock(WakeLockFlags.Partial, "TiktokStreakSaver::StreakWakeLock");
        _wakeLock?.Acquire(10 * 60 * 1000); // 10 minutes max
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
            _needsPageReload = false;

            var allEnabled = _settingsService?.GetEnabledFriends() ?? new List<FriendConfig>();
            _currentFriendIndex = 0;
            _runResult = new StreakRunResult();
            _cooldownSkippedCount = 0;
            _logs.Clear();

            // ── Per-friend cooldown: skip friends already messaged today ──
            var today = DateTime.Now.Date;

            _friendsToProcess = new List<FriendConfig>();
            foreach (var friend in allEnabled)
            {
                if (friend.LastMessageSent.HasValue && friend.LastMessageSent.Value.Date == today)
                {
                    // Friend was already messaged today — skip (do NOT add to FriendResults
                    // to avoid inflating successCount in notifications and history)
                    _cooldownSkippedCount++;
                    AppLog("SKIP", $"@{friend.Username}",
                        $"Already messaged today at {friend.LastMessageSent.Value:HH:mm}");
                }
                else
                {
                    _friendsToProcess.Add(friend);
                }
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

            if (!NetworkConnectivity.HasWifiOrCellularValidatedInternet(this))
            {
                CompleteSkippedNoNetwork();
                return;
            }

            UpdateNotification("Preparing automation...");

            //read tiktok_automation.js from assets
            using var resourceStream = await FileSystem.OpenAppPackageFileAsync("tiktok_automation.js");
            using var reader = new StreamReader(resourceStream);
            this._baseScript = await reader.ReadToEndAsync();
            // Minify: strip comment lines, collapse whitespace
            this._baseScript = string.Join("\n", this._baseScript.Split('\n').Where(line => !line.TrimStart().StartsWith("//")));
            this._baseScript = System.Text.RegularExpressions.Regex.Replace(this._baseScript, @"\s+", " ").Trim();

            // Create WebView
            _webView = new WebView(this);
            _webView.Settings.JavaScriptEnabled = true;
            _webView.Settings.DomStorageEnabled = true;
            _webView.Settings.DatabaseEnabled = true;
            _webView.Settings.CacheMode = CacheModes.Normal;

            _webView.Settings.UserAgentString = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";
            _webView.Settings.SetSupportZoom(true);
            _webView.Settings.BuiltInZoomControls = true;

            // Enable cookies
            var cookieManager = CookieManager.Instance;
            cookieManager?.SetAcceptCookie(true);
            cookieManager?.SetAcceptThirdPartyCookies(_webView, true);

            // Set up WebView client
            _webView.SetWebViewClient(new StreakWebViewClient(this));

            // Add JavaScript interface
            _webView.AddJavascriptInterface(new StreakJsInterface(this), "StreakApp");

            // Load TikTok messages page
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
            // Inter-friend page reload: the page was reloaded to reset the
            // TikTok SPA state so the chat list is fresh for the next friend.
            if (_needsPageReload)
            {
                _needsPageReload = false;
                AppLog("NAVIGATION", "-", "Messages page reloaded for next friend");
                _mainHandler?.PostDelayed(ProcessNextFriend, 3000);
                return;
            }

            // Guard: only start the automation chain once (first page load)
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

        // Reset burst chunk state for this friend
        _burstCurrentChunk = 0;
        _burstTargetChunks = _isBurstMode ? new Random().Next(1, 6) : 1;

        AppLog("PROCESS", $"@{friend.Username}", $"Starting ({_burstTargetChunks} chunks)");

        SendCurrentFriendMessage();
    }

    private void SendCurrentFriendMessage()
    {
        if (_isCancelRequested) return;

        var friend = _friendsToProcess![_currentFriendIndex];

        // Only update main notification on the first chunk so we don't spam
        if (_burstCurrentChunk == 0)
        {
            UpdateNotification($"{_currentFriendIndex + 1}/{_friendsToProcess.Count} \u2014 Processing: @{friend.Username}",
                              _currentFriendIndex, _friendsToProcess.Count);
        }

        var message = _isBurstMode
            ? _settingsService?.GetBurstMessageText() ?? "Burst Message"
            : _settingsService?.GetMessageText() ?? SettingsService.DefaultMessage;

        // ── Burst Chat Mode: prepare burst queue if enabled ──
        bool burstEnabled = _burstChatService?.IsEnabled() ?? false;
        if (burstEnabled && _burstCurrentChunk == 0)
        {
            _pendingBurstMessages = _burstChatService!.GenerateBurstMessages(message);
            _burstMessageIndex = 1; // Index 0 is the primary message already sent by the automation script
            _currentBurstUsername = friend.Username;
            _burstCancelled = false;
            AppLog("BURST", $"@{friend.Username}", $"Burst mode active: {_pendingBurstMessages.Count} messages queued");
        }
        else if (!burstEnabled)
        {
            _pendingBurstMessages = null;
            _burstMessageIndex = 0;
            _currentBurstUsername = null;
        }

        // Inject JavaScript to find and message the friend
        _allowSendRetries = true;
        var js = GetFriendMessageScript(friend.Username, message);
        _webView?.EvaluateJavascript(js, null);
    }

    private string GetFriendMessageScript(string username, string message)
    {
        // Escape special characters for JavaScript
        var escapedUsername = username.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
        var escapedMessage = message.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", "\\n");


        var automationScript = this._baseScript.Replace("[UserName]", escapedUsername);
        automationScript = automationScript.Replace("[Message]", escapedMessage);
        return automationScript;
    }

    internal void OnMessageResult(string username, bool success, string error)
    {
        if (_isCancelRequested) return;
        if (_friendsToProcess == null || _settingsService == null) return;

        var friend = _friendsToProcess.FirstOrDefault(f => f.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (friend == null)
        {
            _failureAttemptsForCurrentFriend = 0;
            _currentFriendIndex++;
            _mainHandler?.PostDelayed(ProcessNextFriend, 3000);
            return;
        }

            if (!success)
            {
                _failureAttemptsForCurrentFriend++;
                if (_allowSendRetries && _failureAttemptsForCurrentFriend < MaxSendAttemptsPerFriend)
                {
                    AppLog("RETRY", $"@{username}",
                        $"Attempt {_failureAttemptsForCurrentFriend}/{MaxSendAttemptsPerFriend}: {error}");
                    _mainHandler?.PostDelayed(SendCurrentFriendMessage, 3000);
                    return;
                }

                friend.FailureCount++;
                AppLog("FAIL", $"@{username}", error);

                bool skipUnreachable = _settingsService.GetSkipUnreachableUsers();
                if (skipUnreachable && error == UserNotFoundError)
                {
                    friend.IsEnabled = false;
                    _disabledUsernames.Add($"@{username}");
                    AppLog("DISABLED", $"@{username}", "Auto-disabled — not found in chat list");
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
                _burstCurrentChunk = 0;
                _currentFriendIndex++;
                UpdateNotification($"{_currentFriendIndex}/{_friendsToProcess.Count} : Failed: @{username}", _currentFriendIndex, _friendsToProcess.Count);
                _mainHandler?.PostDelayed(ProcessNextFriend, 3000);
                return;
            }

            // SUCCESS — handle simple burst chunk loop
            _burstCurrentChunk++;
            if (_burstCurrentChunk < _burstTargetChunks)
            {
                int delayMs = new Random().Next(3000, 10000);
                AppLog("BURST", $"@{username}", $"Chunk {_burstCurrentChunk}/{_burstTargetChunks} sent, waiting {delayMs / 1000.0:F1}s");

                if (!_isCancelRequested)
                {
                    _mainHandler?.PostDelayed(SendCurrentFriendMessage, delayMs);
                }
                return; // Do not move to next friend yet
            }

            // All simple burst chunks done — record success
            friend.SuccessCount++;
            friend.LastMessageSent = DateTime.Now;
            AppLog("SUCCESS", $"@{username}", "Message sequence complete");

            _settingsService.UpdateFriend(friend);

            _runResult?.FriendResults.Add(new FriendMessageResult
            {
                FriendId = friend.Id,
                Username = username,
                Success = true,
                ErrorMessage = null
            });

        // ── Burst Chat Mode: queue next burst message if applicable ──
        if (success && !_burstCancelled && _pendingBurstMessages != null &&
            _currentBurstUsername != null &&
            _currentBurstUsername.Equals(username, StringComparison.OrdinalIgnoreCase) &&
            _burstMessageIndex < _pendingBurstMessages.Count)
        {
            var nextBurstMsg = _pendingBurstMessages[_burstMessageIndex];
            _burstMessageIndex++;
            var delay = _burstChatService?.GenerateRandomDelay() ?? 5000;

            AppLog("BURST", $"@{username}",
                $"Sending burst chunk {_burstMessageIndex}/{_pendingBurstMessages.Count} in {delay}ms");

            var totalCount = _friendsToProcess?.Count ?? 0;
            UpdateNotification(
                $"{_currentFriendIndex + 1}/{totalCount} \u2014 @{username} burst {_burstMessageIndex}/{_pendingBurstMessages.Count}",
                _currentFriendIndex, totalCount);

            _mainHandler?.PostDelayed(() =>
            {
                if (_burstCancelled)
                {
                    AppLog("BURST", $"@{username}", "Burst cancelled by user");
                    AdvanceToNextFriend(username);
                    return;
                }
                _allowSendRetries = false;
                var js = GetDirectMessageScript(nextBurstMsg);
                _webView?.EvaluateJavascript(js, null);
            }, delay);
            return;
        }

        // ── Normal flow: advance to next friend ──
        AdvanceToNextFriend(username);
    }

    /// <summary>
    /// Advance the friend index and schedule the next friend processing.
    /// Extracted to share between normal and burst completion paths.
    /// </summary>
    private void AdvanceToNextFriend(string username)
    {
        // Clear burst state
        _pendingBurstMessages = null;
        _burstMessageIndex = 0;
        _currentBurstUsername = null;
        _burstCurrentChunk = 0;

        // Move to next friend
        _currentFriendIndex++;
        _failureAttemptsForCurrentFriend = 0;
        var completedCount = _currentFriendIndex;
        var totalCount = _friendsToProcess?.Count ?? 0;
        var resultText = $"{completedCount}/{totalCount} : Sent to @{username}";
        UpdateNotification(resultText, completedCount, totalCount);

        // Check if there are more friends to process
        if (_currentFriendIndex < (_friendsToProcess?.Count ?? 0))
        {
            // Reload the messages page to reset TikTok's SPA state.
            // After messaging a friend, the WebView stays inside that conversation.
            // The next friend's script would fail because the chat list DOM is stale.
            // OnPageLoaded will detect _needsPageReload and call ProcessNextFriend.
            _needsPageReload = true;
            AppLog("NAVIGATION", "-", "Reloading messages page for next friend...");
            _mainHandler?.PostDelayed(() =>
            {
                _webView?.LoadUrl("https://www.tiktok.com/messages?lang=en");
            }, 2000);
        }
        else
        {
            // All friends processed — call ProcessNextFriend to trigger completion
            _mainHandler?.PostDelayed(ProcessNextFriend, 1000);
        }
    }

    /// <summary>
    /// Cancel any in-progress burst sequence. Remaining burst chunks will be halted
    /// and the service will advance to the next friend on the next callback.
    /// </summary>
    public void CancelBurst()
    {
        _burstCancelled = true;
        AppLog("BURST", _currentBurstUsername ?? "-", "Burst cancellation requested");
    }

    /// <summary>
    /// Generate a JavaScript snippet that types and sends a message in an already-open chat.
    /// Unlike GetFriendMessageScript, this does NOT search for the user — the chat is already focused.
    /// </summary>
    private string GetDirectMessageScript(string message)
    {
        var escapedMessage = message.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", "\\n");
        var escapedUsername = _currentBurstUsername?.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"") ?? "";

        return $@"(function() {{
            var message = '{escapedMessage}';
            var userName = '{escapedUsername}';

            var log = function(msg) {{
                if (typeof StreakApp === 'undefined') {{ console.log(msg); return; }}
                StreakApp.log(msg);
            }};

            var findMessageInput = function() {{
                return document.querySelector('[class*=""DraftEditor-editorContainer""] [contenteditable=""true""]') ||
                    document.querySelector('[class*=""DraftEditor-root""] [contenteditable=""true""]') ||
                    document.querySelector('div[contenteditable=""true""][role=""textbox""]') ||
                    document.querySelector('div[contenteditable=""true""]');
            }};

            var findDraftEditor = function(messageInput) {{
                var key = Object.keys(messageInput).find(function(k) {{
                    return k.startsWith('__reactFiber$') || k.startsWith('__reactInternalInstance$');
                }});
                if (!key) return null;
                var fiber = messageInput[key];
                var current = fiber;
                while (current) {{
                    if (current.stateNode && current.stateNode.editor) return current.stateNode;
                    current = current.return;
                }}
                return null;
            }};

            var sendMessage = function(messageInput) {{
                var sendBtn = document.querySelector('[data-e2e*=""send""]') ||
                              document.querySelector('[data-e2e*=""Send""]') ||
                              document.querySelector('button[type=""submit""]');
                if (sendBtn) {{
                    sendBtn.dispatchEvent(new Event('click', {{ bubbles: true }}));
                    return;
                }}
                messageInput.dispatchEvent(new KeyboardEvent('keydown', {{
                    key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true
                }}));
                messageInput.dispatchEvent(new KeyboardEvent('keyup', {{
                    key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true
                }}));
            }};

            var messageInput = findMessageInput();
            if (!messageInput) {{
                log('Burst: message input not found');
                if (typeof StreakApp !== 'undefined') StreakApp.onMessageSent(userName, false, 'Message input not found during burst');
                return;
            }}

            var draftEditor = findDraftEditor(messageInput);
            if (draftEditor) {{
                draftEditor.focus();
                setTimeout(function() {{
                    var dt = new DataTransfer();
                    dt.setData('text/plain', message);
                    var pe = new ClipboardEvent('paste', {{ bubbles: true, cancelable: true, clipboardData: dt }});
                    try {{ draftEditor._onPaste(pe); }} catch(e) {{ log('Burst paste error: ' + e.message); }}
                    setTimeout(function() {{
                        sendMessage(messageInput);
                        setTimeout(function() {{
                            log('Burst chunk sent: ' + message);
                            if (typeof StreakApp !== 'undefined') StreakApp.onMessageSent(userName, true, '');
                        }}, 1000);
                    }}, 300);
                }}, 200);
            }} else {{
                messageInput.click();
                messageInput.focus();
                setTimeout(function() {{
                    var sel = window.getSelection();
                    var range = document.createRange();
                    range.selectNodeContents(messageInput);
                    range.collapse(false);
                    sel.removeAllRanges();
                    sel.addRange(range);
                    document.execCommand('insertText', false, message);
                    setTimeout(function() {{
                        sendMessage(messageInput);
                        setTimeout(function() {{
                            log('Burst chunk sent (fallback): ' + message);
                            if (typeof StreakApp !== 'undefined') StreakApp.onMessageSent(userName, true, '');
                        }}, 1000);
                    }}, 300);
                }}, 200);
            }}
        }})();";
    }

    private void CompleteSkippedNoNetwork()
    {
        try
        {
            UpdateNotification("No connection - retry in 1 hour");

            if (_runResult != null && _settingsService != null)
            {
                _runResult.Success = false;
                _runResult.ErrorMessage = "Skipped: no network";
                _settingsService.AddRunResult(_runResult);
            }

            StreakScheduler.ScheduleRetryInOneHour(this);

            var finalNotification = new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle("TikTok Streak Saver")
                .SetContentText("No Wi-Fi or cellular data. Will retry in 1 hour.")
                .SetSmallIcon(Resource.Drawable.ic_notification)
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

                if (_isBurstMode)
                {
                    _settingsService.SetBurstLastRunTime(DateTime.Now);
                }
                else
                {
                    _settingsService.SetLastRunTime(DateTime.Now);
                }
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

            var finalNotification = new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle("TikTok Streak Saver")
                .SetContentText(finalText)
                .SetSmallIcon(Resource.Drawable.ic_notification)
                .SetAutoCancel(true)
                .SetPriority(NotificationCompat.PriorityDefault)
                .Build()!;

            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.Notify(NotificationId + 1, finalNotification);

            // Only re-arm the scheduler if scheduling is enabled
            if (_settingsService?.IsScheduled() == true)
                StreakScheduler.ScheduleNextRun(this);
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

