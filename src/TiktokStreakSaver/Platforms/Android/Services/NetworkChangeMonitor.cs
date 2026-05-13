using Android.Content;
using Android.Net;
using TiktokStreakSaver.Services;

namespace TiktokStreakSaver.Platforms.Android.Services;

/// <summary>
/// Listens for connectivity changes via <see cref="ConnectivityManager"/> and, when the
/// device regains Wi‑Fi or cellular internet AND the last streak run failed because of
/// no-network, triggers a recovery run through <see cref="StreakScheduler.RunNow"/>.
///
/// Registered once from <see cref="MainApplication.OnCreate"/>. The callback only lives
/// while the app process is alive — the AlarmManager / BootReceiver chain still owns the
/// worst-case path when the process has been killed.
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
internal static class NetworkChangeMonitor
{
    /// <summary>
    /// Minimum time between two network-triggered recovery attempts. Prevents flapping
    /// networks (e.g. weak Wi‑Fi rapidly toggling) from spamming service starts.
    /// </summary>
    private static readonly TimeSpan RecoveryThrottle = TimeSpan.FromMinutes(30);

    private static readonly object _lock = new();
    private static DateTime _lastRecoveryAttemptUtc = DateTime.MinValue;
    private static ConnectivityManager.NetworkCallback? _callback;
    private static bool _registered;

    /// <summary>
    /// Register the default-network callback. Safe to call multiple times; only the
    /// first call has an effect.
    /// </summary>
    public static void Register(Context context)
    {
        lock (_lock)
        {
            if (_registered) return;

            var cm = (ConnectivityManager?)context.GetSystemService(Context.ConnectivityService);
            if (cm == null)
            {
                System.Diagnostics.Debug.WriteLine("NetworkChangeMonitor: ConnectivityManager unavailable, skipping registration");
                return;
            }

            try
            {
                _callback = new RecoveryNetworkCallback(context.ApplicationContext ?? context);
                cm.RegisterDefaultNetworkCallback(_callback);
                _registered = true;
                System.Diagnostics.Debug.WriteLine("NetworkChangeMonitor: registered default-network callback");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NetworkChangeMonitor: registration failed — {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Called from the network callback when the system reports a usable Wi‑Fi or
    /// cellular network. Applies all the guard conditions and, if everything aligns,
    /// kicks off a recovery run.
    /// </summary>
    private static void OnNetworkAvailable(Context context)
    {
        try
        {
            var settings = new SettingsService();

            if (!settings.IsScheduled()) return;
            if (StreakService.IsRunning) return;
            if (!settings.GetLastRunFailed()) return;
            if (settings.GetTodayRetryCount() >= SettingsService.MaxRetriesPerDay) return;
            if (!NetworkConnectivity.HasWifiOrCellularInternet(context)) return;

            lock (_lock)
            {
                var nowUtc = DateTime.UtcNow;
                if (nowUtc - _lastRecoveryAttemptUtc < RecoveryThrottle)
                {
                    System.Diagnostics.Debug.WriteLine("NetworkChangeMonitor: throttled — last attempt too recent");
                    return;
                }
                _lastRecoveryAttemptUtc = nowUtc;
            }

            var reason = settings.GetLastRunFailureReason() ?? "unknown";
            System.Diagnostics.Debug.WriteLine(
                $"NetworkChangeMonitor: network restored, starting recovery run (last reason: {reason})");

            StreakScheduler.RunNow(context);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NetworkChangeMonitor.OnNetworkAvailable: {ex.Message}");
        }
    }

    private sealed class RecoveryNetworkCallback : ConnectivityManager.NetworkCallback
    {
        private readonly Context _context;

        public RecoveryNetworkCallback(Context context)
        {
            _context = context;
        }

        public override void OnAvailable(Network network)
        {
            base.OnAvailable(network);
            OnNetworkAvailable(_context);
        }

        public override void OnCapabilitiesChanged(Network network, NetworkCapabilities networkCapabilities)
        {
            base.OnCapabilitiesChanged(network, networkCapabilities);

            // Some devices fire OnAvailable before Internet capability is set; re-check here.
            if (!networkCapabilities.HasCapability(NetCapability.Internet)) return;

            var onWifi = networkCapabilities.HasTransport(TransportType.Wifi);
            var onCellular = networkCapabilities.HasTransport(TransportType.Cellular);
            if (!onWifi && !onCellular) return;

            OnNetworkAvailable(_context);
        }
    }
}
