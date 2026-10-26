using System.Text.Json.Serialization;

namespace SocialTennis.Scheduling;

/// <summary>Source-generated serialisation, because the Release publish is trimmed.</summary>
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(Schedule))]
internal sealed partial class ScheduleStorageJsonContext : JsonSerializerContext;
