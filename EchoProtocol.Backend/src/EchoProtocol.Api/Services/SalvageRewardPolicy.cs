using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
namespace EchoProtocol.Api.Services;

// Reward amounts come from this versioned server policy, never from client currency fields.
public sealed class SalvageRewardPolicy : IRewardPolicy
{
    public const string PolicyVersion="SALVAGE_REWARD_V1";
    public string Version=>PolicyVersion;
    public bool IsConfigured=>true;
    public static bool ValidEvidence(int levels,int days)=>levels>=0&&levels<=10000&&days>=levels*3&&days<=levels*3+3;
    public static int PaidLevels(RewardMatchSnapshot match,RewardPlayerSnapshot player)
    {
        if(!MatchContentCatalog.IsSalvage(match.MapId)||match.SalvageCompletedLevels is not int levels
            ||match.SalvageCompletedDays is not int days||!ValidEvidence(levels,days))
            throw new ArgumentException("Invalid Salvage evidence");
        int participation=player.ParticipationSeconds??0;
        if(match.SalvageUsedDebugCheats||player.Disconnected||player.EvidenceComplete!=true||participation<120||participation<match.DurationSeconds/2d)return 0;
        return Math.Min(5,Math.Min(levels,Math.Min(match.DurationSeconds,participation)/180));
    }
    public IReadOnlyList<RewardAllocation> Calculate(RewardMatchSnapshot match)=>match.Players
        .Select(p=>new RewardAllocation(p.UserId,PaidLevels(match,p)*40)).ToArray();
}
public sealed class SalvageProgressionPolicy : IProgressionPolicy
{
    public const string PolicyVersion="SALVAGE_PROGRESSION_V1";
    public string Version=>PolicyVersion;
    public bool IsConfigured=>true;
    public int GetLevel(long experience)=>new ProgressionPolicyV1().GetLevel(experience);
    public IReadOnlyList<ProgressionAllocation> Calculate(RewardMatchSnapshot match)=>match.Players.Select(p=>
    {
        int levels=SalvageRewardPolicy.PaidLevels(match,p);
        return new ProgressionAllocation(p.UserId,levels*100,levels>0&&match.Outcome==MatchOutcome.WIN,levels>0);
    }).ToArray();
}
