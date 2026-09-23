using EchoProtocol.Api.Common;
using EchoProtocol.Api.Controllers;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Shop;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class ShopCatalogServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 15, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Category", "M4ShopUnit")]
    public async Task ReturnsOnlyItemsCurrentlyForSale()
    {
        await using var harness = await ShopHarness.CreateAsync(
            Item("Active A", "CHARACTER", 100, true),
            Item("Active B", "TEAM_TOOL", 50, true),
            Item("Disabled", "CHARACTER", 25, false));

        var result = await harness.Service.GetItemsAsync(null, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalItems);
        Assert.All(result.Data.Items, item => Assert.NotEqual("Disabled", item.Name));
    }

    [Fact, Trait("Category", "M4ShopUnit")]
    public async Task CategoryFilterIsTrimmedAndCaseInsensitive()
    {
        await using var harness = await ShopHarness.CreateAsync(
            Item("Character", "CHARACTER", 100, true),
            Item("Tool", "TEAM_TOOL", 50, true));

        var result = await harness.Service.GetItemsAsync(" character ", 1, 20);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("Character", item.Name);
        Assert.Equal("CHARACTER", item.Category);
    }

    [Fact, Trait("Category", "M4ShopUnit")]
    public async Task PaginationUsesStableOrderingAndMetadata()
    {
        await using var harness = await ShopHarness.CreateAsync(
            Item("Delta", "TEAM_TOOL", 40, true),
            Item("Alpha", "TEAM_TOOL", 10, true),
            Item("Echo", "TEAM_TOOL", 50, true),
            Item("Bravo", "TEAM_TOOL", 20, true),
            Item("Charlie", "TEAM_TOOL", 30, true));

        var result = await harness.Service.GetItemsAsync(null, 2, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Page);
        Assert.Equal(2, result.Data.PageSize);
        Assert.Equal(5, result.Data.TotalItems);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.Equal(["Charlie", "Delta"], result.Data.Items.Select(item => item.Name));
    }

    [Fact, Trait("Category", "M4ShopUnit")]
    public async Task DisabledItemsNeverAppearEvenWhenCategoryMatches()
    {
        await using var harness = await ShopHarness.CreateAsync(
            Item("Disabled", "CHARACTER", 25, false));

        var result = await harness.Service.GetItemsAsync("CHARACTER", 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalItems);
    }

    [Fact, Trait("Category", "M4ShopUnit")]
    public async Task EmptyCatalogReturnsEmptyFirstPage()
    {
        await using var harness = await ShopHarness.CreateAsync();

        var result = await harness.Service.GetItemsAsync(null, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalPages);
    }

    [Theory, Trait("Category", "M4ShopUnit")]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(2147483647, 100)]
    public async Task InvalidPaginationIsRejected(int page, int pageSize)
    {
        await using var harness = await ShopHarness.CreateAsync();

        var result = await harness.Service.GetItemsAsync(null, page, pageSize);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4ShopUnit")]
    public async Task TestCatalogSeedCanRunTwiceWithoutDuplicates()
    {
        await using var harness = await ShopHarness.CreateAsync();
        var timeProvider = new FixedTimeProvider(Now);

        await ShopCatalogSeeder.SeedTestCatalogAsync(
            harness.Db, timeProvider, NullLogger.Instance);
        await ShopCatalogSeeder.SeedTestCatalogAsync(
            harness.Db, timeProvider, NullLogger.Instance);

        Assert.Equal(2, await harness.Db.ShopItems.CountAsync());
        Assert.Equal(2, await harness.Db.ShopItems.Select(item => item.ItemId).Distinct().CountAsync());
        Assert.All(await harness.Db.ShopItems.ToListAsync(), item =>
            Assert.StartsWith("Test ", item.ItemName));
    }

    [Fact, Trait("Category", "M4ShopUnit")]
    public async Task ControllerResponseMatchesPublicCatalogContract()
    {
        var item = Item("Contract Item", "TEAM_TOOL", 75, true);
        await using var harness = await ShopHarness.CreateAsync(item);
        var controller = new ShopController(
            harness.Service,
            NullLogger<ShopController>.Instance);

        var action = await controller.GetItems(null, 1, 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<ShopCatalogResponse>>(ok.Value);
        Assert.True(response.Success);
        var returned = Assert.Single(response.Data!.Items);
        Assert.Equal(item.ItemId, returned.ItemId);
        Assert.Equal(item.ItemName, returned.Name);
        Assert.Equal(item.Category, returned.Category);
        Assert.Equal(item.Price, returned.Price);
        Assert.Equal(item.Description, returned.Description);
        Assert.Equal(item.AssetReference, returned.AssetReference);
    }

    private static ShopItem Item(
        string name,
        string category,
        int price,
        bool active) => new()
    {
        ItemId = Guid.NewGuid(),
        ItemName = name,
        Description = $"{name} description",
        Category = category,
        Price = price,
        AssetReference = $"test://{category.ToLowerInvariant()}/{name.ToLowerInvariant().Replace(' ', '-')}",
        IsActive = active,
        CreatedAtUtc = Now.UtcDateTime,
        UpdatedAtUtc = Now.UtcDateTime
    };

    private sealed class ShopHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ShopHarness(SqliteConnection connection, AppDbContext db)
        {
            _connection = connection;
            Db = db;
            Service = new ShopCatalogService(db);
        }

        public AppDbContext Db { get; }
        public ShopCatalogService Service { get; }

        public static async Task<ShopHarness> CreateAsync(params ShopItem[] items)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync();
            if (items.Length > 0)
            {
                db.ShopItems.AddRange(items);
                await db.SaveChangesAsync();
            }

            return new ShopHarness(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
