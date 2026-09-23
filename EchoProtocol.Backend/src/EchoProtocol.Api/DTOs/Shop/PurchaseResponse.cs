namespace EchoProtocol.Api.DTOs.Shop;

public sealed class PurchaseResponse
{
    public Guid PurchaseId { get; init; }
    public Guid WalletTransactionId { get; init; }
    public Guid ItemId { get; init; }
    public int PricePaid { get; init; }
    public int WalletBalance { get; init; }
    public DateTime PurchasedAtUtc { get; init; }
    public bool IsReplay { get; init; }
}
