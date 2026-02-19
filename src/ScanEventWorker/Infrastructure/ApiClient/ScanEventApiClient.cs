using System.Net.Http.Json;
using ScanEventWorker.Contracts;
using ScanEventWorker.Domain;

namespace ScanEventWorker.Infrastructure.ApiClient;

public sealed class ScanEventApiClient(HttpClient httpClient) : IScanEventApiClient
{
    public async Task<Result<IReadOnlyList<ScanEvent>>> GetScanEventsAsync(
        long fromEventId, int limit, CancellationToken ct)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync(
                $"/v1/scans/scanevents?FromEventId={fromEventId}&Limit={limit}",
                ApiJsonContext.Default.ScanEventApiResponse,
                ct);

            if (response is null)
                return Result<IReadOnlyList<ScanEvent>>.Failure("API returned null response");

            var events = new List<ScanEvent>(response.ScanEvents.Count);
            foreach (var dto in response.ScanEvents)
            {
                var parsed = MapToDomain(dto);
                if (parsed.IsSuccess)
                    events.Add(parsed.Value);
                // Malformed events are skipped — caller logs via Result pattern
            }

            return Result<IReadOnlyList<ScanEvent>>.Success(events);
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<ScanEvent>>.Failure($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // propagate cancellation
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ScanEvent>>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    public static Result<ScanEvent> MapToDomain(ScanEventDto dto)
    {
        if (dto.EventId <= 0)
            return Result<ScanEvent>.Failure($"Invalid EventId: {dto.EventId}");

        if (dto.ParcelId <= 0)
            return Result<ScanEvent>.Failure($"Invalid ParcelId: {dto.ParcelId}");

        if (string.IsNullOrWhiteSpace(dto.Type))
            return Result<ScanEvent>.Failure($"Missing Type for EventId {dto.EventId}");

        return Result<ScanEvent>.Success(new ScanEvent(
            new Domain.EventId(dto.EventId),
            new Domain.ParcelId(dto.ParcelId),
            dto.Type,
            dto.CreatedDateTimeUtc,
            dto.StatusCode ?? string.Empty,
            dto.User?.RunId ?? string.Empty));
    }
}
