namespace EchoProtocol.Api.Services.Interfaces;

public interface IMatchProfilePostProcessingService
{
    Task EnqueueAsync(Guid matchId, CancellationToken cancellationToken = default);
}
