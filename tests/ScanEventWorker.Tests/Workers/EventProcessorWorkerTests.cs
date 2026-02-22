using Microsoft.Extensions.Logging;
using NSubstitute;
using ScanEventWorker.Contracts;
using ScanEventWorker.Domain;
using ScanEventWorker.Workers;

namespace ScanEventWorker.Tests.Workers;

public class EventProcessorWorkerTests
{
    private readonly IMessageQueue _queue = Substitute.For<IMessageQueue>();
    private readonly IScanEventProcessor _processor = Substitute.For<IScanEventProcessor>();
    private readonly ILogger<EventProcessorWorker> _logger = Substitute.For<ILogger<EventProcessorWorker>>();

    private EventProcessorWorker CreateWorker() => new(_queue, _processor, _logger);

    private static ScanEvent MakeScanEvent(long eventId, int parcelId = 1) =>
        new(new ScanEventWorker.Domain.EventId(eventId), new ParcelId(parcelId), "STATUS",
            DateTimeOffset.UtcNow, string.Empty, string.Empty);

    private static QueueMessage<ScanEvent> MakeMessage(long eventId, string receiptHandle) =>
        new(MakeScanEvent(eventId), receiptHandle);

    [Fact(Timeout = 5000)]
    public async Task WhenProcessingSucceeds_DeletesMessage()
    {
        var cts = new CancellationTokenSource();
        var msg = MakeMessage(1, "receipt-1");

        _queue.ReceiveAsync<ScanEvent>(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<QueueMessage<ScanEvent>> { msg });
        _processor.ProcessSingleAsync(Arg.Any<ScanEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        _queue.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => { cts.Cancel(); return Task.CompletedTask; });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;
        await worker.StopAsync(CancellationToken.None);

        await _queue.Received(1).DeleteAsync("receipt-1", Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 5000)]
    public async Task WhenProcessingFails_DoesNotDeleteMessage()
    {
        var cts = new CancellationTokenSource();
        var msg = MakeMessage(1, "receipt-1");

        _queue.ReceiveAsync<ScanEvent>(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<QueueMessage<ScanEvent>> { msg });
        _processor.ProcessSingleAsync(Arg.Any<ScanEvent>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => { cts.Cancel(); return Task.FromResult(Result<bool>.Failure("error")); });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;
        await worker.StopAsync(CancellationToken.None);

        await _queue.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 5000)]
    public async Task WhenQueueReturnsEmpty_DoesNotCallProcessor()
    {
        var cts = new CancellationTokenSource();
        var callCount = 0;

        _queue.ReceiveAsync<ScanEvent>(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                if (++callCount >= 2) cts.Cancel();
                return Task.FromResult<IReadOnlyList<QueueMessage<ScanEvent>>>(
                    Array.Empty<QueueMessage<ScanEvent>>());
            });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;
        await worker.StopAsync(CancellationToken.None);

        await _processor.DidNotReceive().ProcessSingleAsync(Arg.Any<ScanEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 5000)]
    public async Task WhenMultipleMessagesReceived_ProcessesAll()
    {
        var cts = new CancellationTokenSource();
        var messages = new List<QueueMessage<ScanEvent>>
        {
            MakeMessage(1, "receipt-1"),
            MakeMessage(2, "receipt-2"),
            MakeMessage(3, "receipt-3"),
        };
        var deleteCount = 0;

        _queue.ReceiveAsync<ScanEvent>(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(messages);
        _processor.ProcessSingleAsync(Arg.Any<ScanEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        _queue.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                if (++deleteCount == 3) cts.Cancel();
                return Task.CompletedTask;
            });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;
        await worker.StopAsync(CancellationToken.None);

        await _processor.Received(3).ProcessSingleAsync(Arg.Any<ScanEvent>(), Arg.Any<CancellationToken>());
        await _queue.Received(3).DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 5000)]
    public async Task WhenMixedResults_OnlySuccessfulMessagesAreDeleted()
    {
        var cts = new CancellationTokenSource();
        var messages = new List<QueueMessage<ScanEvent>>
        {
            MakeMessage(1, "receipt-1"),
            MakeMessage(2, "receipt-2"),
            MakeMessage(3, "receipt-3"),
        };
        var processCount = 0;
        var deleteCount = 0;

        _queue.ReceiveAsync<ScanEvent>(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(messages);
        _processor.ProcessSingleAsync(Arg.Any<ScanEvent>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
                Task.FromResult(++processCount == 2
                    ? Result<bool>.Failure("error")
                    : Result<bool>.Success(true)));
        _queue.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                if (++deleteCount == 2) cts.Cancel();
                return Task.CompletedTask;
            });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;
        await worker.StopAsync(CancellationToken.None);

        await _queue.Received(2).DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _queue.DidNotReceive().DeleteAsync("receipt-2", Arg.Any<CancellationToken>());
    }

    [Fact(Timeout = 5000)]
    public async Task WhenOneMsgFails_RemainingMessagesStillProcessed()
    {
        var cts = new CancellationTokenSource();
        var messages = new List<QueueMessage<ScanEvent>>
        {
            MakeMessage(1, "receipt-1"),
            MakeMessage(2, "receipt-2"),
            MakeMessage(3, "receipt-3"),
        };
        var processCount = 0;
        var deleteCount = 0;

        _queue.ReceiveAsync<ScanEvent>(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(messages);
        _processor.ProcessSingleAsync(Arg.Any<ScanEvent>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
                Task.FromResult(++processCount == 2
                    ? Result<bool>.Failure("error")
                    : Result<bool>.Success(true)));
        _queue.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                if (++deleteCount == 2) cts.Cancel();
                return Task.CompletedTask;
            });

        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;
        await worker.StopAsync(CancellationToken.None);

        await _processor.Received(3).ProcessSingleAsync(Arg.Any<ScanEvent>(), Arg.Any<CancellationToken>());
    }
}
