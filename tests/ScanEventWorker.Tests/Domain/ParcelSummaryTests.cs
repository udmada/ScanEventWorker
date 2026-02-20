using ScanEventWorker.Domain;

namespace ScanEventWorker.Tests.Domain;

public class ParcelSummaryTests
{
    [Fact]
    public void ApplyScanEvent_PickupEvent_SetsPickedUpAtUtc()
    {
        var parcel = new ParcelSummary(new ParcelId(1));
        var timestamp = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var scanEvent = new ScanEvent(
            new EventId(1), new ParcelId(1), ScanEventTypes.Pickup,
            timestamp, string.Empty, "run-1");

        parcel.ApplyScanEvent(scanEvent);

        Assert.Equal(new EventId(1), parcel.LatestEventId);
        Assert.Equal(timestamp, parcel.PickedUpAtUtc);
        Assert.Null(parcel.DeliveredAtUtc);
    }

    [Fact]
    public void ApplyScanEvent_DeliveryEvent_SetsDeliveredAtUtc()
    {
        var parcel = new ParcelSummary(new ParcelId(1));
        var timestamp = new DateTimeOffset(2025, 6, 2, 14, 0, 0, TimeSpan.Zero);
        var scanEvent = new ScanEvent(
            new EventId(2), new ParcelId(1), ScanEventTypes.Delivery,
            timestamp, string.Empty, "run-1");

        parcel.ApplyScanEvent(scanEvent);

        Assert.Equal(timestamp, parcel.DeliveredAtUtc);
        Assert.Null(parcel.PickedUpAtUtc);
    }

    [Fact]
    public void ApplyScanEvent_SecondPickup_DoesNotOverwriteFirstOccurrence()
    {
        var parcel = new ParcelSummary(new ParcelId(1));
        var firstPickup = new DateTimeOffset(2025, 6, 1, 8, 0, 0, TimeSpan.Zero);
        var secondPickup = new DateTimeOffset(2025, 6, 2, 9, 0, 0, TimeSpan.Zero);

        parcel.ApplyScanEvent(new ScanEvent(
            new EventId(1), new ParcelId(1), ScanEventTypes.Pickup, firstPickup, string.Empty, "run-1"));
        parcel.ApplyScanEvent(new ScanEvent(
            new EventId(2), new ParcelId(1), ScanEventTypes.Pickup, secondPickup, string.Empty, "run-2"));

        Assert.Equal(firstPickup, parcel.PickedUpAtUtc);
    }

    [Fact]
    public void ApplyScanEvent_SecondDelivery_DoesNotOverwriteFirstOccurrence()
    {
        var parcel = new ParcelSummary(new ParcelId(1));
        var firstDelivery = new DateTimeOffset(2025, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var secondDelivery = new DateTimeOffset(2025, 6, 4, 11, 0, 0, TimeSpan.Zero);

        parcel.ApplyScanEvent(new ScanEvent(
            new EventId(1), new ParcelId(1), ScanEventTypes.Delivery, firstDelivery, string.Empty, "run-1"));
        parcel.ApplyScanEvent(new ScanEvent(
            new EventId(2), new ParcelId(1), ScanEventTypes.Delivery, secondDelivery, string.Empty, "run-2"));

        Assert.Equal(firstDelivery, parcel.DeliveredAtUtc);
    }

    [Fact]
    public void ApplyScanEvent_OlderEvent_IsIgnored()
    {
        var parcel = new ParcelSummary(new ParcelId(1));
        var event1 = new ScanEvent(
            new EventId(5), new ParcelId(1), ScanEventTypes.Status,
            DateTimeOffset.UtcNow, "SC1", "run-1");
        var olderEvent = new ScanEvent(
            new EventId(3), new ParcelId(1), ScanEventTypes.Pickup,
            DateTimeOffset.UtcNow, "SC2", "run-2");

        parcel.ApplyScanEvent(event1);
        parcel.ApplyScanEvent(olderEvent);

        Assert.Equal(new EventId(5), parcel.LatestEventId);
        Assert.Equal("SC1", parcel.LatestStatusCode);
        Assert.Null(parcel.PickedUpAtUtc); // older PICKUP was ignored
    }

    [Fact]
    public void ApplyScanEvent_UnknownType_UpdatesFieldsWithoutSettingTimes()
    {
        var parcel = new ParcelSummary(new ParcelId(1));
        var scanEvent = new ScanEvent(
            new EventId(1), new ParcelId(1), "CUSTOM_TYPE",
            DateTimeOffset.UtcNow, "SC1", "run-1");

        parcel.ApplyScanEvent(scanEvent);

        Assert.Equal("CUSTOM_TYPE", parcel.LatestType);
        Assert.Null(parcel.PickedUpAtUtc);
        Assert.Null(parcel.DeliveredAtUtc);
    }

    [Fact]
    public void ApplyScanEvent_SameEventId_IsIgnored()
    {
        var parcel = new ParcelSummary(new ParcelId(1));
        var event1 = new ScanEvent(
            new EventId(5), new ParcelId(1), ScanEventTypes.Pickup,
            DateTimeOffset.UtcNow, "SC1", "run-1");
        var sameIdEvent = new ScanEvent(
            new EventId(5), new ParcelId(1), ScanEventTypes.Delivery,
            DateTimeOffset.UtcNow, "SC2", "run-2");

        parcel.ApplyScanEvent(event1);
        parcel.ApplyScanEvent(sameIdEvent);

        Assert.Equal(ScanEventTypes.Pickup, parcel.LatestType); // second event ignored
    }
}
