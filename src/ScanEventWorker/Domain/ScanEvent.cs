namespace ScanEventWorker.Domain;

public sealed record ScanEvent(
    EventId EventId,
    ParcelId ParcelId,
    string Type,
    DateTimeOffset CreatedDateTimeUtc,
    string StatusCode,
    string RunId);
