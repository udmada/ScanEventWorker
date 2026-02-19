namespace ScanEventWorker.Domain;

public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}

public sealed record ParcelPickedUp(ParcelId ParcelId, DateTime OccurredAtUtc) : IDomainEvent;

public sealed record ParcelDelivered(ParcelId ParcelId, DateTime OccurredAtUtc) : IDomainEvent;
