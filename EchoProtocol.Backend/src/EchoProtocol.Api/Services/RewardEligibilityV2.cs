using EchoProtocol.Api.Services.Interfaces;

namespace EchoProtocol.Api.Services;

internal static class RewardEligibilityV2
{
    public static (int Units, decimal TeamFactor) Evaluate(RewardPlayerSnapshot player,
        EchoProtocol.Api.Entities.RewardConfiguration settings)
    {
        if (player.EvidenceComplete != true || player.ObjectiveActions is null ||
            player.RescueActions is null || player.ObjectiveActions < 0 || player.RescueActions < 0)
            throw new ArgumentException("Incomplete V2 player evidence.");

        var units = Math.Min(player.ObjectiveActions.Value, settings.ObjectiveActionCap)
            + Math.Min(player.RescueActions.Value, settings.RescueActionCap);
        var factor = units switch {
            0 => 0m,
            1 => settings.OneActionTeamPercent / 100m,
            2 => settings.TwoActionTeamPercent / 100m,
            _ => settings.FullTeamPercent / 100m
        };
        return (units, factor);
    }
}
