namespace ScanEventWorker.Domain;

public readonly record struct EventId(long Value) : IComparable<EventId>
{
    public int CompareTo(EventId other) => Value.CompareTo(other.Value);
    public static bool operator >(EventId left, EventId right) => left.Value > right.Value;
    public static bool operator <(EventId left, EventId right) => left.Value < right.Value;
    public static bool operator >=(EventId left, EventId right) => left.Value >= right.Value;
    public static bool operator <=(EventId left, EventId right) => left.Value <= right.Value;
    public override string ToString() => Value.ToString();
}

public readonly record struct ParcelId(int Value)
{
    public override string ToString() => Value.ToString();
}
