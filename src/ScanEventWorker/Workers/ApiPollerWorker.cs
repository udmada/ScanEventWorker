using Microsoft.Extensions.Options;
using ScanEventWorker.Contracts;
using ScanEventWorker.Domain;

namespace ScanEventWorker.Workers;

public sealed class ScanEventApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 100;
    public int PollingIntervalSeconds { get; set; } = 5;
    public int ErrorRetryIntervalSeconds { get; set; } = 30;
}

public sealed class ApiPollerWorker(
    IScanEventApiClient apiClient,
    IScanEventRepository repository,
    IMessageQueue messageQueue,
    IOptions<ScanEventApiOptions> options,
    ILogger<ApiPollerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ScanEventApiOptions config = options.Value;
        logger.LogInformation("ApiPollerWorker starting. BatchSize={BatchSize}, PollingInterval={Interval}s",
            config.BatchSize, config.PollingIntervalSeconds);

        long lastEventId = await repository.GetLastEventIdAsync(stoppingToken);
        logger.LogInformation("Resuming from LastEventId={LastEventId}", lastEventId);

        while (!stoppingToken.IsCancellationRequested)
        {
            Result<IReadOnlyList<ScanEvent>> result =
                await apiClient.GetScanEventsAsync(lastEventId, config.BatchSize, stoppingToken);

            if (!result.IsSuccess)
            {
                logger.LogWarning("API call failed: {Error}. Retrying in {Delay}s",
                    result.Error, config.ErrorRetryIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(config.ErrorRetryIntervalSeconds), stoppingToken);
                continue;
            }

            IReadOnlyList<ScanEvent> events = result.Value;
            if (events.Count == 0)
            {
                logger.LogDebug("No new events. Waiting {Interval}s", config.PollingIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(config.PollingIntervalSeconds), stoppingToken);
                continue;
            }

            foreach (ScanEvent scanEvent in events)
            {
                await messageQueue.SendAsync(scanEvent, stoppingToken);
            }

            long maxEventId = events[^1].EventId.Value;
            await repository.UpdateLastEventIdAsync(maxEventId, stoppingToken);
            lastEventId = maxEventId;

            logger.LogInformation(
                "Polled {Count} valid events (LastEventId={LastEventId}). Malformed events logged as warnings above.",
                events.Count, lastEventId);
        }
    }
}
