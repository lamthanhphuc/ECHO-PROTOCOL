namespace EchoProtocol.Api.Entities;

public sealed class PlayerLoadoutItem
{
    public Guid UserId { get; set; }
    public string SlotId { get; set; } = string.Empty;
    public Guid InventoryItemId { get; set; }
    public DateTime EquippedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
}
