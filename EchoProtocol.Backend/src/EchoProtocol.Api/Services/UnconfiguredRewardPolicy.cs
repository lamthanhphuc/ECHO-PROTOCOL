using EchoProtocol.Api.Services.Interfaces;

namespace EchoProtocol.Api.Services;

public sealed class UnconfiguredRewardPolicy : IRewardPolicy
{
    public string Version => "UNCONFIGURED";
    public bool IsConfigured => false;

    public IReadOnlyList<RewardAllocation> Calculate(RewardMatchSnapshot match) =>
        throw new InvalidOperationException(
            "A versioned reward formula must be approved and configured before rewards can be processed.");
}
