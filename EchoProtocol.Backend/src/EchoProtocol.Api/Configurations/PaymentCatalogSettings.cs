namespace EchoProtocol.Api.Configurations;

public sealed class PaymentCatalogSettings
{
    public const string SectionName = "PaymentCatalog";

    public string[] AllowedProviders { get; init; } = [];
    public PaymentProductSettings[] Products { get; init; } = [];
}

public sealed class PaymentProductSettings
{
    public string ProductReference { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int? ExpiresAfterMinutes { get; init; }
    public string? FulfillmentKind { get; init; }
    public int? WalletCreditAmount { get; init; }
    public Guid? ShopItemId { get; init; }
}
