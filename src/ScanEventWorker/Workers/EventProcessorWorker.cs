using ScanEventWorker.Contracts;
using ScanEventWorker.Domain;

namespace ScanEventWorker.Workers;

public sealed class EventProcessorWorker(
    IMessageQueue messageQueue,
    IScanEventProcessor processor,
    ILogger<EventProcessorWorker> logger) : BackgroundService
{
    private const int MaxMessagesPerReceive = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EventProcessorWorker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<QueueMessage<ScanEvent>> messages =
                await messageQueue.ReceiveAsync<ScanEvent>(MaxMessagesPerReceive, stoppingToken);

            if (messages.Count == 0)
            {
                continue;
            }

            logger.LogDebug("Received {Count} messages from queue", messages.Count);

            foreach (QueueMessage<ScanEvent> message in messages)
            {
                Result<bool> result = await processor.ProcessSingleAsync(message.Body, stoppingToken);

                if (result.IsSuccess)
                {
                    await messageQueue.DeleteAsync(message.ReceiptHandle, stoppingToken);
                }
                else
                {
                    logger.LogWarning(
                        "Failed to process EventId {EventId}: {Error}. Message will be redelivered by SQS",
                        message.Body.EventId, result.Error);
                    // Don't delete — SQS redelivers after visibility timeout, then DLQ after maxReceiveCount
                }
            }
        }
    }
}
