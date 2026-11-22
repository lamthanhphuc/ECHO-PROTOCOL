using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Player;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public static class LevelUnlockService
{
    public static async Task<LevelUnlockResponse[]> ReadAsync(AppDbContext db, Guid userId, int level,
        long experience, string curve, CancellationToken ct)
    {
        var items = await db.ShopItems.AsNoTracking().Where(x => x.IsActive && x.RequiredLevel > 1)
            .OrderBy(x => x.RequiredLevel).ThenBy(x => x.ItemName)
            .Select(x => new { x.ItemId, x.ItemName, x.Category, x.RequiredLevel, x.Price,
                Owned = db.InventoryItems.Any(i => i.UserId == userId && i.ShopItemId == x.ItemId) }).ToArrayAsync(ct);
        return items.Select(x => new LevelUnlockResponse(x.ItemId, x.ItemName, x.Category, x.RequiredLevel,
            level >= x.RequiredLevel ? 0 : Math.Max(0, (curve == ProgressionCurveV3.Version
                ? ProgressionCurveV3.Threshold(Math.Clamp(x.RequiredLevel, 1, 30)) : (x.RequiredLevel - 1L) * 500) - experience), x.Owned, x.Price)).ToArray();
    }
}
