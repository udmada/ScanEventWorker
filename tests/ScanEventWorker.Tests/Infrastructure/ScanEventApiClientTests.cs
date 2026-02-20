using ScanEventWorker.Domain;
using ScanEventWorker.Infrastructure.ApiClient;

namespace ScanEventWorker.Tests.Infrastructure;

public class ScanEventApiClientTests
{
    [Fact]
    public void MapToDomain_ValidDto_ReturnsSuccess()
    {
        var dto = new ScanEventDto
        {
            EventId = 42,
            ParcelId = 123,
            Type = "PICKUP",
            CreatedDateTimeUtc = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero),
            StatusCode = "OK",
            User = new UserDto { RunId = "run-1" }
        };

        var result = ScanEventApiClient.MapToDomain(dto);

        Assert.True(result.IsSuccess);
        Assert.Equal(new EventId(42), result.Value.EventId);
        Assert.Equal(new ParcelId(123), result.Value.ParcelId);
        Assert.Equal("PICKUP", result.Value.Type);
        Assert.Equal("run-1", result.Value.RunId);
        Assert.Equal("OK", result.Value.StatusCode);
    }

    [Fact]
    public void MapToDomain_ZeroEventId_ReturnsFailure()
    {
        var dto = new ScanEventDto { EventId = 0, ParcelId = 1, Type = "STATUS" };

        var result = ScanEventApiClient.MapToDomain(dto);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid EventId", result.Error);
    }

    [Fact]
    public void MapToDomain_NegativeParcelId_ReturnsFailure()
    {
        var dto = new ScanEventDto { EventId = 1, ParcelId = -5, Type = "STATUS" };

        var result = ScanEventApiClient.MapToDomain(dto);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid ParcelId", result.Error);
    }

    [Fact]
    public void MapToDomain_EmptyType_ReturnsFailure()
    {
        var dto = new ScanEventDto { EventId = 1, ParcelId = 1, Type = "" };

        var result = ScanEventApiClient.MapToDomain(dto);

        Assert.False(result.IsSuccess);
        Assert.Contains("Missing Type", result.Error);
    }

    [Fact]
    public void MapToDomain_NullUser_DefaultsRunIdToEmpty()
    {
        var dto = new ScanEventDto
        {
            EventId = 1,
            ParcelId = 1,
            Type = "DELIVERY",
            User = null
        };

        var result = ScanEventApiClient.MapToDomain(dto);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Value.RunId);
    }

    [Fact]
    public void MapToDomain_NullStatusCode_DefaultsToEmpty()
    {
        var dto = new ScanEventDto
        {
            EventId = 1,
            ParcelId = 1,
            Type = "STATUS",
            StatusCode = null!
        };

        var result = ScanEventApiClient.MapToDomain(dto);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Value.StatusCode);
    }

    [Fact]
    public void MapToDomain_UnknownType_StillSucceeds()
    {
        var dto = new ScanEventDto
        {
            EventId = 1,
            ParcelId = 1,
            Type = "UNKNOWN_TYPE",
            User = new UserDto { RunId = "run-1" }
        };

        var result = ScanEventApiClient.MapToDomain(dto);

        Assert.True(result.IsSuccess);
        Assert.Equal("UNKNOWN_TYPE", result.Value.Type);
    }
}
