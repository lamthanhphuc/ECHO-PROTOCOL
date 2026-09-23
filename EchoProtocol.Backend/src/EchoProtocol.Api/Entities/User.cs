using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public PlayerProfile? PlayerProfile { get; set; }
    public Wallet? Wallet { get; set; }
    public PlayerAIProfile? PlayerAIProfile { get; set; }
    public ICollection<InventoryItem> InventoryItems { get; set; } = [];
    public ICollection<PlayerLoadoutItem> LoadoutItems { get; set; } = [];
    public ICollection<PurchaseTransaction> PurchaseTransactions { get; set; } = [];
    public ICollection<PaymentOrder> PaymentOrders { get; set; } = [];
}
