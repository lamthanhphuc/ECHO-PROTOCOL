namespace EchoProtocol.Api.Services.Interfaces;

public sealed record MatchJoinProofPayload(
    Guid ProofId,
    Guid MatchId,
    Guid UserId,
    int FusionActorNumber,
    string FusionSessionName,
    DateTime ExpiresAtUtc);

public interface IMatchJoinProofService
{
    string Issue(MatchJoinProofPayload payload);
    bool TryValidate(string proof, out MatchJoinProofPayload? payload);
}
