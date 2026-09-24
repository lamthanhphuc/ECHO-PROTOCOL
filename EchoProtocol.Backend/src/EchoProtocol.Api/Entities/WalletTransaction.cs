using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public sealed class WalletTransaction
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public WalletTransactionType Type { get; set; }
    public int Amount { get; set; }
    public int BalanceBefore { get; set; }
    public int BalanceAfter { get; set; }
    public Guid ReferenceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public Wallet Wallet { get; set; } = null!;
    public PurchaseTransaction? PurchaseTransaction { get; set; }
}
