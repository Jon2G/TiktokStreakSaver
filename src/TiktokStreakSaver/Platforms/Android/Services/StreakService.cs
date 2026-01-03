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
using TiktokStreakSaver.Models;
using TiktokStreakSaver.Services;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using WebView = Android.Webkit.WebView;

namespace TiktokStreakSaver.Platforms.Android.Services;

[Service(Name = "com.jon2g.tiktokstreaksaver.Services.StreakService", ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
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
    private string BaseScript = string.Empty;

    public override void OnCreate()
    {
        base.OnCreate();

        // Create notification channel FIRST before anything else
        CreateNotificationChannel();

        _mainHandler = new Handler(Looper.MainLooper!);
        _settingsService = new SettingsService();
        AcquireWakeLock();

        // Start foreground IMMEDIATELY in OnCreate to avoid ANR
        StartForegroundServiceImmediate();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Ensure we're in foreground mode (in case OnCreate didn't complete it)
        StartForegroundServiceImmediate();

        // Start the WebView automation on main thread
        _mainHandler?.Post(StartWebViewAutomation);

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
            .SetSmallIcon(Resource.Drawable.ic_notification)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate)
            .SetCategory(NotificationCompat.CategoryService)
            .SetPriority(NotificationCompat.PriorityLow)
            .SetProgress(0, 0, true);

        return builder.Build();
    }

    private void UpdateNotification(string message, int progress = -1, int max = 0)
    {
        var intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        var pendingIntent = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("TikTok Streak Saver")
            .SetContentText(message)
            .SetSmallIcon(Resource.Drawable.ic_notification)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate)
            .SetCategory(NotificationCompat.CategoryService)
            .SetPriority(NotificationCompat.PriorityLow);

        if (progress >= 0 && max > 0)
        {
            builder.SetProgress(max, progress, false);
        }
        else
        {
            builder.SetProgress(0, 0, true);
        }

        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        notificationManager?.Notify(NotificationId, builder.Build());
    }

    private async void StartWebViewAutomation()
    {
        try
        {
            _friendsToProcess = _settingsService?.GetEnabledFriends() ?? new List<FriendConfig>();
            _currentFriendIndex = 0;
            _runResult = new StreakRunResult();

            if (_friendsToProcess.Count == 0)
            {
                CompleteService(false, "No friends configured");
                return;
            }

            UpdateNotification($"Starting... (0/{_friendsToProcess.Count})");

            //read tiktok_automation.js from assets
            using var resourceStream = await FileSystem.OpenAppPackageFileAsync("tiktok_automation.js");
            using var reader = new StreamReader(resourceStream);
            this.BaseScript = await reader.ReadToEndAsync();
            //Minify
            //Remove lines starting with //
            this.BaseScript = string.Join("\n", this.BaseScript.Split('\n').Where(line => !line.TrimStart().StartsWith("//")));
            //Remove new lines and extra spaces
            this.BaseScript = System.Text.RegularExpressions.Regex.Replace(this.BaseScript, @"\s+", " ");
            this.BaseScript = this.BaseScript.Replace("\n", " ").Replace("\r", " ");
            this.BaseScript = this.BaseScript.Trim();

            // Create WebView
            _webView = new WebView(this);
            _webView.Settings.JavaScriptEnabled = true;
            _webView.Settings.DomStorageEnabled = true;
            _webView.Settings.DatabaseEnabled = true;
            _webView.Settings.CacheMode = CacheModes.Normal;

            _webView.Settings.UserAgentString = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";
            _webView.Settings.BuiltInZoomControls = true;
            _webView.Settings.BuiltInZoomControls = true;
            _webView.Settings.SetSupportZoom(true);
            _webView.Settings.BuiltInZoomControls = true;

            //_webView.Settings.UserAgentString = RandomUa.RandomUserAgent;

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
                if (!_webView.Url!.Contains("tiktok.com/messages"))
                {
                    _webView.LoadUrl("https://www.tiktok.com/messages?lang=en");
                    _mainHandler.PostDelayed(() =>
                    {
                        if (!_webView.Url.Contains("tiktok.com/messages"))
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
        UpdateNotification($"Navigating... [{url}]");
        // Check if we're on the messages page
        if (url.Contains("tiktok.com/messages"))
        {
            // Wait a bit for the page to fully render, then start automation
            _mainHandler?.PostDelayed(ProcessNextFriend, 3000);
        }
        else if (url.Contains("login"))
        {
            // User needs to login
            CompleteService(false, "TikTok login required. Please login via the app first.");
        }
    }

    private void ProcessNextFriend()
    {
        if (_runResult is not null && _runResult.Failed)
        {
            CompleteService(false, $"Previous run failed: {_runResult.ErrorMessage ?? _runResult.FriendsErrorMessage}");
            return;
        }
        if (_friendsToProcess == null || _currentFriendIndex >= _friendsToProcess.Count)
        {
            // All friends processed
            CompleteService(true, "All messages sent successfully");
            return;
        }

        var friend = _friendsToProcess[_currentFriendIndex];
        UpdateNotification($"Sending to {friend.DisplayName ?? friend.Username}... ({_currentFriendIndex + 1}/{_friendsToProcess.Count})",
                          _currentFriendIndex, _friendsToProcess.Count);

        var message = _settingsService?.GetMessageText() ?? SettingsService.DefaultMessage;

        // Inject JavaScript to find and message the friend
        var js = GetFriendMessageScript(friend.Username, message);
        _webView?.EvaluateJavascript(js, null);
    }

    private string GetFriendMessageScript(string username, string message)
    {
        // Escape special characters for JavaScript
        var escapedUsername = username.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
        var escapedMessage = message.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", "\\n");


        var automationScript = this.BaseScript.Replace("[UserName]", escapedUsername);
        automationScript = automationScript.Replace("[Message]", escapedMessage);

        return automationScript;
    }

    internal void OnMessageResult(string username, bool success, string error)
    {
        if (_friendsToProcess == null || _settingsService == null) return;

        var friend = _friendsToProcess.FirstOrDefault(f => f.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (friend != null)
        {
            // Update friend stats
            if (success)
            {
                friend.SuccessCount++;
                friend.LastMessageSent = DateTime.Now;
            }
            else
            {
                friend.FailureCount++;
            }
            _settingsService.UpdateFriend(friend);

            // Add to run result
            _runResult?.FriendResults.Add(new FriendMessageResult
            {
                FriendId = friend.Id,
                Username = username,
                Success = success,
                ErrorMessage = success ? null : error
            });
        }

        // Move to next friend after a delay
        _currentFriendIndex++;
        _mainHandler?.PostDelayed(ProcessNextFriend, 3000);
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

            // Show completion notification briefly
            var finalNotification = new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle("TikTok Streak Saver")
                .SetContentText(success ? "Streaks sent successfully!" : $"Failed: {message}")
                .SetSmallIcon(Resource.Drawable.ic_notification)
                .SetAutoCancel(true)
                .SetPriority(NotificationCompat.PriorityDefault)
                .Build();

            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.Notify(NotificationId + 1, finalNotification);

            // Schedule next run
            StreakScheduler.ScheduleNextRun(this);
        }
        finally
        {
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
            Debugger.Log(0,"DEBUG",message);
            Console.WriteLine(message);
        }

    }
}



