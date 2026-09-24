using EchoProtocol.Api.Services.Models;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IAdaptiveInputSnapshotBuilder
{
    Task<AdaptiveSnapshotBuildResult> BuildPreMatchAsync(
        Guid matchId, CancellationToken cancellationToken = default);
}
