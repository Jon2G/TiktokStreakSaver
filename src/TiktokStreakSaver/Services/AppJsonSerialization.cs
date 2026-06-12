using System.Text.Json;
using System.Text.Json.Serialization;

namespace TiktokStreakSaver.Services;

/// <summary>Shared JSON options for MAUI ↔ native Swift interop.</summary>
public static class AppJsonSerialization
{
    /// <summary>Friends, settings, run history — PascalCase keys to match Swift CodingKeys.</summary>
    public static JsonSerializerOptions Settings { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new FlexibleNullableDateTimeConverter(), new FlexibleDateTimeConverter(), new FlexibleNullableDurationConverter() }
    };

    /// <summary>Exported cookies — camelCase keys for Swift ExportedCookie.</summary>
    public static JsonSerializerOptions Cookies { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}

/// <summary>Reads ISO-8601 strings or legacy Swift reference-date numbers.</summary>
public sealed class FlexibleNullableDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly DateTime AppleReferenceUtc = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            return string.IsNullOrEmpty(s) ? null : DateTime.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var num))
            return AppleReferenceUtc.AddSeconds(num);

        throw new JsonException($"Unexpected token for DateTime?: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value.ToUniversalTime().ToString("O"));
    }
}

public sealed class FlexibleDateTimeConverter : JsonConverter<DateTime>
{
    private readonly FlexibleNullableDateTimeConverter _inner = new();

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => _inner.Read(ref reader, typeof(DateTime?), options) ?? default;

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => _inner.Write(writer, value, options);
}

/// <summary>Reads Swift duration strings like "12.3s", ISO time spans, or numeric seconds.</summary>
public sealed class FlexibleNullableDurationConverter : JsonConverter<TimeSpan?>
{
    public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s))
                return null;

            if (TryParseSwiftSeconds(s, out var seconds))
                return TimeSpan.FromSeconds(seconds);

            return TimeSpan.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var num))
            return TimeSpan.FromSeconds(num);

        throw new JsonException($"Unexpected token for TimeSpan?: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value.ToString("c", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static bool TryParseSwiftSeconds(string text, out double seconds)
    {
        seconds = 0;
        var trimmed = text.Trim();
        if (trimmed.Length > 1 && trimmed.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^1].Trim();

        return double.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out seconds);
    }
}
