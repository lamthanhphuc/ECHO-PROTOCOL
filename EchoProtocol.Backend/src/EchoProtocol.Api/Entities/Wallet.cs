namespace EchoProtocol.Api.Entities;

public class Wallet
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int Balance { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<WalletTransaction> Transactions { get; set; } = [];
    public ICollection<MatchRewardGrant> MatchRewardGrants { get; set; } = [];
}
