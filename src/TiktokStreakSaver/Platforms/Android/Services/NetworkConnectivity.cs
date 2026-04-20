using Android.Content;
using Android.Net;

namespace TiktokStreakSaver.Platforms.Android.Services;

/// <summary>
/// Checks for validated internet on Wi‑Fi or cellular.
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
internal static class NetworkConnectivity
{
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
