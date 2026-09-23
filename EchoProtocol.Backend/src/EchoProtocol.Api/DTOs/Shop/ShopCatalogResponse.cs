namespace EchoProtocol.Api.DTOs.Shop;

public sealed class ShopCatalogResponse
{
    public IReadOnlyList<ShopItemResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}

public sealed class ShopItemResponse
{
    public Guid ItemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public int Price { get; init; }
    public string Description { get; init; } = string.Empty;
    public string AssetReference { get; init; } = string.Empty;
}
