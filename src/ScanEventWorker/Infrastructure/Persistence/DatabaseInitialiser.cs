using Dapper;
using Microsoft.Data.SqlClient;

namespace ScanEventWorker.Infrastructure.Persistence;

[DapperAot]
public sealed class DatabaseInitialiser(string connectionString, ILogger<DatabaseInitialiser> logger)
{
    public async Task InitialiseAsync(CancellationToken ct)
    {
        logger.LogInformation("Initialising database schema");

        // Connect to master first and create the target database if it doesn't exist.
        // This removes the need for a manual sqlcmd step during local setup.
        var builder = new SqlConnectionStringBuilder(connectionString);
        string databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        await using (var masterConnection = new SqlConnection(builder.ConnectionString))
        {
            await masterConnection.OpenAsync(ct);

            // DDL identifiers cannot be parameterised; use QuoteIdentifier to escape the name.
            // Split into two ADO.NET commands so neither statement needs string interpolation.
            bool dbExists;
            await using (SqlCommand checkCmd = masterConnection.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(1) FROM sys.databases WHERE name = @name";
                _ = checkCmd.Parameters.AddWithValue("@name", databaseName);
                dbExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(ct)) > 0;
            }

            if (!dbExists)
            {
                // QuoteIdentifier wraps in brackets and escapes ] as ]], so concatenation is safe.
                string quotedDb = new SqlCommandBuilder().QuoteIdentifier(databaseName);
                await using SqlCommand createCmd = masterConnection.CreateCommand();
#pragma warning disable DAP242 // not a Dapper call; quotedDb is a safely-escaped SQL identifier
                createCmd.CommandText = "CREATE DATABASE " + quotedDb;
#pragma warning restore DAP242
                _ = await createCmd.ExecuteNonQueryAsync(ct);
            }
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        _ = await connection.ExecuteAsync(new CommandDefinition(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProcessingState')
            BEGIN
                CREATE TABLE ProcessingState (
                    Id           INT            PRIMARY KEY DEFAULT 1,
                    LastEventId  BIGINT         NOT NULL DEFAULT 1,
                    UpdatedAtUtc DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                    CONSTRAINT CK_SingleRow CHECK (Id = 1)
                );
                INSERT INTO ProcessingState (Id, LastEventId) VALUES (1, 1);
            END
            """,
            cancellationToken: ct));

        _ = await connection.ExecuteAsync(new CommandDefinition(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ParcelSummary')
            BEGIN
                CREATE TABLE ParcelSummary (
                    ParcelId                 INT            PRIMARY KEY,
                    LatestEventId            BIGINT         NOT NULL,
                    LatestType               NVARCHAR(50)   NOT NULL,
                    LatestCreatedDateTimeUtc DATETIMEOFFSET NOT NULL,
                    LatestStatusCode         NVARCHAR(50)   NOT NULL DEFAULT '',
                    LatestRunId              NVARCHAR(50)   NOT NULL DEFAULT '',
                    PickedUpAtUtc            DATETIMEOFFSET NULL,
                    DeliveredAtUtc           DATETIMEOFFSET NULL
                );
            END
            """,
            cancellationToken: ct));

        logger.LogInformation("Database schema initialised");
    }
}
