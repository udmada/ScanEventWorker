using ScanEventWorker.Domain;

namespace ScanEventWorker.Contracts;

public interface IScanEventRepository
{
    Task<long> GetLastEventIdAsync(CancellationToken ct);
    Task UpdateLastEventIdAsync(long eventId, CancellationToken ct);
    Task UpsertParcelSummaryAsync(ScanEvent scanEvent, CancellationToken ct);
}
