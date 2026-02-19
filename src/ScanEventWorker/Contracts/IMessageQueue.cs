namespace ScanEventWorker.Contracts;

public interface IMessageQueue
{
    Task SendAsync<T>(T message, CancellationToken ct);
    Task<IReadOnlyList<QueueMessage<T>>> ReceiveAsync<T>(int maxMessages, CancellationToken ct);
    Task DeleteAsync(string receiptHandle, CancellationToken ct);
}

public sealed record QueueMessage<T>(T Body, string ReceiptHandle);
