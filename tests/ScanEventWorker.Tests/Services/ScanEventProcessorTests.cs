using Microsoft.Extensions.Logging;
using NSubstitute;
using ScanEventWorker.Contracts;
using ScanEventWorker.Domain;
using ScanEventWorker.Services;
using EventId = ScanEventWorker.Domain.EventId;

namespace ScanEventWorker.Tests.Services;

public class ScanEventProcessorTests
{
    private readonly ILogger<ScanEventProcessor> _logger = Substitute.For<ILogger<ScanEventProcessor>>();
    private readonly ScanEventProcessor _processor;
    private readonly IScanEventRepository _repository = Substitute.For<IScanEventRepository>();

    public ScanEventProcessorTests()
    {
        _processor = new ScanEventProcessor(_repository, _logger);
    }

    [Fact]
    public async Task ProcessSingleAsync_ValidEvent_UpsertsCalled()
    {
        ScanEvent scanEvent = CreateScanEvent(1, 100, ScanEventTypes.Pickup);

        Result<bool> result = await _processor.ProcessSingleAsync(scanEvent, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _repository.Received(1).UpsertParcelSummaryAsync(scanEvent, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSingleAsync_RepositoryThrows_ReturnsFailure()
    {
        ScanEvent scanEvent = CreateScanEvent(1, 100, ScanEventTypes.Delivery);
        _ = _repository.UpsertParcelSummaryAsync(scanEvent, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("DB connection failed")));

        Result<bool> result = await _processor.ProcessSingleAsync(scanEvent, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("DB connection failed", result.Error);
    }

    [Fact]
    public async Task ProcessBatchAsync_MixedResults_ReturnsProcessedCount()
    {
        ScanEvent event1 = CreateScanEvent(1, 100, ScanEventTypes.Pickup);
        ScanEvent event2 = CreateScanEvent(2, 101, ScanEventTypes.Delivery);
        ScanEvent event3 = CreateScanEvent(3, 102, ScanEventTypes.Status);

        _ = _repository.UpsertParcelSummaryAsync(event2, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("Transient error")));

        Result<int> result = await _processor.ProcessBatchAsync([event1, event2, event3], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value); // event1 and event3 succeeded
    }

    [Fact]
    public async Task ProcessBatchAsync_EmptyList_ReturnsZero()
    {
        Result<int> result = await _processor.ProcessBatchAsync([], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }

    private static ScanEvent CreateScanEvent(long eventId, int parcelId, string type) =>
        new(new EventId(eventId), new ParcelId(parcelId), type,
            DateTimeOffset.UtcNow, string.Empty, string.Empty);
}
