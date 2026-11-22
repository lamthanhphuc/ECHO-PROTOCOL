namespace EchoProtocol.Api.Entities;

public sealed class MatchRewardGrant
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public Guid WalletId { get; set; }
    public int CurrencyAmount { get; set; }
    public string PolicyVersion { get; set; } = string.Empty;
    public long ExperiencePointsAwarded { get; set; }
    public int? LevelBefore { get; set; }
    public int? LevelAfter { get; set; }
    public string UnlockedItemsJson { get; set; } = "[]";
    public string CosmeticRewardsJson { get; set; } = "[]";
    public string ProgressionPolicyVersion { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; }

    public MatchResultPlayer ResultPlayer { get; set; } = null!;
    public Wallet Wallet { get; set; } = null!;
}
