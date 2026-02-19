using System.Text.Json.Serialization;

namespace ScanEventWorker.Infrastructure.ApiClient;

public sealed class ScanEventApiResponse
{
    [JsonPropertyName("ScanEvents")]
    public List<ScanEventDto> ScanEvents { get; set; } = [];
}

public sealed class ScanEventDto
{
    [JsonPropertyName("EventId")]
    public long EventId { get; set; }

    [JsonPropertyName("ParcelId")]
    public int ParcelId { get; set; }

    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("CreatedDateTimeUtc")]
    public DateTimeOffset CreatedDateTimeUtc { get; set; }

    [JsonPropertyName("StatusCode")]
    public string StatusCode { get; set; } = string.Empty;

    [JsonPropertyName("User")]
    public UserDto? User { get; set; }
}

public sealed class UserDto
{
    [JsonPropertyName("RunId")]
    public string RunId { get; set; } = string.Empty;
}
