namespace EchoProtocol.Api.DTOs.Shop;

public sealed record PurchaseHistoryItemResponse(
    Guid PurchaseId,
    Guid ItemId,
    string ItemName,
    string Category,
    int PricePaid,
    string Status,
    DateTime PurchasedAtUtc);

public sealed record PurchaseHistoryResponse(
    IReadOnlyList<PurchaseHistoryItemResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
