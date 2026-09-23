using System.Security.Claims;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Controllers;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Inventory;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class LoadoutServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task ReadEmptyLoadout()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        var result = await harness.Service.GetCurrentAsync(harness.UserId);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Character);
        Assert.Empty(result.Data.TeamTools);
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task EquipOwnedCharacter()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        var result = await harness.Equip("CHARACTER", harness.CharacterOneId);
        Assert.True(result.IsSuccess);
        Assert.Equal(harness.CharacterOneId, result.Data!.ItemId);
        Assert.Equal("CHARACTER", result.Data.Category);
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task EquipCharacterReplacesOldCharacter()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        Assert.True((await harness.Equip("CHARACTER", harness.CharacterOneId)).IsSuccess);
        Assert.True((await harness.Equip("CHARACTER", harness.CharacterTwoId)).IsSuccess);
        var row = Assert.Single(await harness.Db.PlayerLoadoutItems.ToArrayAsync());
        Assert.Equal(harness.CharacterTwoInventoryId, row.InventoryItemId);
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task RetrySameCharacterEquipIsIdempotent()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        var first = await harness.Equip("CHARACTER", harness.CharacterOneId);
        var retry = await harness.Equip("CHARACTER", harness.CharacterOneId);
        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Single(await harness.Db.PlayerLoadoutItems.ToArrayAsync());
        Assert.Equal(first.Data!.EquippedAtUtc, retry.Data!.EquippedAtUtc);
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task RejectUnownedCharacter()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        var result = await harness.Equip("CHARACTER", harness.OtherUserCharacterId);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.LoadoutItemNotOwned, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task RejectTeamToolInCharacterSlot()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        var result = await harness.Equip("CHARACTER", harness.TeamToolOneId);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.LoadoutCategoryMismatch, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task EquipOwnedTeamTool()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        var result = await harness.Equip("TEAM_TOOL_1", harness.TeamToolOneId);
        Assert.True(result.IsSuccess);
        Assert.Equal("TEAM_TOOL_1", result.Data!.SlotId);
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task ReplaceTeamToolInSameSlot()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        Assert.True((await harness.Equip("TEAM_TOOL_1", harness.TeamToolOneId)).IsSuccess);
        Assert.True((await harness.Equip("TEAM_TOOL_1", harness.TeamToolTwoId)).IsSuccess);
        var result = await harness.Service.GetCurrentAsync(harness.UserId);
        Assert.Equal(harness.TeamToolTwoId, Assert.Single(result.Data!.TeamTools).ItemId);
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task RejectCharacterInTeamToolSlot()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        var result = await harness.Equip("TEAM_TOOL_1", harness.CharacterOneId);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.LoadoutCategoryMismatch, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task RejectTeamToolTwoBecauseOnlyOneSlotIsConfigured()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        Assert.True((await harness.Equip("TEAM_TOOL_1", harness.TeamToolOneId)).IsSuccess);
        var result = await harness.Equip("TEAM_TOOL_2", harness.TeamToolOneId);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.LoadoutSlotInvalid, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task LegacyTeamToolTwoIsExcludedFromCurrentLoadout()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        var inventory = await harness.Db.InventoryItems.SingleAsync(
            item => item.UserId == harness.UserId &&
                    item.ShopItemId == harness.TeamToolOneId);
        harness.Db.PlayerLoadoutItems.Add(new PlayerLoadoutItem
        {
            UserId = harness.UserId,
            SlotId = "TEAM_TOOL_2",
            InventoryItemId = inventory.InventoryItemId,
            EquippedAtUtc = Now.UtcDateTime,
            UpdatedAtUtc = Now.UtcDateTime
        });
        await harness.Db.SaveChangesAsync();

        var result = await harness.Service.GetCurrentAsync(harness.UserId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.TeamTools);
        Assert.Single(await harness.Db.PlayerLoadoutItems.ToArrayAsync());
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task UnequipTeamTool()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        Assert.True((await harness.Equip("TEAM_TOOL_1", harness.TeamToolOneId)).IsSuccess);
        var result = await harness.Service.UnequipAsync(harness.UserId, new UnequipLoadoutRequest
        {
            SlotId = "TEAM_TOOL_1"
        });
        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.WasEquipped);
        Assert.Empty(await harness.Db.PlayerLoadoutItems.ToArrayAsync());
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task RejectInvalidSlotAndCharacterUnequip()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        var invalid = await harness.Equip("TEAM_TOOL_3", harness.TeamToolOneId);
        var character = await harness.Service.UnequipAsync(
            harness.UserId, new UnequipLoadoutRequest { SlotId = "CHARACTER" });
        Assert.Equal(ErrorCodes.LoadoutSlotInvalid, invalid.ErrorCode);
        Assert.Equal(ErrorCodes.LoadoutCharacterRequired, character.ErrorCode);
    }

    [Fact, Trait("Category", "M4LoadoutUnit")]
    public async Task JwtOwnershipCannotMutateAnotherUsersLoadout()
    {
        await using var harness = await LoadoutHarness.CreateAsync();
        var controller = new LoadoutController(
            harness.Service,
            NullLogger<LoadoutController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = Principal(harness.OtherUserId)
                }
            }
        };

        var action = await controller.Equip(new EquipLoadoutRequest
        {
            SlotId = "CHARACTER",
            ItemId = harness.CharacterOneId
        }, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Empty(await harness.Db.PlayerLoadoutItems.ToArrayAsync());
    }

    private static ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test"));

    private sealed class LoadoutHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private LoadoutHarness(
            SqliteConnection connection,
            AppDbContext db,
            Guid userId,
            Guid otherUserId,
            ShopItem characterOne,
            ShopItem characterTwo,
            ShopItem teamToolOne,
            ShopItem teamToolTwo,
            ShopItem otherCharacter,
            Guid characterOneInventoryId,
            Guid characterTwoInventoryId)
        {
            _connection = connection;
            Db = db;
            UserId = userId;
            OtherUserId = otherUserId;
            CharacterOneId = characterOne.ItemId;
            CharacterTwoId = characterTwo.ItemId;
            TeamToolOneId = teamToolOne.ItemId;
            TeamToolTwoId = teamToolTwo.ItemId;
            OtherUserCharacterId = otherCharacter.ItemId;
            CharacterOneInventoryId = characterOneInventoryId;
            CharacterTwoInventoryId = characterTwoInventoryId;
            Service = new LoadoutService(
                db,
                new FixedTimeProvider(Now),
                Options.Create(new LoadoutSettings { TeamToolSlotCount = 1 }));
        }

        public AppDbContext Db { get; }
        public LoadoutService Service { get; }
        public Guid UserId { get; }
        public Guid OtherUserId { get; }
        public Guid CharacterOneId { get; }
        public Guid CharacterTwoId { get; }
        public Guid TeamToolOneId { get; }
        public Guid TeamToolTwoId { get; }
        public Guid OtherUserCharacterId { get; }
        public Guid CharacterOneInventoryId { get; }
        public Guid CharacterTwoInventoryId { get; }

        public Task<ServiceResult<LoadoutItemResponse>> Equip(string slotId, Guid itemId) =>
            Service.EquipAsync(UserId, new EquipLoadoutRequest
            {
                SlotId = slotId,
                ItemId = itemId
            });

        public static async Task<LoadoutHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync();

            var user = CreateUser("loadout-owner");
            var other = CreateUser("other-owner");
            var characterOne = CreateItem("Character One", "CHARACTER");
            var characterTwo = CreateItem("Character Two", "CHARACTER");
            var teamToolOne = CreateItem("Team Tool One", "TEAM_TOOL");
            var teamToolTwo = CreateItem("Team Tool Two", "TEAM_TOOL");
            var otherCharacter = CreateItem("Other Character", "CHARACTER");
            var inventory = new[]
            {
                CreateInventory(user.Id, characterOne.ItemId),
                CreateInventory(user.Id, characterTwo.ItemId),
                CreateInventory(user.Id, teamToolOne.ItemId),
                CreateInventory(user.Id, teamToolTwo.ItemId),
                CreateInventory(other.Id, otherCharacter.ItemId)
            };
            db.Users.AddRange(user, other);
            db.ShopItems.AddRange(
                characterOne, characterTwo, teamToolOne, teamToolTwo, otherCharacter);
            db.InventoryItems.AddRange(inventory);
            await db.SaveChangesAsync();

            return new LoadoutHarness(
                connection, db, user.Id, other.Id,
                characterOne, characterTwo, teamToolOne, teamToolTwo, otherCharacter,
                inventory[0].InventoryItemId,
                inventory[1].InventoryItemId);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static User CreateUser(string name) => new()
        {
            Id = Guid.NewGuid(),
            Email = $"{name}-{Guid.NewGuid():N}@echo.invalid",
            Username = $"{name}-{Guid.NewGuid():N}",
            PasswordHash = "not-used",
            Role = UserRole.PLAYER,
            Status = UserStatus.ACTIVE,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        };

        private static ShopItem CreateItem(string name, string category) => new()
        {
            ItemId = Guid.NewGuid(),
            ItemName = name,
            Description = $"{name} test fixture",
            Category = category,
            Price = 100,
            AssetReference = $"test://loadout/{name.Replace(' ', '-').ToLowerInvariant()}",
            IsActive = true,
            CreatedAtUtc = Now.UtcDateTime,
            UpdatedAtUtc = Now.UtcDateTime
        };

        private static InventoryItem CreateInventory(Guid userId, Guid itemId) => new()
        {
            InventoryItemId = Guid.NewGuid(),
            UserId = userId,
            ShopItemId = itemId,
            Source = InventoryAcquisitionSource.PURCHASE,
            AcquiredAtUtc = Now.UtcDateTime
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
