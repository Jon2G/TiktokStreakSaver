using System.Text.Json.Serialization;
using TiktokStreakSaver.Models;

namespace TiktokStreakSaver.Services;

[JsonSerializable(typeof(FriendConfig))]
[JsonSerializable(typeof(List<FriendConfig>))]
[JsonSerializable(typeof(StreakRunResult))]
[JsonSerializable(typeof(List<StreakRunResult>))]
[JsonSerializable(typeof(FriendMessageResult))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    Converters = [typeof(FlexibleNullableDateTimeConverter), typeof(FlexibleDateTimeConverter), typeof(FlexibleNullableDurationConverter)])]
[Microsoft.Maui.Controls.Internals.Preserve(AllMembers = true)]
internal partial class AppJsonContext : JsonSerializerContext;
