using EchoProtocol.Api.Services.Models;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IMatchTelemetryAggregator
{
    Task<MatchTelemetryAggregation> AggregateAsync(
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
