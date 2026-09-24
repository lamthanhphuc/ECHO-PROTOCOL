using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Shop;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IShopCatalogService
{
    Task<ServiceResult<ShopCatalogResponse>> GetItemsAsync(
        string? category,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
