using Dapper;
using Microsoft.Data.SqlClient;
using ScanEventWorker.Contracts;
using ScanEventWorker.Domain;

namespace ScanEventWorker.Infrastructure.Persistence;

[DapperAot]
public sealed class ScanEventRepository(
    string connectionString,
    ILogger<ScanEventRepository> logger) : IScanEventRepository
{
    public async Task<long> GetLastEventIdAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        long? lastEventId = await connection.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition(
                "SELECT LastEventId FROM ProcessingState WHERE Id = 1",
                cancellationToken: ct));

        if (lastEventId is null)
        {
            logger.LogWarning(
                "ProcessingState row not found - defaulting LastEventId to 1. " +
                "Re-run DatabaseInitializer or INSERT INTO ProcessingState (Id, LastEventId) VALUES (1, 1).");
            return 1L;
        }

        return lastEventId.Value;
    }

    public async Task UpdateLastEventIdAsync(long eventId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        _ = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE ProcessingState SET LastEventId = @EventId, UpdatedAtUtc = SYSDATETIMEOFFSET() WHERE Id = 1",
                new { EventId = eventId },
                cancellationToken: ct));
    }

    public async Task UpsertParcelSummaryAsync(ScanEvent scanEvent, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        _ = await connection.ExecuteAsync(
            new CommandDefinition("""
                                  MERGE ParcelSummary AS target
                                  USING (SELECT @ParcelId AS ParcelId) AS source
                                  ON target.ParcelId = source.ParcelId
                                  WHEN MATCHED AND @EventId > target.LatestEventId THEN
                                      UPDATE SET
                                          LatestEventId = @EventId,
                                          LatestType = @Type,
                                          LatestCreatedDateTimeUtc = @CreatedDateTimeUtc,
                                          LatestStatusCode = @StatusCode,
                                          LatestRunId = @RunId,
                                          PickedUpAtUtc = CASE WHEN @Type = 'PICKUP' AND target.PickedUpAtUtc IS NULL THEN @CreatedDateTimeUtc ELSE target.PickedUpAtUtc END,
                                          DeliveredAtUtc = CASE WHEN @Type = 'DELIVERY' AND target.DeliveredAtUtc IS NULL THEN @CreatedDateTimeUtc ELSE target.DeliveredAtUtc END
                                  WHEN NOT MATCHED THEN
                                      INSERT (ParcelId, LatestEventId, LatestType, LatestCreatedDateTimeUtc, LatestStatusCode, LatestRunId, PickedUpAtUtc, DeliveredAtUtc)
                                      VALUES (
                                          @ParcelId, @EventId, @Type, @CreatedDateTimeUtc, @StatusCode, @RunId,
                                          CASE WHEN @Type = 'PICKUP' THEN @CreatedDateTimeUtc END,
                                          CASE WHEN @Type = 'DELIVERY' THEN @CreatedDateTimeUtc END
                                      );
                                  """,
                new
                {
                    ParcelId = scanEvent.ParcelId.Value,
                    EventId = scanEvent.EventId.Value,
                    scanEvent.Type,
                    scanEvent.CreatedDateTimeUtc,
                    scanEvent.StatusCode,
                    scanEvent.RunId
                },
                cancellationToken: ct));
    }
}
