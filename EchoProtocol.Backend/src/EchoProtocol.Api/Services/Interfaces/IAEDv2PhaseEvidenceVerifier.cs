namespace EchoProtocol.Api.Services.Interfaces;

public interface IAEDv2PhaseEvidenceVerifier
{
    Task<bool> VerifyAsync(Guid matchId, int phaseOrdinal, string evidenceFingerprint,
        CancellationToken cancellationToken = default);
}
