using System.Net.Http.Json;
using ScanEventWorker.Contracts;
using ScanEventWorker.Domain;
using EventId = ScanEventWorker.Domain.EventId;

namespace ScanEventWorker.Infrastructure.ApiClient;

public sealed class ScanEventApiClient(
    HttpClient httpClient,
    ILogger<ScanEventApiClient> logger) : IScanEventApiClient
{
    public async Task<Result<IReadOnlyList<ScanEvent>>> GetScanEventsAsync(
        long fromEventId, int limit, CancellationToken ct)
    {
        try
        {
            ScanEventApiResponse? response = await httpClient.GetFromJsonAsync(
                $"/v1/scans/scanevents?FromEventId={fromEventId}&Limit={limit}",
                ApiJsonContext.Default.ScanEventApiResponse,
                ct);

            if (response is null)
            {
                return Result<IReadOnlyList<ScanEvent>>.Failure("API returned null response");
            }

            var events = new List<ScanEvent>(response.ScanEvents.Count);
            foreach (ScanEventDto dto in response.ScanEvents)
            {
                Result<ScanEvent> parsed = MapToDomain(dto);
                if (parsed.IsSuccess)
                {
                    events.Add(parsed.Value);
                }
                else
                {
                    logger.LogWarning(
                        "Skipping malformed scan event: EventId={EventId}, ParcelId={ParcelId}, Reason={Reason}",
                        dto.EventId, dto.ParcelId, parsed.Error);
                }
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
        return dto.EventId <= 0
            ? Result<ScanEvent>.Failure($"Invalid EventId: {dto.EventId}")
            : dto.ParcelId <= 0
                ? Result<ScanEvent>.Failure($"Invalid ParcelId: {dto.ParcelId}")
                : string.IsNullOrWhiteSpace(dto.Type)
                    ? Result<ScanEvent>.Failure($"Missing Type for EventId {dto.EventId}")
                    : Result<ScanEvent>.Success(new ScanEvent(
                        new EventId(dto.EventId),
                        new ParcelId(dto.ParcelId),
                        dto.Type.ToUpperInvariant(),
                        dto.CreatedDateTimeUtc,
                        dto.StatusCode ?? string.Empty,
                        dto.User?.RunId ?? string.Empty));
    }
}
