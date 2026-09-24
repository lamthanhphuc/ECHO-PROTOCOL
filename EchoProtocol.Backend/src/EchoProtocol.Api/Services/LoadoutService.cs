using System.Globalization;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Inventory;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoProtocol.Api.Services;

public sealed class LoadoutService : ILoadoutService
{
    public const string CharacterSlot = "CHARACTER";
    public const string CharacterCategory = ShopItemCategories.Character;
    public const string TeamToolCategory = ShopItemCategories.TeamTool;
    private const string TeamToolPrefix = "TEAM_TOOL_";

    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly int _teamToolSlotCount;

    public LoadoutService(
        AppDbContext db,
        TimeProvider timeProvider,
        IOptions<LoadoutSettings> settings)
    {
        _db = db;
        _timeProvider = timeProvider;
        _teamToolSlotCount = settings.Value.TeamToolSlotCount;
    }

    public async Task<ServiceResult<PlayerLoadoutResponse>> GetCurrentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var items = await QueryLoadout(userId).ToArrayAsync(cancellationToken);
        return ServiceResult<PlayerLoadoutResponse>.Success(new PlayerLoadoutResponse
        {
            Character = items.SingleOrDefault(item => item.SlotId == CharacterSlot),
            TeamTools = items.Where(item =>
                    ResolveSlot(item.SlotId) is { IsTeamTool: true })
                .OrderBy(item => ParseTeamToolIndex(item.SlotId))
                .ToArray()
        }, "Loadout retrieved");
    }

    public async Task<ServiceResult<LoadoutItemResponse>> EquipAsync(
        Guid userId,
        EquipLoadoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ExtensionData is { Count: > 0 } || request.ItemId == Guid.Empty)
            return Failure<LoadoutItemResponse>(
                "Equip request contains invalid or unsupported fields",
                ErrorCodes.ValidationError);

        var slot = ResolveSlot(request.SlotId);
        if (slot is null)
            return Failure<LoadoutItemResponse>(
                "Loadout slot is not configured",
                ErrorCodes.LoadoutSlotInvalid);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (!await LockUserAsync(userId, cancellationToken))
                return Failure<LoadoutItemResponse>("User not found", ErrorCodes.NotFound);

            var inventory = await _db.InventoryItems
                .Include(item => item.ShopItem)
                .SingleOrDefaultAsync(
                    item => item.UserId == userId && item.ShopItemId == request.ItemId,
                    cancellationToken);
            if (inventory is null)
                return Failure<LoadoutItemResponse>(
                    "Item is not owned by the authenticated player",
                    ErrorCodes.LoadoutItemNotOwned);

            if (!string.Equals(
                    inventory.ShopItem.Category,
                    slot.Category,
                    StringComparison.Ordinal))
                return Failure<LoadoutItemResponse>(
                    $"Item category {inventory.ShopItem.Category} cannot be equipped in {slot.Id}",
                    ErrorCodes.LoadoutCategoryMismatch);

            if (slot.IsTeamTool && await _db.PlayerLoadoutItems.AnyAsync(
                    item => item.UserId == userId &&
                            item.InventoryItemId == inventory.InventoryItemId &&
                            item.SlotId != slot.Id,
                    cancellationToken))
                return Failure<LoadoutItemResponse>(
                    "The same TeamTool cannot be equipped in more than one slot",
                    ErrorCodes.LoadoutDuplicateTeamTool);

            var equipped = await _db.PlayerLoadoutItems
                .Include(item => item.InventoryItem)
                .ThenInclude(item => item.ShopItem)
                .SingleOrDefaultAsync(
                    item => item.UserId == userId && item.SlotId == slot.Id,
                    cancellationToken);
            if (equipped?.InventoryItemId == inventory.InventoryItemId)
            {
                await transaction.CommitAsync(cancellationToken);
                return ServiceResult<LoadoutItemResponse>.Success(
                    ToResponse(equipped), "Item is already equipped");
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (equipped is null)
            {
                equipped = new PlayerLoadoutItem
                {
                    UserId = userId,
                    SlotId = slot.Id,
                    InventoryItemId = inventory.InventoryItemId,
                    InventoryItem = inventory,
                    EquippedAtUtc = now,
                    UpdatedAtUtc = now
                };
                _db.PlayerLoadoutItems.Add(equipped);
            }
            else
            {
                equipped.InventoryItemId = inventory.InventoryItemId;
                equipped.InventoryItem = inventory;
                equipped.EquippedAtUtc = now;
                equipped.UpdatedAtUtc = now;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult<LoadoutItemResponse>.Success(
                ToResponse(equipped), "Item equipped");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();

            var current = await QueryLoadout(userId)
                .SingleOrDefaultAsync(item => item.SlotId == slot.Id, cancellationToken);
            if (current?.ItemId == request.ItemId)
                return ServiceResult<LoadoutItemResponse>.Success(
                    current, "Item is already equipped");

            return Failure<LoadoutItemResponse>(
                "Loadout changed concurrently; read the current loadout and retry",
                ErrorCodes.LoadoutConflict);
        }
    }

    public async Task<ServiceResult<UnequipLoadoutResponse>> UnequipAsync(
        Guid userId,
        UnequipLoadoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ExtensionData is { Count: > 0 })
            return Failure<UnequipLoadoutResponse>(
                "Unequip request contains unsupported fields",
                ErrorCodes.ValidationError);

        var slot = ResolveSlot(request.SlotId);
        if (slot is null)
            return Failure<UnequipLoadoutResponse>(
                "Loadout slot is not configured",
                ErrorCodes.LoadoutSlotInvalid);
        if (!slot.IsTeamTool)
            return Failure<UnequipLoadoutResponse>(
                "The Character slot cannot be unequipped",
                ErrorCodes.LoadoutCharacterRequired);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockUserAsync(userId, cancellationToken))
            return Failure<UnequipLoadoutResponse>("User not found", ErrorCodes.NotFound);

        var equipped = await _db.PlayerLoadoutItems.SingleOrDefaultAsync(
            item => item.UserId == userId && item.SlotId == slot.Id,
            cancellationToken);
        var wasEquipped = equipped is not null;
        if (equipped is not null)
        {
            _db.PlayerLoadoutItems.Remove(equipped);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<UnequipLoadoutResponse>.Success(new UnequipLoadoutResponse
        {
            SlotId = slot.Id,
            WasEquipped = wasEquipped
        }, wasEquipped ? "Item unequipped" : "Slot is already empty");
    }

    private IQueryable<LoadoutItemResponse> QueryLoadout(Guid userId) =>
        _db.PlayerLoadoutItems.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new LoadoutItemResponse
            {
                SlotId = item.SlotId,
                ItemId = item.InventoryItem.ShopItemId,
                Name = item.InventoryItem.ShopItem.ItemName,
                Category = item.InventoryItem.ShopItem.Category,
                AssetReference = item.InventoryItem.ShopItem.AssetReference,
                EquippedAtUtc = item.EquippedAtUtc
            });

    private async Task<bool> LockUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            return await _db.Users.FromSqlInterpolated(
                    $"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken) is not null;

        return await _db.Users.AnyAsync(user => user.Id == userId, cancellationToken);
    }

    private SlotDefinition? ResolveSlot(string? requestedSlot)
    {
        var slot = requestedSlot?.Trim().ToUpperInvariant();
        if (slot == CharacterSlot)
            return new SlotDefinition(CharacterSlot, CharacterCategory, false);
        if (slot is null || !slot.StartsWith(TeamToolPrefix, StringComparison.Ordinal))
            return null;

        var suffix = slot[TeamToolPrefix.Length..];
        if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
            index < 1 || index > _teamToolSlotCount ||
            slot != $"{TeamToolPrefix}{index}")
            return null;

        return new SlotDefinition(slot, TeamToolCategory, true);
    }

    private static int ParseTeamToolIndex(string slotId) =>
        int.Parse(slotId[TeamToolPrefix.Length..], CultureInfo.InvariantCulture);

    private static LoadoutItemResponse ToResponse(PlayerLoadoutItem item) => new()
    {
        SlotId = item.SlotId,
        ItemId = item.InventoryItem.ShopItemId,
        Name = item.InventoryItem.ShopItem.ItemName,
        Category = item.InventoryItem.ShopItem.Category,
        AssetReference = item.InventoryItem.ShopItem.AssetReference,
        EquippedAtUtc = item.EquippedAtUtc
    };

    private static ServiceResult<T> Failure<T>(string message, string code) =>
        ServiceResult<T>.Failure(message, code);

    private sealed record SlotDefinition(string Id, string Category, bool IsTeamTool);
}
