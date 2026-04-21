using Android.Content;
using Android.Net;

namespace TiktokStreakSaver.Platforms.Android.Services;

/// <summary>
/// Wi‑Fi / cellular reachability for deciding whether to start WebView work.
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
internal static class NetworkConnectivity
{
    /// <summary>
    /// True when the active network is Wi‑Fi or cellular and reports <see cref="NetCapability.Internet"/>.
    /// Does not require <see cref="NetCapability.Validated"/> — requiring validation caused immediate skips
    /// on many devices until the system finished captive-portal / DNS checks.
    /// </summary>
    public static bool HasWifiOrCellularInternet(Context context)
    {
        var cm = (ConnectivityManager?)context.GetSystemService(Context.ConnectivityService);
        if (cm == null) return false;

        var network = cm.ActiveNetwork;
        if (network == null) return false;

        var caps = cm.GetNetworkCapabilities(network);
        if (caps == null) return false;

        if (!caps.HasCapability(NetCapability.Internet)) return false;

        var onWifi = caps.HasTransport(TransportType.Wifi);
        var onCellular = caps.HasTransport(TransportType.Cellular);

        return onWifi || onCellular;
    }

    /// <summary>Stricter check when you need the OS to have marked the network as validated.</summary>
    public static bool HasWifiOrCellularValidatedInternet(Context context)
    {
        var cm = (ConnectivityManager?)context.GetSystemService(Context.ConnectivityService);
        if (cm == null) return false;

        var network = cm.ActiveNetwork;
        if (network == null) return false;

        var caps = cm.GetNetworkCapabilities(network);
        if (caps == null) return false;

        if (!caps.HasCapability(NetCapability.Internet)) return false;
        if (!caps.HasCapability(NetCapability.Validated)) return false;

        var onWifi = caps.HasTransport(TransportType.Wifi);
        var onCellular = caps.HasTransport(TransportType.Cellular);

        return onWifi || onCellular;
    }
}
