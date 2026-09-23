using EchoProtocol.Api.Services.Interfaces;

namespace EchoProtocol.Api.Services;

public sealed class UnconfiguredProgressionPolicy : IProgressionPolicy
{
    public string Version => "UNCONFIGURED";
    public bool IsConfigured => false;

    public IReadOnlyList<ProgressionAllocation> Calculate(RewardMatchSnapshot match) =>
        throw new InvalidOperationException("An approved progression policy is not configured.");

    public int GetLevel(long totalExperiencePoints) =>
        throw new InvalidOperationException("Approved level thresholds are not configured.");
}
