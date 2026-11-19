using System.ComponentModel.DataAnnotations;

namespace EchoProtocol.Api.Entities;

public sealed class RewardConfiguration
{
    public static readonly Guid SharedId = new("c7e0137e-2484-4c88-9d7b-2161c6e15cef");
    public Guid Id { get; set; }
    [ConcurrencyCheck] public DateTime EffectiveAtUtc { get; set; }
    public Guid UpdatedByUserId { get; set; }
    [Range(0, 10000)] public int Participation { get; set; } = 0;
    [Range(0, 10000)] public int Objective { get; set; } = 70;
    [Range(0, 10000)] public int Win { get; set; } = 150;
    [Range(0, 10000)] public int Survival { get; set; } = 10;
    [Range(0, 10000)] public int Contribution { get; set; } = 70;
    [Range(0, 10000)] public int Disconnected { get; set; } = 0;
    [Range(0, 10000)] public int Maximum { get; set; } = 320;
    [Required, RegularExpression("REWARD_V1|REWARD_V2")]
    public string PolicyKey { get; set; } = "REWARD_V2";
    [Range(typeof(decimal), "0", "100")]
    public decimal LossCapCreditsPerMinute { get; set; } = 2m;
    [Range(0, 10000)] public int RescueBonusCredits { get; set; } = 10;
    [Range(1, 100)] public int ObjectiveActionCap { get; set; } = 4;
    [Range(1, 100)] public int RescueActionCap { get; set; } = 2;
    [Range(0, 100)] public int OneActionTeamPercent { get; set; } = 50;
    [Range(0, 100)] public int TwoActionTeamPercent { get; set; } = 75;
    [Range(0, 100)] public int FullTeamPercent { get; set; } = 100;
    [Range(0, 10000)] public int WinExperience { get; set; } = 75;
    [Range(0, 10000)] public int ObjectiveExperience { get; set; } = 50;
    [Range(0, 10000)] public int ContributionExperience { get; set; } = 25;
    [Range(0, 10000)] public int SurvivalExperience { get; set; } = 25;
    [Range(0, 100)] public int LossExperiencePerMinute { get; set; } = 1;

    public bool HasValidV2Settings() => PolicyKey == "REWARD_V2" && Participation == 0 &&
        Disconnected == 0 && Maximum > 0 && LossCapCreditsPerMinute is > 0 and <= 100 &&
        RescueBonusCredits is >= 0 and <= 10000 && ObjectiveActionCap is >= 1 and <= 100 &&
        RescueActionCap is >= 1 and <= 100 &&
        OneActionTeamPercent is >= 0 and <= 100 &&
        TwoActionTeamPercent >= OneActionTeamPercent && TwoActionTeamPercent <= 100 &&
        FullTeamPercent >= TwoActionTeamPercent && FullTeamPercent <= 100 &&
        WinExperience is >= 0 and <= 10000 && ObjectiveExperience is >= 0 and <= 10000 &&
        ContributionExperience is >= 0 and <= 10000 && SurvivalExperience is >= 0 and <= 10000 &&
        LossExperiencePerMinute is >= 0 and <= 100;
}
