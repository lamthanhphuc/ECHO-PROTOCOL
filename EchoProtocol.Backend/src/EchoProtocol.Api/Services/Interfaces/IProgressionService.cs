using EchoProtocol.Api.Common;
using EchoProtocol.Api.Entities;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IProgressionService
{
    bool IsConfigured { get; }
    string PolicyVersion { get; }

    ServiceResult<IReadOnlyList<ProgressionUpdate>> Calculate(
        RewardMatchSnapshot match,
        IReadOnlyDictionary<Guid, PlayerProfile> profiles);
}

public sealed record ProgressionUpdate(
    Guid UserId,
    int TotalMatches,
    int TotalWins,
    long ExperiencePoints,
    int Level,
    long ExperiencePointsAwarded);
