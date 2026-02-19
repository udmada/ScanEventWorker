using System.Text.Json.Serialization;
using ScanEventWorker.Domain;

namespace ScanEventWorker.Infrastructure.ApiClient;

[JsonSerializable(typeof(ScanEventApiResponse))]
[JsonSerializable(typeof(ScanEvent))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class ApiJsonContext : JsonSerializerContext;
