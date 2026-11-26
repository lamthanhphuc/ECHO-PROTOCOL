using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class RemoteFixedBalanceAdminTests
{
    [Fact] public async Task CreateValidVersionStartsInactive()
    {
        await using var h = await Harness.CreateAsync();
        var result = await h.Create("RFB-001");
        Assert.True(result.IsSuccess); Assert.False(result.Data!.IsActive);
        Assert.Equal("CREATE", (await h.Db.RemoteFixedBalanceAuditEntries.SingleAsync()).Action);
    }

    [Fact] public async Task CreateComputesCorrectHash()
    {
        await using var h = await Harness.CreateAsync();
        var result = await h.Create("RFB-001");
        Assert.Equal(RemoteFixedBalanceContract.ComputeHash(RemoteFixedBalanceTests.ValidSnapshot), result.Data!.ContentHash);
    }

    [Fact] public async Task RejectInvalidSnapshot()
    {
        await using var h = await Harness.CreateAsync();
        var result = await h.Create("RFB-001", "{}");
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
    }

    [Fact]
    public async Task InvalidDraftCannotReplaceActiveVersion()
    {
        await using var h = await Harness.CreateAsync();
        var adminId = Guid.NewGuid();
        var created = await h.Service.CreateVersionAsync(
            adminId, Request("RFB-001"), default);
        Assert.True(created.IsSuccess, created.Message);

        var activated = await h.Service.ActivateVersionAsync(
            adminId,
            "RFB-001",
            new ChangeRemoteFixedActivationRequest
            {
                ExpectedActiveVersion = string.Empty,
                Reason = "Initial approved version"
            },
            default);
        Assert.True(activated.IsSuccess, activated.Message);

        var invalidDraft = await h.Service.CreateVersionAsync(
            adminId, Request("RFB-002", json: "{}"), default);
        Assert.False(invalidDraft.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, invalidDraft.ErrorCode);

        h.Db.ChangeTracker.Clear();
        var active = await h.Db.RemoteFixedBalanceVersions
            .SingleAsync(item => item.IsActive);
        Assert.Equal("RFB-001", active.Version);
        Assert.Equal(2, await h.Db.RemoteFixedBalanceAuditEntries.CountAsync());
        var activationAudit = await h.Db.RemoteFixedBalanceAuditEntries
            .SingleAsync(item => item.Action == "ACTIVATE");
        Assert.Equal(adminId, activationAudit.AdminUserId);
        Assert.Equal("Initial approved version", activationAudit.Reason);
    }

    [Fact]
    public async Task PreviouslyValidDraftCannotActivateAfterGameVersionIsRemovedFromAllowlist()
    {
        var settings = new MatchAuthoritySettings
        {
            EnableRemoteFixed = true,
            EnableRemoteFixedGameplayTest = true,
            RemoteFixedGameVersions = ["0.1.0"]
        };
        await using var h = await Harness.CreateAsync(settings);
        var adminId = Guid.NewGuid();

        Assert.True((await h.Service.CreateVersionAsync(adminId, Request("RFB-001"), default)).IsSuccess);
        Assert.True((await h.Service.ActivateVersionAsync(adminId, "RFB-001", new()
        {
            ExpectedActiveVersion = string.Empty,
            Reason = "Initial approved version"
        }, default)).IsSuccess);

        var versionedSnapshot = RemoteFixedBalanceTests.ValidSnapshot.Replace("RFB-1", "RFB-2").TrimEnd();
        versionedSnapshot = versionedSnapshot[..^1] +
            ",\"gameplay\":[],\"content\":{\"gameVersion\":\"0.1.0\",\"mode\":\"EXPEDITION\",\"monster\":\"STALKER\",\"route\":\"DEFAULT_ROUTE\"}}";
        var draft = await h.Service.CreateVersionAsync(adminId,
            Request("RFB-002", json: versionedSnapshot), default);
        Assert.True(draft.IsSuccess, draft.Message);
        var auditCountBeforeRevalidation = await h.Db.RemoteFixedBalanceAuditEntries.CountAsync();

        settings.RemoteFixedGameVersions = ["0.1.1"];
        var activation = await h.Service.ActivateVersionAsync(adminId, "RFB-002", new()
        {
            ExpectedActiveVersion = "RFB-001",
            Reason = "Compatibility was withdrawn"
        }, default);

        Assert.False(activation.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, activation.ErrorCode);
        h.Db.ChangeTracker.Clear();
        Assert.Equal("RFB-001", await h.Db.RemoteFixedBalanceVersions
            .Where(item => item.IsActive)
            .Select(item => item.Version)
            .SingleAsync());
        Assert.False((await h.Db.RemoteFixedBalanceVersions
            .SingleAsync(item => item.Version == "RFB-002")).IsActive);
        Assert.Equal(auditCountBeforeRevalidation,
            await h.Db.RemoteFixedBalanceAuditEntries.CountAsync());
    }

    [Fact] public async Task RejectUnknownMap()
    {
        await using var h = await Harness.CreateAsync();
        var result = await h.Service.CreateVersionAsync(Guid.NewGuid(), Request("RFB-001", "Unknown"), default);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
    }

    [Fact] public async Task RejectDuplicateVersion()
    {
        await using var h = await Harness.CreateAsync(); await h.Create("RFB-001");
        Assert.Equal(ErrorCodes.Conflict, (await h.Create("RFB-001")).ErrorCode);
    }

    [Fact] public async Task ListFiltersByMapAndDifficulty()
    {
        await using var h = await Harness.CreateAsync(); await h.Create("A");
        await h.Service.CreateVersionAsync(Guid.NewGuid(), Request("B", "Scene_A", "HARD"), default);
        var result = await h.Service.GetVersionsAsync(new() { MapId = "SciFi", Difficulty = "NORMAL" }, default);
        Assert.Equal("A", Assert.Single(result.Data!.Items).Version);
    }

    [Fact] public async Task ListPaginationIsDeterministic()
    {
        await using var h = await Harness.CreateAsync(); await h.Create("B"); await h.Create("A");
        var first = await h.Service.GetVersionsAsync(new() { Page = 1, PageSize = 1 }, default);
        var second = await h.Service.GetVersionsAsync(new() { Page = 2, PageSize = 1 }, default);
        Assert.Equal(["A", "B"], new[] { first.Data!.Items[0].Version, second.Data!.Items[0].Version });
    }

    [Fact] public async Task VersionDetailPreservesExactSnapshot()
    {
        await using var h = await Harness.CreateAsync(); await h.Create("RFB-001");
        Assert.Equal(RemoteFixedBalanceTests.ValidSnapshot, (await h.Service.GetVersionAsync("RFB-001", default)).Data!.SnapshotJson);
    }

    [Fact]
    public async Task DetailFindsActiveVersionBeyondFirstHundred()
    {
        await using var h = await Harness.CreateAsync();
        var snapshot = RemoteFixedBalanceTests.ValidSnapshot;
        var hash = RemoteFixedBalanceContract.ComputeHash(snapshot);
        var now = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 101; i++)
            h.Db.RemoteFixedBalanceVersions.Add(new RemoteFixedBalanceVersion
            {
                Version = $"RFB-{i:000}", MapId = "SciFi", Difficulty = "NORMAL",
                ProtocolVersion = RemoteFixedBalanceContract.ProtocolVersion,
                SnapshotJson = snapshot, ContentHash = hash, IsActive = i == 0,
                CreatedAtUtc = now.AddMinutes(i)
            });
        await h.Db.SaveChangesAsync();

        var page = await h.Service.GetVersionsAsync(new()
        { MapId = "SciFi", Difficulty = "NORMAL", Page = 1, PageSize = 100 }, default);
        Assert.True(page.IsSuccess);
        Assert.DoesNotContain(page.Data!.Items, item => item.IsActive);
        var detail = await h.Service.GetVersionAsync("RFB-100", default);
        Assert.True(detail.IsSuccess);
        Assert.Equal("RFB-000", detail.Data!.ActiveVersion);
    }

    [Fact]
    public async Task PaginationOverflowIsRejected()
    {
        await using var h = await Harness.CreateAsync();
        var result = await h.Service.GetVersionsAsync(new() { Page = int.MaxValue, PageSize = 100 }, default);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
    }

    private static CreateRemoteFixedVersionRequest Request(string version, string map = "SciFi", string difficulty = "NORMAL", string? json = null) =>
        new() { Version = version, MapId = map, Difficulty = difficulty, SnapshotJson = json ?? RemoteFixedBalanceTests.ValidSnapshot };

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private Harness(
            SqliteConnection connection,
            AppDbContext db,
            MatchAuthoritySettings settings)
        {
            this.connection = connection; Db = db;
            Settings = settings;
            Service = new(db, TimeProvider.System, Options.Create(settings));
        }
        public AppDbContext Db { get; }
        public MatchAuthoritySettings Settings { get; }
        public RemoteFixedBalanceAdminService Service { get; }
        public Task<ServiceResult<RemoteFixedVersionDetailResponse>> Create(string version, string? json = null) =>
            Service.CreateVersionAsync(Guid.NewGuid(), Request(version, json: json), default);
        public static async Task<Harness> CreateAsync(MatchAuthoritySettings? settings = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new(connection, db, settings ?? new MatchAuthoritySettings());
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
