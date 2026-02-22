using ScanEventWorker.Domain;

namespace ScanEventWorker.Contracts;

public interface IScanEventProcessor
{
    Task<Result<bool>> ProcessSingleAsync(ScanEvent scanEvent, CancellationToken ct);
}
