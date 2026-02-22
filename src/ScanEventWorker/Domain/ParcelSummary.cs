namespace ScanEventWorker.Domain;

public sealed class ParcelSummary(ParcelId parcelId)
{
    public ParcelId ParcelId { get; } = parcelId;
    public EventId LatestEventId { get; private set; }
    public string LatestType { get; private set; } = string.Empty;
    public DateTimeOffset LatestCreatedDateTimeUtc { get; private set; }
    public string LatestStatusCode { get; private set; } = string.Empty;
    public string LatestRunId { get; private set; } = string.Empty;
    public DateTimeOffset? PickedUpAtUtc { get; private set; }
    public DateTimeOffset? DeliveredAtUtc { get; private set; }

    public void ApplyScanEvent(ScanEvent scanEvent)
    {
        if (scanEvent.EventId <= LatestEventId)
        {
            return; // idempotent - ignore older events
        }

        LatestEventId = scanEvent.EventId;
        LatestType = scanEvent.Type;
        LatestCreatedDateTimeUtc = scanEvent.CreatedDateTimeUtc;
        LatestStatusCode = scanEvent.StatusCode;
        LatestRunId = scanEvent.RunId;

        switch (scanEvent.Type)
        {
            case ScanEventTypes.Pickup:
                PickedUpAtUtc ??= scanEvent.CreatedDateTimeUtc; // first occurrence only
                break;
            case ScanEventTypes.Delivery:
                DeliveredAtUtc ??= scanEvent.CreatedDateTimeUtc; // first occurrence only
                break;
            default:
                break;
        }
    }
}
