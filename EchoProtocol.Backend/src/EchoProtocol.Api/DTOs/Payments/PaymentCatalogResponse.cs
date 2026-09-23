namespace EchoProtocol.Api.DTOs.Payments;

public sealed class PaymentCatalogResponse
{
    public IReadOnlyList<PaymentCatalogItemResponse> Items { get; init; } = [];
}

public sealed class PaymentCatalogItemResponse
{
    public string ProductReference { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int WalletCredit { get; init; }
    public string Provider { get; init; } = string.Empty;
}
