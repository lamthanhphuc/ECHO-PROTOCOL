using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Telemetry;

namespace EchoProtocol.Api.Services.Interfaces;

public interface ITelemetryService
{
    Task<ServiceResult<TelemetryBatchResponse>> IngestBatchAsync(
        TelemetryBatchRequest request,
        Guid authenticatedUserId,
        CancellationToken cancellationToken);
}
