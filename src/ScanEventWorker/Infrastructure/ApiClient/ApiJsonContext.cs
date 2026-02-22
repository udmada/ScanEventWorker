using System.Text.Json.Serialization;
using ScanEventWorker.Domain;

namespace ScanEventWorker.Infrastructure.ApiClient;

// CamelCase policy applies to ScanEvent (SQS serialisation/deserialisation) - symmetrical both ways.
// ScanEventApiResponse/ScanEventDto use explicit [JsonPropertyName] overrides and are unaffected.
// Do NOT add [JsonPropertyName] to ScanEvent properties - they inherit camelCase from this policy.
[JsonSerializable(typeof(ScanEventApiResponse))]
[JsonSerializable(typeof(ScanEvent))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class ApiJsonContext : JsonSerializerContext;
