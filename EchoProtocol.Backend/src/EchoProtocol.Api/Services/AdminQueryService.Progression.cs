using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed partial class AdminQueryService
{
    public async Task<ServiceResult<object>> GetProgressionAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await db.PlayerProfiles.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new { x.Level, x.ExperiencePoints, x.LifetimeExperiencePoints }).SingleOrDefaultAsync(ct);
        if (profile is null) return ServiceResult<object>.Failure("Profile not found", ErrorCodes.NotFound);
        var grants = await db.AdminExperienceGrants.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(20).ToArrayAsync(ct);
        return ServiceResult<object>.Success(new { profile, grants });
    }

    public async Task<ServiceResult<object>> GrantExperienceAsync(Guid adminId, Guid userId,
        AdminGrantExperienceRequest request, CancellationToken ct = default)
    {
        string reason = (request.Reason ?? "").Trim();
        if (adminId == Guid.Empty || request.RequestId == Guid.Empty || request.Amount is < 1 or > 100000 || reason.Length is < 1 or > 200)
            return ServiceResult<object>.Failure("XP phải từ 1–100.000 và cần lý do tối đa 200 ký tự.", ErrorCodes.ValidationError);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var profile = db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? await db.PlayerProfiles.FromSqlInterpolated($"SELECT * FROM \"PlayerProfiles\" WHERE \"UserId\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct)
            : await db.PlayerProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (profile is null) return ServiceResult<object>.Failure("Profile not found", ErrorCodes.NotFound);
        await db.Entry(profile).ReloadAsync(ct);
        var existing = await db.AdminExperienceGrants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.RequestId, ct);
        if (existing != null)
            return existing.UserId == userId && existing.AdminUserId == adminId && existing.Amount == request.Amount && existing.Reason == reason
                ? ServiceResult<object>.Success(existing, "XP grant already applied")
                : ServiceResult<object>.Failure("Request ID already used for another grant", ErrorCodes.Conflict);
        long xp, lifetime;
        try { xp = checked(profile.ExperiencePoints + request.Amount); lifetime = checked(profile.LifetimeExperiencePoints + request.Amount); }
        catch (OverflowException) { return ServiceResult<object>.Failure("XP exceeds supported range", ErrorCodes.ValidationError); }
        int level = Math.Max(profile.Level, profile.LevelCurveVersion == ProgressionCurveV3.Version
            ? ProgressionCurveV3.GetLevel(xp) : new ProgressionPolicyV1().GetLevel(xp));
        var grant = new AdminExperienceGrant { Id = request.RequestId, UserId = userId, AdminUserId = adminId,
            Amount = request.Amount, ExperienceBefore = profile.ExperiencePoints, ExperienceAfter = xp,
            LevelBefore = profile.Level, LevelAfter = level, Reason = reason, CreatedAtUtc = DateTime.UtcNow };
        profile.ExperiencePoints = xp; profile.LifetimeExperiencePoints = lifetime; profile.Level = level; profile.UpdatedAt = grant.CreatedAtUtc;
        db.AdminExperienceGrants.Add(grant);
        await ProgressionCosmeticService.StageEligibleAsync(db, userId, level, grant.CreatedAtUtc, ct);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.GetBaseException() is Npgsql.PostgresException { SqlState: "23505" })
        { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); return ServiceResult<object>.Failure("Request ID already used", ErrorCodes.Conflict); }
        return ServiceResult<object>.Success(grant, "XP granted");
    }
}
