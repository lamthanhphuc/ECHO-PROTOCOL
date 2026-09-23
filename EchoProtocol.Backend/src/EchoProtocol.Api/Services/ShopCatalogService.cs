using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Shop;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class ShopCatalogService : IShopCatalogService
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    private readonly AppDbContext _db;

    public ShopCatalogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<ShopCatalogResponse>> GetItemsAsync(
        string? category,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > MaximumPageSize)
        {
            return ServiceResult<ShopCatalogResponse>.Failure(
                $"Page must be at least 1 and pageSize must be between 1 and {MaximumPageSize}",
                ErrorCodes.ValidationError);
        }

        var skip = (long)(page - 1) * pageSize;
        if (skip > int.MaxValue)
        {
            return ServiceResult<ShopCatalogResponse>.Failure(
                "Requested page is outside the supported pagination range",
                ErrorCodes.ValidationError);
        }

        var normalizedCategory = string.IsNullOrWhiteSpace(category)
            ? null
            : category.Trim().ToUpperInvariant();
        if (normalizedCategory is { Length: > 50 })
        {
            return ServiceResult<ShopCatalogResponse>.Failure(
                "Category must not exceed 50 characters",
                ErrorCodes.ValidationError);
        }

        var query = _db.ShopItems.AsNoTracking().Where(item =>
            item.IsActive &&
            (item.Category == ShopItemCategories.Character ||
             item.Category == ShopItemCategories.TeamTool));
        if (normalizedCategory is not null)
        {
            query = query.Where(item => item.Category == normalizedCategory);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.ItemName)
            .ThenBy(item => item.ItemId)
            .Skip((int)skip)
            .Take(pageSize)
            .Select(item => new ShopItemResponse
            {
                ItemId = item.ItemId,
                Name = item.ItemName,
                Category = item.Category,
                Price = item.Price,
                Description = item.Description,
                AssetReference = item.AssetReference
            })
            .ToArrayAsync(cancellationToken);

        return ServiceResult<ShopCatalogResponse>.Success(new ShopCatalogResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0
                ? 0
                : (int)Math.Ceiling(totalItems / (double)pageSize)
        }, "Shop catalog retrieved");
    }
}
