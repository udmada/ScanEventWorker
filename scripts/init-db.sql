-- Reference SQL schema for ScanEventWorker
-- This is applied automatically by DatabaseInitializer on startup.
-- Provided here for reference and manual setup.

CREATE TABLE ProcessingState (
    Id INT PRIMARY KEY DEFAULT 1,
    LastEventId BIGINT NOT NULL DEFAULT 1,
    UpdatedAtUtc DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT CK_SingleRow CHECK (Id = 1)
);

INSERT INTO ProcessingState (Id, LastEventId) VALUES (1, 1);

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
