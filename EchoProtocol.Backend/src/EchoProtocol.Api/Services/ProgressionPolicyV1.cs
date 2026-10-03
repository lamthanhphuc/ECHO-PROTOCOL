using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;

namespace EchoProtocol.Api.Services;

public sealed class ProgressionPolicyV1 : IProgressionPolicy
{
    public const string PolicyVersion = "PROGRESSION_V1";
    public const long ExperiencePerLevel = 500;
    public const long MaximumExperiencePerMatch = 225;

    private const long ParticipationExperience = 50;
    private const long MaximumObjectiveExperience = 50;
    private const long WinExperience = 75;
    private const long SurvivalExperience = 25;
    private const long ContributionExperience = 25;
    private const long DisconnectedExperience = 20;

    public string Version => PolicyVersion;
    public bool IsConfigured => true;

    public IReadOnlyList<ProgressionAllocation> Calculate(RewardMatchSnapshot match)
    {
        RewardSnapshotRules.Validate(match);

        var objectiveExperience = (long)decimal.Floor(
            match.ObjectiveCompletion * MaximumObjectiveExperience);

        return match.Players
            .Select(player => player.Disconnected
                ? new ProgressionAllocation(player.UserId, DisconnectedExperience, false)
                : new ProgressionAllocation(
                    player.UserId,
                    CalculateConnectedPlayer(match.Outcome, objectiveExperience, player),
                    match.Outcome == MatchOutcome.WIN))
            .ToArray();
    }

    public int GetLevel(long totalExperiencePoints)
    {
        if (totalExperiencePoints < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalExperiencePoints),
                "Total experience points cannot be negative.");
        }

        var level = (totalExperiencePoints / ExperiencePerLevel) + 1;
        return level >= int.MaxValue ? int.MaxValue : (int)level;
    }

    private static long CalculateConnectedPlayer(
        MatchOutcome outcome,
        long objectiveExperience,
        RewardPlayerSnapshot player)
    {
        var experience = checked(
            ParticipationExperience
            + objectiveExperience
            + (outcome == MatchOutcome.WIN ? WinExperience : 0)
            + (player.Survived ? SurvivalExperience : 0)
            + (player.ObjectiveContribution > 0 ? ContributionExperience : 0));

        return Math.Min(experience, MaximumExperiencePerMatch);
    }
}
