using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Amazon.SQS;
using Amazon.SQS.Model;
using ScanEventWorker.Contracts;
using ScanEventWorker.Infrastructure.ApiClient;

namespace ScanEventWorker.Infrastructure.Messaging;

public sealed class SqsMessageQueue(IAmazonSQS sqsClient, string queueUrl) : IMessageQueue
{
    private const int LongPollWaitTimeSeconds = 20;

    public async Task SendAsync<T>(T message, CancellationToken ct)
    {
        var typeInfo = GetTypeInfo<T>();
        var body = JsonSerializer.Serialize(message, typeInfo);
        await sqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = body
        }, ct);
    }

    public async Task<IReadOnlyList<QueueMessage<T>>> ReceiveAsync<T>(int maxMessages, CancellationToken ct)
    {
        var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = maxMessages,
            WaitTimeSeconds = LongPollWaitTimeSeconds
        }, ct);

        var typeInfo = GetTypeInfo<T>();
        var results = new List<QueueMessage<T>>(response.Messages.Count);
        foreach (var msg in response.Messages)
        {
            var body = JsonSerializer.Deserialize(msg.Body, typeInfo);
            if (body is not null)
                results.Add(new QueueMessage<T>(body, msg.ReceiptHandle));
        }

        return results;
    }

    public async Task DeleteAsync(string receiptHandle, CancellationToken ct)
    {
        await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = queueUrl,
            ReceiptHandle = receiptHandle
        }, ct);
    }

    private static JsonTypeInfo<T> GetTypeInfo<T>()
    {
        return (JsonTypeInfo<T>)(ApiJsonContext.Default.GetTypeInfo(typeof(T))
            ?? throw new InvalidOperationException(
                $"Type {typeof(T).Name} is not registered in ApiJsonContext"));
    }
}
