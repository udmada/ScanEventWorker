using Dapper;
using Microsoft.Data.SqlClient;

namespace ScanEventWorker.Infrastructure.Persistence;

[DapperAot]
public sealed class DatabaseInitializer(string connectionString, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        logger.LogInformation("Initializing database schema");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        _ = await connection.ExecuteAsync(new CommandDefinition("""
                                                                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessingState')
                                                                BEGIN
                                                                    CREATE TABLE ProcessingState (
                                                                        Id INT PRIMARY KEY DEFAULT 1,
                                                                        LastEventId BIGINT NOT NULL DEFAULT 1,
                                                                        UpdatedAtUtc DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                                                                        CONSTRAINT CK_SingleRow CHECK (Id = 1)
                                                                    );
                                                                    INSERT INTO ProcessingState (Id, LastEventId) VALUES (1, 1);
                                                                END
                                                                """, cancellationToken: ct));

        _ = await connection.ExecuteAsync(new CommandDefinition("""
                                                                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ParcelSummary')
                                                                BEGIN
                                                                    CREATE TABLE ParcelSummary (
                                                                        ParcelId INT PRIMARY KEY,
                                                                        LatestEventId BIGINT NOT NULL,
                                                                        LatestType NVARCHAR(50) NOT NULL,
                                                                        LatestCreatedDateTimeUtc DATETIMEOFFSET NOT NULL,
                                                                        LatestStatusCode NVARCHAR(50) NOT NULL DEFAULT '',
                                                                        LatestRunId NVARCHAR(50) NOT NULL DEFAULT '',
                                                                        PickedUpAtUtc DATETIMEOFFSET NULL,
                                                                        DeliveredAtUtc DATETIMEOFFSET NULL
                                                                    );
                                                                END
                                                                """, cancellationToken: ct));

        logger.LogInformation("Database schema initialized");
    }
}
