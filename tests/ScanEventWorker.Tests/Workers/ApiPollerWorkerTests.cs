using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ScanEventWorker.Contracts;
using ScanEventWorker.Domain;
using ScanEventWorker.Workers;

namespace ScanEventWorker.Tests.Workers;

public class ApiPollerWorkerTests
{
    private readonly IScanEventApiClient _apiClient = Substitute.For<IScanEventApiClient>();
    private readonly IScanEventRepository _repository = Substitute.For<IScanEventRepository>();
    private readonly IMessageQueue _queue = Substitute.For<IMessageQueue>();
    private readonly ILogger<ApiPollerWorker> _logger = Substitute.For<ILogger<ApiPollerWorker>>();

    private ApiPollerWorker CreateWorker() =>
        new(_apiClient, _repository, _queue,
            Options.Create(new ScanEventApiOptions
            {
                BatchSize = 100,
                PollingIntervalSeconds = 0,
                ErrorRetryIntervalSeconds = 0
            }),
            _logger);

    private static ScanEvent MakeScanEvent(long eventId, int parcelId = 1) =>
        new(new ScanEventWorker.Domain.EventId(eventId), new ParcelId(parcelId), "STATUS",
            DateTimeOffset.UtcNow, string.Empty, string.Empty);

    [Fact(Timeout = 5000)]
    public async Task OnStartup_ReadsLastEventIdFromRepository()
    {
        var cts = new CancellationTokenSource();

        _repository.GetLastEventIdAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                cts.Cancel();
                return Task.FromResult(0L);
            });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;
        await worker.StopAsync(CancellationToken.None);

        await _repository.Received(1).GetLastEventIdAsync(Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 5000)]
    public async Task WhenApiReturnsEvents_SendsEachEventToQueue()
    {
        var cts = new CancellationTokenSource();
        var events = new List<ScanEvent> { MakeScanEvent(1), MakeScanEvent(2) };

        _repository.GetLastEventIdAsync(Arg.Any<CancellationToken>()).Returns(0L);
        _apiClient.GetScanEventsAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScanEvent>>.Success(events));
        _repository.UpdateLastEventIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;
        await worker.StopAsync(CancellationToken.None);

        await _queue.Received(2).SendAsync(Arg.Any<ScanEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 5000)]
    public async Task WhenApiReturnsEvents_AdvancesLastEventIdToMax()
    {
        var cts = new CancellationTokenSource();
        var events = new List<ScanEvent> { MakeScanEvent(5), MakeScanEvent(10) };

        _repository.GetLastEventIdAsync(Arg.Any<CancellationToken>()).Returns(0L);
        _apiClient.GetScanEventsAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScanEvent>>.Success(events));
        _repository.UpdateLastEventIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;
        await worker.StopAsync(CancellationToken.None);

        await _repository.Received(1).UpdateLastEventIdAsync(10L, Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 5000)]
    public async Task WhenApiFails_DoesNotAdvanceLastEventId()
    {
        var cts = new CancellationTokenSource();

        _repository.GetLastEventIdAsync(Arg.Any<CancellationToken>()).Returns(0L);
        _apiClient.GetScanEventsAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                cts.Cancel();
                return Task.FromResult(Result<IReadOnlyList<ScanEvent>>.Failure("timeout"));
            });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        try { await worker.ExecuteTask!; } catch (OperationCanceledException) { }
        await worker.StopAsync(CancellationToken.None);

        await _repository.DidNotReceive().UpdateLastEventIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 5000)]
    public async Task WhenApiReturnsEmpty_DoesNotAdvanceLastEventId()
    {
        var cts = new CancellationTokenSource();

        _repository.GetLastEventIdAsync(Arg.Any<CancellationToken>()).Returns(0L);
        _apiClient.GetScanEventsAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                cts.Cancel();
                return Task.FromResult(
                    Result<IReadOnlyList<ScanEvent>>.Success(Array.Empty<ScanEvent>()));
            });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        try { await worker.ExecuteTask!; } catch (OperationCanceledException) { }
        await worker.StopAsync(CancellationToken.None);

        await _repository.DidNotReceive().UpdateLastEventIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        await _queue.DidNotReceive().SendAsync(Arg.Any<ScanEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 5000)]
    public async Task WhenSendAsyncThrowsMidBatch_DoesNotAdvanceLastEventId()
    {
        var cts = new CancellationTokenSource();
        var events = new List<ScanEvent> { MakeScanEvent(1), MakeScanEvent(2), MakeScanEvent(3) };
        var callCount = 0;

        _repository.GetLastEventIdAsync(Arg.Any<CancellationToken>()).Returns(0L);
        _apiClient.GetScanEventsAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScanEvent>>.Success(events));
        _queue.SendAsync(Arg.Any<ScanEvent>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                if (callCount == 2)
                    throw new Exception("SQS unavailable");
                return Task.CompletedTask;
            });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(4)).ContinueWith(_ => { });
        await worker.StopAsync(CancellationToken.None);

        await _repository.DidNotReceive().UpdateLastEventIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 5000)]
    public async Task WhenApiReturnsEvents_CallsUpdateExactlyOnce()
    {
        var cts = new CancellationTokenSource();
        var events = new List<ScanEvent> { MakeScanEvent(1), MakeScanEvent(2), MakeScanEvent(3) };

        _repository.GetLastEventIdAsync(Arg.Any<CancellationToken>()).Returns(0L);
        _apiClient.GetScanEventsAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScanEvent>>.Success(events));
        _repository.UpdateLastEventIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;
        await worker.StopAsync(CancellationToken.None);

        await _repository.Received(1).UpdateLastEventIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }
}
