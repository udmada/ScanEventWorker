using ScanEventWorker.Domain;

namespace ScanEventWorker.Contracts;

public interface IScanEventApiClient
{
    Task<Result<IReadOnlyList<ScanEvent>>> GetScanEventsAsync(
        long fromEventId, int limit, CancellationToken ct);
}
