namespace EchoProtocol.Api.Services.Interfaces;

public interface IProgressionPolicy
{
    string Version { get; }
    bool IsConfigured { get; }
    IReadOnlyList<ProgressionAllocation> Calculate(RewardMatchSnapshot match);
    int GetLevel(long totalExperiencePoints);
}

public sealed record ProgressionAllocation(
    Guid UserId,
    long ExperiencePoints,
    bool CountsAsWin);
