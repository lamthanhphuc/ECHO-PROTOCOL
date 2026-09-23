using EchoProtocol.Api.Common;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services.Interfaces;

namespace EchoProtocol.Api.Services;

public sealed class ProgressionService : IProgressionService
{
    private const int MaximumPolicyVersionLength = 50;
    private readonly IProgressionPolicy _policy;

    public ProgressionService(IProgressionPolicy policy)
    {
        _policy = policy;
    }

    public bool IsConfigured => _policy.IsConfigured;
    public string PolicyVersion => _policy.Version;

    public ServiceResult<IReadOnlyList<ProgressionUpdate>> Calculate(
        RewardMatchSnapshot match,
        IReadOnlyDictionary<Guid, PlayerProfile> profiles)
    {
        if (!_policy.IsConfigured)
        {
            return Failure(
                "No approved progression policy is configured",
                ErrorCodes.ProgressionPolicyNotConfigured);
        }

        if (string.IsNullOrWhiteSpace(_policy.Version) ||
            _policy.Version.Length > MaximumPolicyVersionLength)
        {
            return Failure("Progression policy version is invalid", ErrorCodes.ProgressionPolicyInvalid);
        }

        IReadOnlyList<ProgressionAllocation> allocations;
        try
        {
            allocations = _policy.Calculate(match);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Failure(
                $"Progression policy rejected the stored result: {exception.Message}",
                ErrorCodes.ProgressionPolicyInvalid);
        }

        if (allocations is null ||
            allocations.Count != match.Players.Count ||
            allocations.Any(item => item.UserId == Guid.Empty || item.ExperiencePoints < 0) ||
            allocations.Select(item => item.UserId).Distinct().Count() != allocations.Count ||
            allocations.Any(item => !profiles.ContainsKey(item.UserId)))
        {
            return Failure(
                "Progression policy must return one valid allocation per result player",
                ErrorCodes.ProgressionPolicyInvalid);
        }

        try
        {
            var updates = allocations.Select(allocation =>
            {
                var profile = profiles[allocation.UserId];
                var totalMatches = checked(profile.TotalMatches + 1);
                var totalWins = checked(profile.TotalWins + (allocation.CountsAsWin ? 1 : 0));
                var experiencePoints = checked(profile.ExperiencePoints + allocation.ExperiencePoints);
                var level = _policy.GetLevel(experiencePoints);
                if (level < profile.Level || level < 1 || totalWins > totalMatches)
                {
                    throw new InvalidOperationException(
                        "Progression policy produced an invalid level or win count.");
                }

                return new ProgressionUpdate(
                    allocation.UserId,
                    totalMatches,
                    totalWins,
                    experiencePoints,
                    level,
                    allocation.ExperiencePoints);
            }).ToArray();

            return ServiceResult<IReadOnlyList<ProgressionUpdate>>.Success(updates);
        }
        catch (Exception exception) when (exception is OverflowException or InvalidOperationException)
        {
            return Failure(
                $"Progression calculation is invalid: {exception.Message}",
                ErrorCodes.ProgressionPolicyInvalid);
        }
    }

    private static ServiceResult<IReadOnlyList<ProgressionUpdate>> Failure(
        string message,
        string code) => ServiceResult<IReadOnlyList<ProgressionUpdate>>.Failure(message, code);
}
