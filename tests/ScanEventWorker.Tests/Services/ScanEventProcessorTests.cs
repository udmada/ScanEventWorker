using Microsoft.Extensions.Logging;
using NSubstitute;
using ScanEventWorker.Contracts;
using ScanEventWorker.Domain;
using ScanEventWorker.Services;

namespace ScanEventWorker.Tests.Services;

public class ScanEventProcessorTests
{
    private readonly IScanEventRepository _repository = Substitute.For<IScanEventRepository>();
    private readonly ILogger<ScanEventProcessor> _logger = Substitute.For<ILogger<ScanEventProcessor>>();
    private readonly ScanEventProcessor _processor;

    public ScanEventProcessorTests()
    {
        _processor = new ScanEventProcessor(_repository, _logger);
    }

    [Fact]
    public async Task ProcessSingleAsync_ValidEvent_UpsertsCalled()
    {
        var scanEvent = CreateScanEvent(1, 100, ScanEventTypes.Pickup);

        var result = await _processor.ProcessSingleAsync(scanEvent, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _repository.Received(1).UpsertParcelSummaryAsync(scanEvent, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSingleAsync_RepositoryThrows_ReturnsFailure()
    {
        var scanEvent = CreateScanEvent(1, 100, ScanEventTypes.Delivery);
        _repository.UpsertParcelSummaryAsync(scanEvent, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("DB connection failed")));

        var result = await _processor.ProcessSingleAsync(scanEvent, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("DB connection failed", result.Error);
    }

    [Fact]
    public async Task ProcessBatchAsync_MixedResults_ReturnsProcessedCount()
    {
        var event1 = CreateScanEvent(1, 100, ScanEventTypes.Pickup);
        var event2 = CreateScanEvent(2, 101, ScanEventTypes.Delivery);
        var event3 = CreateScanEvent(3, 102, ScanEventTypes.Status);

        _repository.UpsertParcelSummaryAsync(event2, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("Transient error")));

        var result = await _processor.ProcessBatchAsync([event1, event2, event3], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value); // event1 and event3 succeeded
    }

    [Fact]
    public async Task ProcessBatchAsync_EmptyList_ReturnsZero()
    {
        var result = await _processor.ProcessBatchAsync([], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }

    private static ScanEvent CreateScanEvent(long eventId, int parcelId, string type) =>
        new(new ScanEventWorker.Domain.EventId(eventId), new ParcelId(parcelId), type,
            DateTimeOffset.UtcNow, string.Empty, string.Empty);
}
