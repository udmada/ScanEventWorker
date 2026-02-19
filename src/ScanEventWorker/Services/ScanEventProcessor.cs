using ScanEventWorker.Contracts;
using ScanEventWorker.Domain;

namespace ScanEventWorker.Services;

public sealed class ScanEventProcessor(
    IScanEventRepository repository,
    ILogger<ScanEventProcessor> logger)
{
    public async Task<Result<int>> ProcessBatchAsync(
        IReadOnlyList<ScanEvent> events, CancellationToken ct)
    {
        var processed = 0;

        foreach (var scanEvent in events)
        {
            var result = await ProcessSingleAsync(scanEvent, ct);
            result.Match(
                onSuccess: _ =>
                {
                    processed++;
                    return true;
                },
                onFailure: error =>
                {
                    logger.LogWarning(
                        "Failed to process EventId {EventId} for ParcelId {ParcelId}: {Error}",
                        scanEvent.EventId, scanEvent.ParcelId, error);
                    return false;
                });
        }

        return Result<int>.Success(processed);
    }

    public async Task<Result<bool>> ProcessSingleAsync(ScanEvent scanEvent, CancellationToken ct)
    {
        try
        {
            await repository.UpsertParcelSummaryAsync(scanEvent, ct);

            logger.LogDebug(
                "Processed EventId {EventId}, ParcelId {ParcelId}, Type {Type}",
                scanEvent.EventId, scanEvent.ParcelId, scanEvent.Type);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"Database error for EventId {scanEvent.EventId}: {ex.Message}");
        }
    }
}
