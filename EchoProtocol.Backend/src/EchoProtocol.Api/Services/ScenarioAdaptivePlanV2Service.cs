using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EchoProtocol.Api.Services;

public sealed class ScenarioAdaptivePlanV2Service(
    AppDbContext db, IScenarioSnapshotReadService snapshots, TimeProvider clock)
    : IScenarioAdaptivePlanV2Service
{
    public const string PolicyVersion = "AED_V2_POLICY_V1";
    public const string BaselineVersion = "AED_DIFFICULTY_V2|NORMAL";

    // The order and candidates mirror the Unity AEDv2Catalog contract. Any catalog change
    // needs a new policy version and a contract test before gameplay can be enabled.
    private static readonly (string Key, double Normal, double Relief, double Pressure, bool ReliefOnly, bool PreMatchOnly)[] Rules =
    [
        ("SupportBonus", 0, 2, 0, true, true),
        ("ReviveBonusPerZone", 0, 1, 0, true, false),
        ("DetectionAcquireSeconds", 1.25, 1.50, 1.00, false, false),
        ("DetectionForgetSeconds", 25, 20, 30, false, false),
        ("ChaseSpeed", 7.5, 7.0, 8.0, false, false),
        ("SearchSeconds", 2.25, 1.5, 3, false, false),
        ("HearingMultiplier", 1, 0.85, 1.15, false, false),
        ("SeekPlayersAfterSeconds", 120, 150, 90, false, false),
        ("CoreCarrierAfterSeconds", 25, 30, 20, false, false),
        ("SpecialCooldownSeconds", 420, 540, 360, false, false),
        ("DoorBreakSeconds", 4.5, 5.5, 3.5, false, false),
        ("PatrolSpeed", 6.5, 6, 7, false, false),
        ("PostChaseCooldownSeconds", 18, 28, 12, false, false),
        ("PostAttackCooldownSeconds", 22, 32, 15, false, false),
        ("SameRoomCooldownSeconds", 15, 22, 10, false, false),
        ("JumpEntryReuseSeconds", 120, 180, 90, false, false),
        ("PostSpecialCooldownSeconds", 20, 30, 15, false, false),
        ("ObjectiveNoiseInvestigationEnabled", 1, 0, 1, true, false),
        ("Zone1MinionCap", 1, 0, 1, true, true),
        ("Zone2MinionCap", 2, 1, 2, true, true),
        ("CoreCarrierPressureEnabled", 1, 0, 1, true, false)
    ];

    public static double[] NormalPlanValues() => Rules.Select(x => x.Normal).ToArray();

    public async Task<ServiceResult<ScenarioAdaptivePlanV2Dto>> SubmitAsync(
        Guid callerUserId, Guid matchId, SubmitScenarioAdaptivePlanV2Request request,
        CancellationToken ct = default)
    {
        if (request.DecisionId == Guid.Empty || matchId == Guid.Empty ||
            request.PlanValues is null || request.PlanValues.Length != Rules.Length ||
            request.PolicyVersion != PolicyVersion || request.BaselineVersion != BaselineVersion ||
            !IsFingerprint(request.SnapshotFingerprint, 64) ||
            !IsFingerprint(request.RosterIdentity, 64) ||
            request.CommitStatus is not ("PROPOSED" or "SHADOW_ONLY" or "COMMITTED"))
            return Fail("Invalid AED v2 plan request", ErrorCodes.ValidationError);

        var preMatch = request.DecisionPoint == "PRE_MATCH" && request.PhaseOrdinal == 0;
        var boundary = request.DecisionPoint is "ALLOWED_PHASE_BOUNDARY" or "FINAL_HUNT_SETUP"
            && request.PhaseOrdinal > 0 && IsFingerprint(request.EvidenceFingerprint, 64);
        if (!preMatch && !boundary)
            return Fail("Invalid AED v2 decision point", ErrorCodes.ValidationError);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var match = await db.MatchAuthorityBindings.SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        var auth = CheckHost(match, callerUserId);
        if (auth is not null) return Fail(auth.Value.Message, auth.Value.Code);
        var existing = await db.Set<ScenarioAdaptivePlanV2>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.DecisionId == request.DecisionId, ct);
        var scenario = await db.ScenarioDecisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.MatchId == matchId && x.IsCurrent, ct);
        if (scenario is null || scenario.ResolutionMode != ScenarioResolutionMode.Adaptive
            || scenario.SnapshotId != request.SnapshotId)
            return Fail("AED v2 snapshot is stale", ErrorCodes.ScenarioDecisionStaleRoster);
        var read = await snapshots.GetAsync(callerUserId, matchId, scenario.DecisionId, ct);
        if (!read.IsSuccess || read.Data is null ||
            read.Data.SnapshotValidity != "VALID" ||
            read.Data.SnapshotContentFingerprint != request.SnapshotFingerprint ||
            read.Data.RosterIdentity != request.RosterIdentity)
            return Fail("AED v2 snapshot is stale or ineligible", ErrorCodes.ScenarioDecisionStaleRoster);
        if (existing is not null)
        {
            if (existing.MatchId != matchId || existing.HostUserId != callerUserId ||
                !SameRequest(existing, request))
                return Fail("AED v2 decision ID has conflicting semantics", ErrorCodes.ScenarioDecisionIdentityConflict);
            await tx.RollbackAsync(ct);
            return ServiceResult<ScenarioAdaptivePlanV2Dto>.Success(Map(existing, true), "AED v2 decision replayed");
        }
        if (preMatch ? match!.Status != MatchAuthorityStatus.Lobby : match!.Status != MatchAuthorityStatus.InMatch)
            return Fail("AED v2 decision window is closed", ErrorCodes.ScenarioDecisionWindowClosed);

        var hasPending = await db.Set<ScenarioAdaptivePlanV2>()
            .AnyAsync(x => x.MatchId == matchId
                && x.CommitStatus == "COMMITTED"
                && x.ApplyStatus == "PENDING", ct);
        if (hasPending)
            return Fail("Previous AED v2 plan is pending",
                ErrorCodes.ScenarioApplyConflict);

        var previous = await db.Set<ScenarioAdaptivePlanV2>()
            .AsNoTracking()
            .Where(x => x.MatchId == matchId
                && x.CommitStatus == "COMMITTED"
                && x.ApplyStatus == "APPLIED")
            .OrderByDescending(x => x.PhaseOrdinal)
            .FirstOrDefaultAsync(ct);
        if (previous is not null && previous.PhaseOrdinal >= request.PhaseOrdinal)
            return Fail("AED v2 phase ordinal is stale", ErrorCodes.ScenarioDecisionConflict);
        if (await db.Set<ScenarioAdaptivePlanV2>().AnyAsync(
                x => x.MatchId == matchId && x.PhaseOrdinal == request.PhaseOrdinal, ct))
            return Fail("AED v2 phase already has a decision", ErrorCodes.ScenarioDecisionConflict);
        var before = previous is null ? NormalPlanValues()
            : JsonSerializer.Deserialize<double[]>(previous.PlanValuesJson);
        if (before is null || before.Length != Rules.Length ||
            ComputePlanFingerprint(before) != request.PreviousPlanFingerprint ||
            !TryValidateChange(before, request, preMatch, out var changed))
            return Fail("AED v2 candidate is outside the approved transition", ErrorCodes.ScenarioApplyConflict);

        var now = clock.GetUtcNow().UtcDateTime;
        var plan = new ScenarioAdaptivePlanV2
        {
            DecisionId = request.DecisionId, MatchId = matchId, HostUserId = callerUserId,
            PhaseOrdinal = request.PhaseOrdinal, DecisionPoint = request.DecisionPoint,
            PolicyVersion = request.PolicyVersion, BaselineVersion = request.BaselineVersion,
            PreviousPlanFingerprint = request.PreviousPlanFingerprint,
            ResultingPlanFingerprint = request.ResultingPlanFingerprint,
            ChangedKey = Rules[changed].Key, PreviousValue = request.PreviousValue,
            AppliedValue = request.AppliedValue, AdaptationIntent = request.AdaptationIntent,
            DecisionReason = request.DecisionReason, SnapshotId = request.SnapshotId,
            SnapshotFingerprint = request.SnapshotFingerprint,
            EvidenceFingerprint = request.EvidenceFingerprint, RosterIdentity = request.RosterIdentity,
            PlanValuesJson = JsonSerializer.Serialize(request.PlanValues),
            CommitStatus = request.CommitStatus,
            ApplyStatus = request.CommitStatus == "COMMITTED" ? "PENDING" : "NOT_APPLICABLE",
            CommittedAtUtc = request.CommitStatus == "COMMITTED" ? now : null
        };
        db.Set<ScenarioAdaptivePlanV2>().Add(plan);
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception exception) when (IsConcurrencyFailure(exception))
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var stored = await db.Set<ScenarioAdaptivePlanV2>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.DecisionId == request.DecisionId, ct);
            return stored is not null && stored.MatchId == matchId && SameRequest(stored, request)
                ? ServiceResult<ScenarioAdaptivePlanV2Dto>.Success(Map(stored, true), "AED v2 decision replayed")
                : Fail("Concurrent AED v2 decision conflict", ErrorCodes.ScenarioDecisionConflict);
        }
        return ServiceResult<ScenarioAdaptivePlanV2Dto>.Success(Map(plan, false), "AED v2 plan recorded");
    }

    public async Task<ServiceResult<ScenarioAdaptivePlanV2Dto>> ConfirmAppliedAsync(
        Guid callerUserId, Guid matchId, Guid decisionId, string planFingerprint,
        CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var match = await db.MatchAuthorityBindings.SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        var auth = CheckHost(match, callerUserId);
        if (auth is not null) return Fail(auth.Value.Message, auth.Value.Code);
        if (match!.Status == MatchAuthorityStatus.Ended)
            return Fail("AED v2 apply window is closed", ErrorCodes.ScenarioDecisionWindowClosed);
        var plan = await db.Set<ScenarioAdaptivePlanV2>()
            .SingleOrDefaultAsync(x => x.DecisionId == decisionId && x.MatchId == matchId, ct);
        if (plan is null) return Fail("AED v2 plan was not found", ErrorCodes.ScenarioDecisionNotFound);
        if (plan.ApplyStatus == "ABORTED")
            return Fail("AED v2 plan was aborted",
                ErrorCodes.ScenarioApplyConflict);
        if (plan.CommitStatus != "COMMITTED" || plan.ResultingPlanFingerprint != planFingerprint)
            return Fail("AED v2 plan was not committed", ErrorCodes.ScenarioApplyConflict);
        if (plan.AppliedAtUtc is not null)
        {
            await tx.RollbackAsync(ct);
            return ServiceResult<ScenarioAdaptivePlanV2Dto>.Success(Map(plan, true), "AED v2 apply replayed");
        }
        var scenario = await db.ScenarioDecisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.MatchId == matchId && x.IsCurrent, ct);
        if (scenario is null || scenario.SnapshotId != plan.SnapshotId ||
            !(await snapshots.GetAsync(callerUserId, matchId, scenario.DecisionId, ct)).IsSuccess)
            return Fail("AED v2 snapshot is stale", ErrorCodes.ScenarioDecisionStaleRoster);
        plan.ApplyStatus = "APPLIED";
        plan.AppliedAtUtc = clock.GetUtcNow().UtcDateTime;
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception exception) when (IsConcurrencyFailure(exception))
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var stored = await db.Set<ScenarioAdaptivePlanV2>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.DecisionId == decisionId && x.MatchId == matchId, ct);
            return stored?.AppliedAtUtc is not null && stored.ResultingPlanFingerprint == planFingerprint
                ? ServiceResult<ScenarioAdaptivePlanV2Dto>.Success(Map(stored, true), "AED v2 apply replayed")
                : Fail("Concurrent AED v2 apply conflict", ErrorCodes.ScenarioApplyConflict);
        }
        return ServiceResult<ScenarioAdaptivePlanV2Dto>.Success(Map(plan, false), "AED v2 apply confirmed");
    }

    public async Task<ServiceResult<ScenarioAdaptivePlanV2Dto>> AbortPendingAsync(
        Guid callerUserId, Guid matchId, Guid decisionId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var match = await db.MatchAuthorityBindings.SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        var auth = CheckHost(match, callerUserId);
        if (auth is not null) return Fail(auth.Value.Message, auth.Value.Code);
        var plan = await db.Set<ScenarioAdaptivePlanV2>()
            .SingleOrDefaultAsync(x => x.DecisionId == decisionId && x.MatchId == matchId, ct);
        if (plan is null) return Fail("AED v2 plan was not found", ErrorCodes.ScenarioDecisionNotFound);
        if (plan.ApplyStatus == "APPLIED")
            return Fail("Applied AED v2 plan cannot be aborted",
                ErrorCodes.ScenarioApplyConflict);
        if (plan.ApplyStatus == "ABORTED")
        {
            await tx.RollbackAsync(ct);
            return ServiceResult<ScenarioAdaptivePlanV2Dto>.Success(
                Map(plan, true), "AED v2 abort replayed");
        }
        if (plan.CommitStatus != "COMMITTED" || plan.ApplyStatus != "PENDING")
            return Fail("Only a pending committed AED v2 plan can be aborted",
                ErrorCodes.ScenarioApplyConflict);

        plan.ApplyStatus = "ABORTED";
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception exception) when (IsConcurrencyFailure(exception))
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var stored = await db.Set<ScenarioAdaptivePlanV2>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.DecisionId == decisionId && x.MatchId == matchId, ct);
            return stored?.CommitStatus == "COMMITTED" && stored.ApplyStatus == "ABORTED"
                ? ServiceResult<ScenarioAdaptivePlanV2Dto>.Success(
                    Map(stored, true), "AED v2 abort replayed")
                : Fail("Concurrent AED v2 abort conflict", ErrorCodes.ScenarioApplyConflict);
        }
        return ServiceResult<ScenarioAdaptivePlanV2Dto>.Success(
            Map(plan, false), "AED v2 plan aborted");
    }

    private (string Message, string Code)? CheckHost(MatchAuthorityBinding? match, Guid caller)
    {
        if (match is null) return ("Match was not found", ErrorCodes.MatchNotFound);
        if (match.HostUserId != caller) return ("Only the verified host may submit AED v2 plans", ErrorCodes.MatchAuthorityForbidden);
        if (match.LeaseExpiresAtUtc < clock.GetUtcNow().UtcDateTime)
            return ("Host authority lease expired", ErrorCodes.MatchLeaseExpired);
        return null;
    }

    private static bool TryValidateChange(double[] before, SubmitScenarioAdaptivePlanV2Request r,
        bool preMatch, out int changed)
    {
        changed = -1;
        for (var i = 0; i < Rules.Length; i++)
        {
            var spec = Rules[i];
            var value = r.PlanValues[i];
            if (!double.IsFinite(value) || (value != spec.Normal && value != spec.Relief && value != spec.Pressure))
                return false;
            if (value == before[i]) continue;
            if (changed >= 0 || (spec.PreMatchOnly && !preMatch) ||
                (i is 0 or 1 && value < before[i]) ||
                (i == 1 && r.DecisionPoint == "FINAL_HUNT_SETUP")) return false;
            changed = i;
        }
        if (changed < 0 || ComputePlanFingerprint(r.PlanValues) != r.ResultingPlanFingerprint)
            return false;
        var rule = Rules[changed];
        var expected = r.AdaptationIntent switch
        {
            "RELIEVE" => rule.Relief,
            "INCREASE_PRESSURE" when !rule.ReliefOnly => rule.Pressure,
            _ => double.NaN
        };
        return r.ChangedKey == rule.Key && r.PreviousValue == before[changed]
            && r.AppliedValue == r.PlanValues[changed] && r.AppliedValue == expected
            && !string.IsNullOrWhiteSpace(r.DecisionReason);
    }

    public static string ComputePlanFingerprint(double[] values)
    {
        if (values is null || values.Length != Rules.Length || values.Any(x => !double.IsFinite(x)))
            throw new ArgumentException("AED v2 plan needs one finite value per key", nameof(values));
        var text = new StringBuilder(BaselineVersion);
        for (var i = 0; i < Rules.Length; i++)
            text.Append('|').Append(Rules[i].Key).Append('=')
                .Append(values[i].ToString("R", CultureInfo.InvariantCulture));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))[..16];
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes).ToString("N");
    }

    private static bool IsFingerprint(string? value, int length) => value?.Length == length
        && value.All(Uri.IsHexDigit);

    private static bool IsConcurrencyFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException postgres &&
                postgres.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
                return true;
        return exception is DbUpdateException;
    }

    private static bool SameRequest(ScenarioAdaptivePlanV2 stored, SubmitScenarioAdaptivePlanV2Request r) =>
        stored.PhaseOrdinal == r.PhaseOrdinal && stored.DecisionPoint == r.DecisionPoint &&
        stored.PolicyVersion == r.PolicyVersion && stored.BaselineVersion == r.BaselineVersion &&
        stored.PreviousPlanFingerprint == r.PreviousPlanFingerprint &&
        stored.ResultingPlanFingerprint == r.ResultingPlanFingerprint &&
        stored.ChangedKey == r.ChangedKey && stored.PreviousValue == r.PreviousValue &&
        stored.AppliedValue == r.AppliedValue && stored.AdaptationIntent == r.AdaptationIntent &&
        stored.DecisionReason == r.DecisionReason && stored.SnapshotId == r.SnapshotId &&
        stored.SnapshotFingerprint == r.SnapshotFingerprint &&
        stored.EvidenceFingerprint == r.EvidenceFingerprint &&
        stored.RosterIdentity == r.RosterIdentity && stored.CommitStatus == r.CommitStatus &&
        r.PlanValues is not null &&
        (JsonSerializer.Deserialize<double[]>(stored.PlanValuesJson)?.SequenceEqual(r.PlanValues) ?? false);

    private static ScenarioAdaptivePlanV2Dto Map(ScenarioAdaptivePlanV2 p, bool replay) => new(
        p.MatchId, p.DecisionId, p.PhaseOrdinal, p.DecisionPoint, p.PolicyVersion,
        p.BaselineVersion, p.PreviousPlanFingerprint, p.ResultingPlanFingerprint,
        p.ChangedKey, p.PreviousValue, p.AppliedValue, p.AdaptationIntent,
        p.DecisionReason, p.SnapshotId, p.SnapshotFingerprint, p.EvidenceFingerprint,
        p.RosterIdentity, p.CommitStatus, p.ApplyStatus, p.CommittedAtUtc,
        p.AppliedAtUtc, replay);

    private static ServiceResult<ScenarioAdaptivePlanV2Dto> Fail(string message, string code) =>
        ServiceResult<ScenarioAdaptivePlanV2Dto>.Failure(message, code);
}
