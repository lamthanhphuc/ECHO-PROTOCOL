using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Inventory;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;

    public InventoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<InventoryResponse>> GetCurrentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var items = await _db.InventoryItems.AsNoTracking()
            .Where(inventory => inventory.UserId == userId)
            .OrderBy(inventory => inventory.AcquiredAtUtc)
            .ThenBy(inventory => inventory.InventoryItemId)
            .Select(inventory => new InventoryItemResponse
            {
                InventoryItemId = inventory.InventoryItemId,
                ItemId = inventory.ShopItemId,
                ItemName = inventory.ShopItem.ItemName,
                Category = inventory.ShopItem.Category,
                AssetReference = inventory.ShopItem.AssetReference,
                Source = inventory.Source,
                PurchaseId = inventory.PurchaseId,
                AcquiredAtUtc = inventory.AcquiredAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return ServiceResult<InventoryResponse>.Success(
            new InventoryResponse { Items = items },
            "Inventory retrieved");
    }
}
