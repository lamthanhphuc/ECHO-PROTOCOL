using System.Text.RegularExpressions;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EchoProtocol.Api.Services;

public sealed partial class RemoteFixedBalanceAdminService(
    AppDbContext db,
    TimeProvider timeProvider,
    IOptions<MatchAuthoritySettings> settings)
{
    private static readonly string[] Maps = MatchContentCatalog.Entries.Select(x => x.Map).ToArray();
    private static readonly string[] Difficulties = MatchContentCatalog.Difficulties;

    public async Task<ServiceResult<AdminPagedResponse<RemoteFixedVersionResponse>>> GetVersionsAsync(
        RemoteFixedVersionsQuery query, CancellationToken ct)
    {
        var pagination = ValidateQuery(query, out var map, out var difficulty);
        if (pagination != null) return ServiceResult<AdminPagedResponse<RemoteFixedVersionResponse>>.Failure(pagination, ErrorCodes.ValidationError);

        var versions = db.RemoteFixedBalanceVersions.AsNoTracking();
        if (map != null) versions = versions.Where(x => x.MapId == map);
        if (difficulty != null) versions = versions.Where(x => x.Difficulty == difficulty);
        return ServiceResult<AdminPagedResponse<RemoteFixedVersionResponse>>.Success(
            await PageAsync(versions.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Version)
                .Select(x => ToResponse(x)), query, ct));
    }

    public async Task<ServiceResult<RemoteFixedVersionDetailResponse>> GetVersionAsync(string version, CancellationToken ct)
    {
        var item = await db.RemoteFixedBalanceVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Version == version, ct);
        return item == null
            ? ServiceResult<RemoteFixedVersionDetailResponse>.Failure("Remote Fixed version not found.", ErrorCodes.NotFound)
            : ServiceResult<RemoteFixedVersionDetailResponse>.Success(await ToDetailAsync(item, ct));
    }

    public async Task<ServiceResult<RemoteFixedVersionDetailResponse>> CreateVersionAsync(
        Guid adminUserId, CreateRemoteFixedVersionRequest request, CancellationToken ct)
    {
        var version = request.Version.Trim();
        var map = request.MapId.Trim();
        var difficulty = request.Difficulty.Trim().ToUpperInvariant();
        if (version.Length is < 1 or > 80 || !VersionPattern().IsMatch(version))
            return Invalid<RemoteFixedVersionDetailResponse>("Version must start with a letter or digit and contain only letters, digits, '.', '_' or '-'.");
        if (!Maps.Contains(map)) return Invalid<RemoteFixedVersionDetailResponse>("Unsupported map.");
        if (!Difficulties.Contains(difficulty)) return Invalid<RemoteFixedVersionDetailResponse>("Unsupported difficulty.");
        if (!RemoteFixedBalanceContract.IsValidSnapshot(request.SnapshotJson, map))
            return Invalid<RemoteFixedVersionDetailResponse>("Invalid gameplay balance snapshot for this map.");
        if (RemoteFixedBalanceContract.GameVersion(request.SnapshotJson) is { } build && !settings.Value.RemoteFixedGameVersions.Contains(build))
            return Invalid<RemoteFixedVersionDetailResponse>("Unity build is outside the registered compatibility list.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.RemoteFixedBalanceVersions.AnyAsync(x => x.Version == version, ct))
            return Conflict<RemoteFixedVersionDetailResponse>("Version already exists.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var item = new RemoteFixedBalanceVersion
        {
            Version = version, MapId = map, Difficulty = difficulty,
            ProtocolVersion = RemoteFixedBalanceContract.ProtocolVersion,
            SnapshotJson = request.SnapshotJson,
            ContentHash = RemoteFixedBalanceContract.ComputeHash(request.SnapshotJson),
            IsActive = false, CreatedAtUtc = now
        };
        db.RemoteFixedBalanceVersions.Add(item);
        db.RemoteFixedBalanceAuditEntries.Add(Audit("CREATE", item, adminUserId, null, item.Version, "Version created", now));
        try
        {
            await db.SaveChangesAsync(ct);
            var detail = await ToDetailAsync(item, ct);
            await transaction.CommitAsync(ct);
            return ServiceResult<RemoteFixedVersionDetailResponse>.Success(detail, "Version created inactive.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict<RemoteFixedVersionDetailResponse>("Version already exists.");
        }
    }

    public Task<ServiceResult<RemoteFixedActivationResponse>> ActivateVersionAsync(
        Guid adminUserId, string version, ChangeRemoteFixedActivationRequest request, CancellationToken ct) =>
        ChangeActivationAsync(adminUserId, version, request, true, ct);

    public Task<ServiceResult<RemoteFixedActivationResponse>> DeactivateVersionAsync(
        Guid adminUserId, string version, ChangeRemoteFixedActivationRequest request, CancellationToken ct) =>
        ChangeActivationAsync(adminUserId, version, request, false, ct);

    public async Task<ServiceResult<AdminPagedResponse<RemoteFixedAuditResponse>>> GetHistoryAsync(
        RemoteFixedVersionsQuery query, CancellationToken ct)
    {
        var pagination = ValidateQuery(query, out var map, out var difficulty);
        if (pagination != null) return ServiceResult<AdminPagedResponse<RemoteFixedAuditResponse>>.Failure(pagination, ErrorCodes.ValidationError);
        var history = db.RemoteFixedBalanceAuditEntries.AsNoTracking();
        if (map != null) history = history.Where(x => x.MapId == map);
        if (difficulty != null) history = history.Where(x => x.Difficulty == difficulty);
        return ServiceResult<AdminPagedResponse<RemoteFixedAuditResponse>>.Success(
            await PageAsync(history.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
                .Select(x => new RemoteFixedAuditResponse
                {
                    Id = x.Id, Action = x.Action, MapId = x.MapId, Difficulty = x.Difficulty,
                    PreviousVersion = x.PreviousVersion, TargetVersion = x.TargetVersion,
                    AdminUserId = x.AdminUserId, Reason = x.Reason, CreatedAtUtc = x.CreatedAtUtc
                }), query, ct));
    }

    public ServiceResult<RemoteFixedStatusResponse> GetStatus() =>
        ServiceResult<RemoteFixedStatusResponse>.Success(new()
        {
            RemoteFixedEnabled = settings.Value.EnableRemoteFixedTest,
            MatchDataPolicy = RemoteFixedBalanceContract.TestOnly,
            ProtocolVersion = RemoteFixedBalanceContract.ProtocolVersion,
            SupportedMaps = Maps
        });

    private async Task<ServiceResult<RemoteFixedActivationResponse>> ChangeActivationAsync(
        Guid adminUserId, string version, ChangeRemoteFixedActivationRequest request, bool activate, CancellationToken ct)
    {
        var reason = request.Reason.Trim();
        if (reason.Length is < 1 or > 500) return Invalid<RemoteFixedActivationResponse>("Reason is required and must not exceed 500 characters.");
        var target = await db.RemoteFixedBalanceVersions.SingleOrDefaultAsync(x => x.Version == version, ct);
        if (target == null) return ServiceResult<RemoteFixedActivationResponse>.Failure("Remote Fixed version not found.", ErrorCodes.NotFound);
        if (!RemoteFixedBalanceContract.IsValidSnapshot(target.SnapshotJson, target.MapId) ||
            !string.Equals(RemoteFixedBalanceContract.ComputeHash(target.SnapshotJson), target.ContentHash, StringComparison.Ordinal))
            return Invalid<RemoteFixedActivationResponse>("Stored snapshot or hash is invalid.");
        if (activate && RemoteFixedBalanceContract.GameVersion(target.SnapshotJson) is { } build &&
            !settings.Value.RemoteFixedGameVersions.Contains(build))
            return Invalid<RemoteFixedActivationResponse>("Unity build is outside the registered compatibility list.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({target.MapId + "\n" + target.Difficulty + "\n" + target.ProtocolVersion}, 0))", ct);
        await db.Entry(target).ReloadAsync(ct);

        var active = await db.RemoteFixedBalanceVersions.SingleOrDefaultAsync(x =>
            x.MapId == target.MapId && x.Difficulty == target.Difficulty &&
            x.ProtocolVersion == target.ProtocolVersion && x.IsActive, ct);
        var expected = string.IsNullOrWhiteSpace(request.ExpectedActiveVersion) ? null : request.ExpectedActiveVersion.Trim();
        if (!string.Equals(active?.Version, expected, StringComparison.Ordinal))
            return Conflict<RemoteFixedActivationResponse>("Active version changed. Refresh and try again.");

        if (activate && active?.Version == target.Version)
            return ServiceResult<RemoteFixedActivationResponse>.Success(new() { PreviousActiveVersion = target.Version, ActiveVersion = target.Version, Changed = false });
        if (!activate && active?.Version != target.Version)
            return Conflict<RemoteFixedActivationResponse>("Only the active version can be deactivated.");

        var previous = active?.Version;
        if (active != null)
        {
            active.IsActive = false;
            await db.SaveChangesAsync(ct);
        }
        if (activate)
        {
            target.IsActive = true;
            await db.SaveChangesAsync(ct);
        }
        var now = timeProvider.GetUtcNow().UtcDateTime;
        db.RemoteFixedBalanceAuditEntries.Add(Audit(activate ? "ACTIVATE" : "DEACTIVATE", target, adminUserId,
            previous, activate ? target.Version : null, reason, now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<RemoteFixedActivationResponse>.Success(new()
        {
            PreviousActiveVersion = previous, ActiveVersion = activate ? target.Version : null, Changed = true
        });
    }

    private static string? ValidateQuery(RemoteFixedVersionsQuery query, out string? map, out string? difficulty)
    {
        map = string.IsNullOrWhiteSpace(query.MapId) ? null : query.MapId.Trim();
        difficulty = string.IsNullOrWhiteSpace(query.Difficulty) ? null : query.Difficulty.Trim().ToUpperInvariant();
        if (query.Page < 1 || query.PageSize is < 1 or > 100 ||
            (long)(query.Page - 1) * query.PageSize > int.MaxValue) return "Invalid pagination.";
        if (map != null && !Maps.Contains(map)) return "Unsupported map.";
        if (difficulty != null && !Difficulties.Contains(difficulty)) return "Unsupported difficulty.";
        return null;
    }

    private static async Task<AdminPagedResponse<T>> PageAsync<T>(IQueryable<T> query, AdminPagedQuery page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        return new()
        {
            Items = await query.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct),
            Page = page.Page, PageSize = page.PageSize, TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)page.PageSize)
        };
    }

    private static RemoteFixedVersionResponse ToResponse(RemoteFixedBalanceVersion x) => new()
    {
        Version = x.Version, MapId = x.MapId, Difficulty = x.Difficulty, ProtocolVersion = x.ProtocolVersion,
        ContentHash = x.ContentHash, IsActive = x.IsActive, CreatedAtUtc = x.CreatedAtUtc
    };

    private async Task<RemoteFixedVersionDetailResponse> ToDetailAsync(RemoteFixedBalanceVersion x, CancellationToken ct)
    {
        var activeVersion = await db.RemoteFixedBalanceVersions.AsNoTracking()
            .Where(item => item.MapId == x.MapId && item.Difficulty == x.Difficulty &&
                item.ProtocolVersion == x.ProtocolVersion && item.IsActive)
            .Select(item => item.Version).SingleOrDefaultAsync(ct);
        return new RemoteFixedVersionDetailResponse
        {
            Version = x.Version, MapId = x.MapId, Difficulty = x.Difficulty, ProtocolVersion = x.ProtocolVersion,
            ContentHash = x.ContentHash, IsActive = x.IsActive, CreatedAtUtc = x.CreatedAtUtc,
            SnapshotJson = x.SnapshotJson, ActiveVersion = activeVersion
        };
    }

    private static RemoteFixedBalanceAuditEntry Audit(string action, RemoteFixedBalanceVersion item, Guid adminUserId,
        string? previous, string? target, string reason, DateTime now) => new()
    {
        Id = Guid.NewGuid(), Action = action, MapId = item.MapId, Difficulty = item.Difficulty,
        ProtocolVersion = item.ProtocolVersion, PreviousVersion = previous, TargetVersion = target,
        AdminUserId = adminUserId, Reason = reason, CreatedAtUtc = now
    };

    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Failure(message, ErrorCodes.ValidationError);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Failure(message, ErrorCodes.Conflict);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
