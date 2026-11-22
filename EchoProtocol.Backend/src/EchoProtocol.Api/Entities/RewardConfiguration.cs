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
    [Required, RegularExpression("REWARD_V1|REWARD_V2|REWARD_V3")]
    // Retain defaults for previously serialized snapshots. New matches opt into V3 explicitly.
    public string PolicyKey { get; set; } = "REWARD_V2";
    [Range(typeof(decimal), "1", "120")] public decimal BaseMinutes { get; set; } = 20m;
    [Range(typeof(decimal), "1", "120")] public decimal SciFiReferenceMinutes { get; set; } = 20m;
    [Range(typeof(decimal), "1", "120")] public decimal FloodedReferenceMinutes { get; set; } = 20m;
    [Range(typeof(decimal), "1", "120")] public decimal PurgeReferenceMinutes { get; set; } = 20m;
    [Range(typeof(decimal), "0.5", "1")] public decimal EasyMultiplier { get; set; } = .90m;
    [Range(typeof(decimal), "1", "1.15")] public decimal HardMultiplier { get; set; } = 1.15m;
    [Range(typeof(decimal), "0", "10")] public decimal V3LossCreditsPerMinute { get; set; } = 4m;
    [Range(typeof(decimal), "0", "10")] public decimal V3LossExperiencePerMinute { get; set; } = 2m;

    public bool HasValidV3Settings() => PolicyKey == "REWARD_V3" &&
        Participation == 0 && Disconnected == 0 && Maximum == 320 &&
        Win == 150 && Objective == 70 && Contribution == 70 && Survival == 10 &&
        WinExperience == 75 && ObjectiveExperience == 50 && ContributionExperience == 25 && SurvivalExperience == 25 &&
        ObjectiveActionCap == 4 && RescueActionCap == 2 && RescueBonusCredits == 10 &&
        OneActionTeamPercent == 50 && TwoActionTeamPercent == 75 && FullTeamPercent == 100 &&
        BaseMinutes is >= 1 and <= 120 && SciFiReferenceMinutes is >= 1 and <= 120 &&
        FloodedReferenceMinutes is >= 1 and <= 120 && PurgeReferenceMinutes is >= 1 and <= 120 &&
        EasyMultiplier is >= .5m and <= 1m && HardMultiplier is >= 1m and <= 1.15m &&
        V3LossCreditsPerMinute is >= 0 and <= 10 && V3LossExperiencePerMinute is >= 0 and <= 10;
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
