namespace TiktokStreakSaver.Services;

/// <summary>
/// Burst Chat Mode: multiple short messages per friend with randomized delays.
/// </summary>
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
public class BurstChatService
{
    private const string BurstEnabledKey = "burst_chat_enabled";
    private const string BurstCountKey = "burst_chat_count";
    private const string BurstMinDelayMsKey = "burst_min_delay_ms";
    private const string BurstMaxDelayMsKey = "burst_max_delay_ms";

    public const int MinBurstCount = 2;
    public const int MaxBurstCount = 5;
    public const int DefaultBurstCount = 4;
    public const int MinDelayMs = 3000;
    public const int MaxDelayMs = 10000;
    public const int AbsoluteMinDelayMs = 500;
    public const int AbsoluteMaxDelayMs = 180_000;

    public static readonly string[] BurstChunks =
    {
        "Streak",
        "\uD83D\uDD25",
        "keeping it alive",
        "hey",
        "\uD83D\uDC4B"
    };

    public bool IsEnabled() => Preferences.Get(BurstEnabledKey, false);

    public void SetEnabled(bool enabled) => Preferences.Set(BurstEnabledKey, enabled);

    public int GetBurstCount()
    {
        var count = Preferences.Get(BurstCountKey, DefaultBurstCount);
        return Math.Clamp(count, MinBurstCount, MaxBurstCount);
    }

    public void SetBurstCount(int count) =>
        Preferences.Set(BurstCountKey, Math.Clamp(count, MinBurstCount, MaxBurstCount));

    public int GetMinDelayMs()
    {
        var v = Preferences.Get(BurstMinDelayMsKey, MinDelayMs);
        return Math.Clamp(v, AbsoluteMinDelayMs, AbsoluteMaxDelayMs);
    }

    public void SetMinDelayMs(int ms)
    {
        var v = Math.Clamp(ms, AbsoluteMinDelayMs, AbsoluteMaxDelayMs);
        Preferences.Set(BurstMinDelayMsKey, v);
        if (GetMaxDelayMs() < v)
            SetMaxDelayMs(v);
    }

    public int GetMaxDelayMs()
    {
        var v = Preferences.Get(BurstMaxDelayMsKey, MaxDelayMs);
        return Math.Clamp(v, AbsoluteMinDelayMs, AbsoluteMaxDelayMs);
    }

    public void SetMaxDelayMs(int ms)
    {
        var v = Math.Clamp(ms, AbsoluteMinDelayMs, AbsoluteMaxDelayMs);
        Preferences.Set(BurstMaxDelayMsKey, v);
        if (GetMinDelayMs() > v)
            SetMinDelayMs(v);
    }

    public List<string> GenerateBurstMessages(string primaryMessage)
    {
        var count = GetBurstCount();
        var messages = new List<string>(count);
        var rng = new Random();
        messages.Add(primaryMessage);

        var availableChunks = BurstChunks
            .Where(c => !c.Equals(primaryMessage, StringComparison.OrdinalIgnoreCase))
            .ToList();

        for (int i = 1; i < count; i++)
        {
            if (availableChunks.Count > 0)
            {
                var idx = rng.Next(availableChunks.Count);
                messages.Add(availableChunks[idx]);
                availableChunks.RemoveAt(idx);
            }
            else
            {
                messages.Add(BurstChunks[rng.Next(BurstChunks.Length)]);
            }
        }

        return messages;
    }

    public int GenerateRandomDelay()
    {
        var lo = GetMinDelayMs();
        var hi = GetMaxDelayMs();
        if (lo > hi)
            (lo, hi) = (hi, lo);
        var rng = new Random();
        return rng.Next(lo, hi + 1);
    }
}
