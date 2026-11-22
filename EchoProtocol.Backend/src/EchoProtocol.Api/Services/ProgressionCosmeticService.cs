using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Player;
using EchoProtocol.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public static class ProgressionCosmeticService
{
    // Stable entitlement keys. Presentation assets are supplied independently by Unity/Web.
    public static readonly IReadOnlyList<ProgressionCosmeticResponse> Catalog = Array.AsReadOnly<ProgressionCosmeticResponse>([
        new("level15_badge", "BADGE", 15, "Dấu ấn thám hiểm", false, false, null),
        new("level20_frame", "PROFILE_FRAME", 20, "Khung hồ sơ kỳ cựu", false, false, null),
        new("level25_tool", "TOOL_SKIN", 25, "Công cụ chuyên gia", false, false, null),
        new("level30_outfit", "CHARACTER_OUTFIT", 30, "Trang phục Echo", false, false, null)
    ]);

    public static async Task<ProgressionCosmeticResponse[]> ReadAsync(AppDbContext db, Guid userId, CancellationToken ct)
    {
        var owned = await db.ProgressionCosmeticGrants.AsNoTracking().Where(g => g.UserId == userId)
            .ToDictionaryAsync(g => g.RewardKey, ct);
        return Catalog.Select(item => owned.TryGetValue(item.RewardKey, out var grant)
            ? item with { Owned = true, Equipped = grant.Equipped, GrantedAtUtc = grant.GrantedAtUtc } : item).ToArray();
    }

    // Caller holds the player's profile row lock; staged grants share its transaction.
    public static async Task<ProgressionCosmeticResponse[]> StageEligibleAsync(AppDbContext db, Guid userId,
        int level, DateTime now, CancellationToken ct)
    {
        var existing = await db.ProgressionCosmeticGrants.Where(g => g.UserId == userId).ToListAsync(ct);
        var owned = existing.Select(g => g.RewardKey).ToHashSet(StringComparer.Ordinal);
        owned.UnionWith(db.ProgressionCosmeticGrants.Local.Where(g => g.UserId == userId).Select(g => g.RewardKey));
        var rewards = Catalog.Where(item => item.RequiredLevel <= level && !owned.Contains(item.RewardKey)).ToArray();
        foreach (var reward in rewards)
            db.ProgressionCosmeticGrants.Add(new ProgressionCosmeticGrant
                { UserId = userId, RewardKey = reward.RewardKey, GrantedAtUtc = now });
        return rewards.Select(item => item with { Owned = true, GrantedAtUtc = now }).ToArray();
    }

    public static async Task<ServiceResult<ProgressionCosmeticResponse[]>> ReconcileAsync(AppDbContext db,
        Guid userId, DateTime now, CancellationToken ct, EquipProgressionCosmeticRequest? equip = null)
    {
        // Unity JsonUtility writes null string fields as empty strings when clearing a slot.
        if (equip is not null && string.IsNullOrWhiteSpace(equip.RewardKey)) equip = equip with { RewardKey = null };
        if (equip is not null && (!Catalog.Any(c => c.Category == equip.Category) ||
            equip.RewardKey is not null && !Catalog.Any(c => c.Category == equip.Category && c.RewardKey == equip.RewardKey)))
            return ServiceResult<ProgressionCosmeticResponse[]>.Failure("Unknown cosmetic reward", ErrorCodes.ValidationError);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var profile = db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? await db.PlayerProfiles.FromSqlInterpolated($"SELECT * FROM \"PlayerProfiles\" WHERE \"UserId\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct)
            : await db.PlayerProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
            return ServiceResult<ProgressionCosmeticResponse[]>.Failure("Profile not found", ErrorCodes.PlayerProfileNotFound);
        await db.Entry(profile).ReloadAsync(ct);
        if (equip?.RewardKey is not null && Catalog.Single(c => c.RewardKey == equip.RewardKey).RequiredLevel > profile.Level &&
            !await db.ProgressionCosmeticGrants.AnyAsync(g => g.UserId == userId && g.RewardKey == equip.RewardKey, ct))
            return ServiceResult<ProgressionCosmeticResponse[]>.Failure("Cosmetic level not reached", ErrorCodes.ShopLevelRequired);
        await StageEligibleAsync(db, userId, profile.Level, now, ct);
        if (equip is not null)
        {
            var categoryKeys = Catalog.Where(c => c.Category == equip.Category).Select(c => c.RewardKey).ToHashSet();
            // StageEligible loads stored grants; newly added grants are in Local too.
            foreach (var grant in db.ProgressionCosmeticGrants.Local.Where(g => g.UserId == userId && categoryKeys.Contains(g.RewardKey)))
                grant.Equipped = grant.RewardKey == equip.RewardKey;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<ProgressionCosmeticResponse[]>.Success(await ReadAsync(db, userId, ct));
    }
}
