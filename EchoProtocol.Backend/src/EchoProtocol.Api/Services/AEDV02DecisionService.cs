using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed class AEDV02DecisionService(AppDbContext db, AEDV02SourceVerifier sources,
    ITelemetryEventRepository telemetry, TimeProvider clock, IOptions<AEDV02PilotSettings> settings)
{
    public async Task<ServiceResult<AEDV02DecisionResult>> SubmitAsync(Guid caller, Guid matchId,
        AEDV02DecisionRequest request, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var authority = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (authority == null || authority.HostUserId != caller) return Fail("AED_V02_HOST_REQUIRED", ErrorCodes.MatchAuthorityForbidden);
        if (authority.LeaseExpiresAtUtc <= now) return Fail("AED_V02_LEASE_EXPIRED", ErrorCodes.MatchLeaseExpired);
        if (!settings.Value.Enabled) return Fail("HOLD_GAMEPLAY_DISABLED");
        if (request == null || request.DecisionId == Guid.Empty || request.ResolutionMode != "Adaptive" || request.Difficulty != "Normal" ||
            request.PolicyVersion != AEDV02PilotPolicy.Version || request.CatalogVersion != AEDV02Plan.CatalogVersion ||
            !Enum.TryParse<AEDV02DecisionPoint>(request.DecisionPoint, out var point) || !Enum.IsDefined(point) ||
            request.DecisionPoint != point.ToString() ||
            AEDV02PhaseMap.LogicalPhase(request.TargetPhaseName) == 0 ||
            request.PhaseOrdinal < 0 || request.WindowSequence < 0 || request.ExpectedPlanRevision < 0 ||
            (point == AEDV02DecisionPoint.SAFE_WINDOW ? request.WindowSequence == 0 : request.WindowSequence != 0) ||
            !AEDV02WorldSample.Hash(request.PreviousPlanFingerprint) || !AEDV02WorldSample.Hash(request.RosterIdentity) ||
            !AEDV02WorldSample.Hash(request.SnapshotFingerprint) || !AEDV02WorldSample.Hash(request.SceneManifestFingerprint)) return Fail("AED_V02_REQUEST_INVALID", ErrorCodes.ValidationError);
        bool preMatch = point == AEDV02DecisionPoint.PRE_MATCH;
        var content = await db.AEDV02SceneContents.AsNoTracking().SingleOrDefaultAsync(x => x.Fingerprint == request.SceneManifestFingerprint, ct);
        if (content == null || AEDV02Plan.Hash(content.ManifestJson) != content.Fingerprint) return Fail("HOLD_UNTRUSTED_SCENE_MANIFEST");
        if (preMatch ? request.PhaseOrdinal != 0 || authority.Status != MatchAuthorityStatus.Lobby
            : request.PhaseOrdinal == 0 || authority.Status != MatchAuthorityStatus.InMatch) return Fail("HOLD_DECISION_WINDOW_CLOSED");
        AEDV02Plan proposed;
        try { proposed = new AEDV02Plan(request.PlanValues); } catch (ArgumentException) { return Fail("AED_V02_PLAN_INVALID", ErrorCodes.ValidationError); }
        var requestFingerprint = AEDV02Plan.Hash(JsonSerializer.Serialize(request));
        var replay = await db.AEDV02Decisions.AsNoTracking().SingleOrDefaultAsync(x => x.DecisionId == request.DecisionId, ct);
        if (replay != null) return replay.MatchId == matchId && replay.HostUserId == caller && replay.RequestFingerprint == requestFingerprint
            ? ServiceResult<AEDV02DecisionResult>.Success(Map(replay, true)) : Fail("AED_V02_DECISION_IDENTITY_CONFLICT", ErrorCodes.ScenarioDecisionIdentityConflict);
        var scenario = await db.ScenarioDecisions.AsNoTracking().Include(x => x.Snapshot).SingleOrDefaultAsync(x => x.MatchId == matchId && x.IsCurrent, ct);
        if (scenario == null || scenario.ResolutionMode != ScenarioResolutionMode.Adaptive || scenario.SnapshotId != request.SnapshotId ||
            scenario.Snapshot.SnapshotContentFingerprint != request.SnapshotFingerprint || scenario.Snapshot.RosterIdentity != request.RosterIdentity)
            return Fail("HOLD_STALE_SNAPSHOT", ErrorCodes.ScenarioDecisionStaleRoster);
        var read = await new ScenarioSnapshotReadService(db, clock).GetAsync(caller, matchId, scenario.DecisionId, ct);
        if (!read.IsSuccess || read.Data!.SnapshotValidity != "VALID") return Fail("HOLD_INVALID_SNAPSHOT");
        if (await db.MatchPlayerBindings.AnyAsync(x => x.MatchId == matchId && x.DisconnectedAtUtc != null, ct)) return Fail("HOLD_ROSTER_NOT_CURRENT");
        var previousRow = await db.AEDV02Decisions.AsNoTracking().Where(x => x.MatchId == matchId && x.ApprovalStatus == "APPROVED" && x.ApplyStatus == "APPLIED")
            .OrderByDescending(x => x.PlanRevision).FirstOrDefaultAsync(ct);
        var previous = previousRow == null ? AEDV02Plan.Normal() : new AEDV02Plan(JsonSerializer.Deserialize<double[]>(previousRow.PlanValuesJson)!);
        if (request.ExpectedPlanRevision != (previousRow?.PlanRevision ?? 0) || request.PreviousPlanFingerprint != previous.Fingerprint() || (preMatch && previousRow != null)) return Fail("ABORT_STALE_REVISION");
        if (await db.AEDV02Decisions.AnyAsync(x => x.MatchId == matchId && x.ApprovalStatus == "APPROVED" && x.ApplyStatus == "PENDING", ct)) return Fail("HOLD_PREVIOUS_RECEIPT_PENDING");
        if (await db.AEDV02Decisions.AnyAsync(x => x.MatchId == matchId && x.PhaseOrdinal == request.PhaseOrdinal && x.DecisionPoint == request.DecisionPoint && x.WindowSequence >= request.WindowSequence, ct)) return Fail("HOLD_WINDOW_ALREADY_USED");
        var initialOptional = previousRow?.InitialOptionalBudget ?? (int)proposed.Get(AEDV02Key.OptionalToolBudget);
        AEDV02SourceResult? source = null;
        string hold = string.Empty;
        if (!preMatch)
        {
            if (!AEDV02WorldSample.Hash(request.SourceFingerprint)) return Fail("HOLD_INSUFFICIENT_EVIDENCE");
            var verified = await sources.VerifyAsync(matchId, new AEDV02SourceRequest(request.PhaseOrdinal, request.StartTick, request.EndTick, request.SceneManifestFingerprint), ct);
            if (!verified.IsSuccess || verified.Data!.Fingerprint != request.SourceFingerprint) return Fail(verified.IsSuccess ? "HOLD_SOURCE_FINGERPRINT_MISMATCH" : verified.Message);
            source = verified.Data;
            if (point == AEDV02DecisionPoint.SAFE_WINDOW && request.TargetPhaseName != source.PhaseName) return Fail("HOLD_PHASE_SOURCE_MISMATCH");
            if (source.PlanRevision != request.ExpectedPlanRevision || source.PlanFingerprint != request.PreviousPlanFingerprint) return Fail("ABORT_STALE_SOURCE_REVISION");
            if (!source.SafeWindow) hold = "HOLD_SAFE_WINDOW_CLOSED";
            if (source.RosterIdentity != request.RosterIdentity) return Fail("HOLD_STALE_ROSTER");
        }
        var windowId = AEDV02Plan.WindowId(matchId, request.PhaseOrdinal, point, request.WindowSequence, request.StartTick, request.EndTick, request.SourceFingerprint);
        if (request.WindowId != windowId) return Fail("AED_V02_WINDOW_ID_INVALID", ErrorCodes.ValidationError);
        if (!proposed.TryValidateChange(previous, point, source?.LogicalPhase == 4 || AEDV02PhaseMap.LogicalPhase(request.TargetPhaseName) == 4,
            initialOptional, out var changedKey, out var reason)) hold = reason;
        if (proposed.Get(AEDV02Key.CabinetAmbushEnabled) != 0 && !settings.Value.CabinetAmbushEnabled) hold = "HOLD_CABINET_AMBUSH_DISABLED";
        var changed = Enum.GetValues<AEDV02Key>().Where(x => previous.Get(x) != proposed.Get(x)).ToArray();
        bool adverse = changed.Any(x => AEDV02Plan.IsAdverse(x, previous.Get(x), proposed.Get(x)) && x != AEDV02Key.CabinetAmbushEnabled);
        if (preMatch && changed.Any(x => x != AEDV02Key.CabinetAmbushEnabled)) hold = "HOLD_HISTORICAL_OPPORTUNITIES_REQUIRED";
        if (!preMatch && source != null && hold.Length == 0)
        {
            if (changedKey == AEDV02Key.Zone3AdaptiveMinionCap && proposed.Get(changedKey) > previous.Get(changedKey) &&
                (source.Zone != 3 || source.PhaseName != "ZONE_3_FIND_FRIGATE")) hold = "HOLD_ZONE3_SAFE_ENTRY_REQUIRED";
            if (adverse)
            {
                if (source.PressureComplete && source.PlayerPressure.Values.Any(x => x >= .25) || source.RecentDownUsers.Length != 0) hold = "HOLD_TEAM_OVERLOADED";
                // Pressure/census evidence cannot certify the unsupported historical competence mapping.
                else hold = "HOLD_HISTORICAL_OPPORTUNITIES_REQUIRED";
            }
            else if (proposed.PositiveTokens(initialOptional) >= previous.PositiveTokens(initialOptional))
            {
                int channel = (int)changedKey < 6 ? 0 : changedKey is AEDV02Key.Zone1MinionCap or AEDV02Key.Zone2MinionCap or AEDV02Key.Zone3AdaptiveMinionCap ? 1 : 3;
                if ((source.RecentDownUsers.Length == 0 && source.PlayerPressure.Values.All(x => x < .65)) ||
                    source.PressureComponents.Values.All(x => x[channel] <= 0)) hold = "HOLD_RELIEF_CAUSE_UNVERIFIED";
            }
            if (previousRow?.AppliedAtUtc != null && previousRow.ChangedKey.Length != 0 &&
                (adverse || source.RecentDownUsers.Length == 0) && now - previousRow.AppliedAtUtc.Value < TimeSpan.FromSeconds(adverse ? 180 : 90)) hold = "HOLD_PACING_COOLDOWN";
        }
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var appliedRevision = await db.AEDV02Decisions.Where(x => x.MatchId == matchId && x.ApplyStatus == "APPLIED")
            .Select(x => (int?)x.PlanRevision).MaxAsync(ct) ?? 0;
        if (appliedRevision != request.ExpectedPlanRevision) return Fail("ABORT_STALE_REVISION");
        if (await db.AEDV02Decisions.AnyAsync(x => x.MatchId == matchId && x.ApprovalStatus == "APPROVED" && x.ApplyStatus == "PENDING", ct)) return Fail("HOLD_PREVIOUS_RECEIPT_PENDING");
        if (await db.AEDV02Decisions.AnyAsync(x => x.MatchId == matchId && x.PhaseOrdinal == request.PhaseOrdinal && x.DecisionPoint == request.DecisionPoint && x.WindowSequence >= request.WindowSequence, ct)) return Fail("HOLD_WINDOW_ALREADY_USED");
        if (!await db.MatchAuthorityBindings.AnyAsync(x => x.MatchId == matchId && x.HostUserId == caller && x.LeaseExpiresAtUtc > clock.GetUtcNow().UtcDateTime &&
            x.Status == (preMatch ? MatchAuthorityStatus.Lobby : MatchAuthorityStatus.InMatch), ct) ||
            await db.MatchPlayerBindings.AnyAsync(x => x.MatchId == matchId && x.DisconnectedAtUtc != null, ct)) return Fail("HOLD_AUTHORITY_NOT_CURRENT");
        int revision = (await db.AEDV02Decisions.Where(x => x.MatchId == matchId).Select(x => (int?)x.PlanRevision).MaxAsync(ct) ?? 0) + 1;
        var decision = new AEDV02Decision
        {
            DecisionId = request.DecisionId, MatchId = matchId, HostUserId = caller, PhaseOrdinal = request.PhaseOrdinal,
            DecisionPoint = request.DecisionPoint, WindowSequence = request.WindowSequence, WindowId = request.WindowId,
            TargetPhaseName = request.TargetPhaseName,
            ExpectedPlanRevision = request.ExpectedPlanRevision, PlanRevision = revision, PreviousPlanFingerprint = previous.Fingerprint(),
            PlanFingerprint = proposed.Fingerprint(), PlanValuesJson = JsonSerializer.Serialize(proposed.ToArray()),
            SnapshotId = request.SnapshotId, SnapshotFingerprint = request.SnapshotFingerprint, RosterIdentity = request.RosterIdentity,
            SourceFingerprint = request.SourceFingerprint, SceneManifestFingerprint = request.SceneManifestFingerprint,
            PolicyVersion = request.PolicyVersion, CatalogVersion = request.CatalogVersion, RequestFingerprint = requestFingerprint,
            InitialOptionalBudget = initialOptional, ChangedKey = changed.Length == 1 ? changedKey.ToString() : string.Empty,
            ApprovalStatus = hold.Length == 0 ? "APPROVED" : "HOLD", ApplyStatus = hold.Length == 0 ? "PENDING" : "NOT_APPLICABLE",
            ReasonCode = hold.Length == 0 ? "APPROVED_REV02_PILOT" : hold, CreatedAtUtc = now,
            ExpiresAtUtc = preMatch ? authority.LeaseExpiresAtUtc : new[] { now.AddSeconds(10), authority.LeaseExpiresAtUtc }.Min()
        };
        db.AEDV02Decisions.Add(decision);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (Exception ex) when (ex is DbUpdateException || ex is Npgsql.PostgresException { SqlState: "40001" or "40P01" or "23505" })
        {
            db.Entry(decision).State = EntityState.Detached;
            return Fail("AED_V02_LEDGER_CONFLICT");
        }
        return ServiceResult<AEDV02DecisionResult>.Success(Map(decision, false));
    }

    public async Task<ServiceResult<AEDV02DecisionResult>> ConfirmAsync(Guid caller, Guid matchId, Guid decisionId, AEDV02ReceiptRequest receipt, CancellationToken ct = default)
    {
        var binding = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (binding == null || binding.HostUserId != caller) return Fail("AED_V02_HOST_REQUIRED", ErrorCodes.MatchAuthorityForbidden);
        bool closed = binding.Status == MatchAuthorityStatus.Ended;
        if (!closed && (binding.LeaseExpiresAtUtc <= clock.GetUtcNow().UtcDateTime || binding.Status != MatchAuthorityStatus.InMatch)) return Fail("HOLD_AUTHORITY_NOT_CURRENT");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var decision = await db.AEDV02Decisions.SingleOrDefaultAsync(x => x.DecisionId == decisionId && x.MatchId == matchId, ct);
        if (decision == null || receipt == null || receipt.ReceiptId == Guid.Empty || receipt.EffectEventId == Guid.Empty || receipt.AppliedTick < 0 ||
            receipt.PlanRevision != decision.PlanRevision || receipt.PlanFingerprint != decision.PlanFingerprint || decision.ApprovalStatus != "APPROVED" || decision.ApplyStatus == "ABORTED") return Fail("AED_V02_RECEIPT_CONFLICT");
        if (decision.ApplyStatus == "APPLIED") return decision.ReceiptId == receipt.ReceiptId && decision.EffectEventId == receipt.EffectEventId && decision.AppliedTick == receipt.AppliedTick
            ? ServiceResult<AEDV02DecisionResult>.Success(Map(decision, true)) : Fail("AED_V02_RECEIPT_IDENTITY_CONFLICT");
        var events = await telemetry.LoadAcceptedMatchEventsAsync(matchId, ct);
        var terminal = closed ? events.SingleOrDefault(x => x.MatchId == matchId && x.EventType == "MATCH_ENDED" && x.SchemaVersion == "1.1") : null;
        if (closed && terminal == null) return Fail("HOLD_MATCH_TERMINAL_PROOF_MISSING");
        var effectEvent = events.SingleOrDefault(x => x.Id == receipt.EffectEventId && x.MatchId == matchId && x.SchemaVersion == "1.3" && x.EventType == "AED_V02_ACTION_EFFECT");
        if (effectEvent == null || effectEvent.Ts < decision.CreatedAtUtc || effectEvent.Ts > decision.ExpiresAtUtc.AddSeconds(20)) return Fail("HOLD_EFFECT_NOT_ACCEPTED");
        if (terminal != null && (effectEvent.EventSequence >= terminal.EventSequence || effectEvent.Ts > terminal.Ts)) return Fail("HOLD_EFFECT_AFTER_MATCH_END");
        AEDV02EffectSource? effect;
        try { effect = JsonSerializer.Deserialize<AEDV02EffectSource>(effectEvent.ValueJson["data"]["effect"].ToJson()); } catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidCastException) { return Fail("AED_V02_EFFECT_INVALID"); }
        var plan = new AEDV02Plan(JsonSerializer.Deserialize<double[]>(decision.PlanValuesJson)!);
        if (effect == null || !effect.IsValid() || effect.DecisionId != decisionId || effect.PlanRevision != decision.PlanRevision ||
            effect.PlanFingerprint != plan.Fingerprint() || effect.ObservedTick < receipt.AppliedTick ||
            (decision.ChangedKey.Length != 0 && (effect.Key != decision.ChangedKey || Math.Abs(effect.ObservedValue - plan.Get(Enum.Parse<AEDV02Key>(decision.ChangedKey))) > .000001))) return Fail("AED_V02_EFFECT_MISMATCH");
        if (decision.ChangedKey.Length == 0 && effect.Consumer != "AEDPolicyEnvelope") return Fail("AED_V02_EFFECT_MISMATCH");
        var samples = events.Where(x => x.SchemaVersion == "1.3" && x.EventType == "AED_V02_WORLD_SAMPLE" &&
                x.EventSequence > effectEvent.EventSequence && x.Ts >= effectEvent.Ts)
            .Where(x => terminal == null || x.EventSequence < terminal.EventSequence && x.Ts <= terminal.Ts)
            .Select(x => JsonSerializer.Deserialize<AEDV02WorldSample>(x.ValueJson["data"]["world"].ToJson()))
            .Where(x => x != null && x.IsValid() && x.EvidenceExclusion.Length == 0 && x.Tick >= effect.ObservedTick &&
                x.PlanRevision == decision.PlanRevision && x.PlanFingerprint == decision.PlanFingerprint && x.RosterIdentity == decision.RosterIdentity &&
                x.SceneManifestFingerprint == decision.SceneManifestFingerprint).ToArray();
        if (samples.Length == 0) return Fail("HOLD_WORLD_EFFECT_NOT_OBSERVED");
        if (decision.ChangedKey.Length != 0)
        {
            var key = Enum.Parse<AEDV02Key>(decision.ChangedKey);
            var consumer = key is AEDV02Key.Zone1MinionCap or AEDV02Key.Zone2MinionCap or AEDV02Key.Zone3AdaptiveMinionCap ? "PlayerSpawner"
                : key is AEDV02Key.OptionalToolBudget or AEDV02Key.ResourceGrade ? "TeamToolWorldSpawn"
                : key == AEDV02Key.CabinetAmbushEnabled ? "AEDPolicyEnvelope" : "StalkerFusionRuntime";
            if (effect.Consumer != consumer) return Fail("AED_V02_EFFECT_MISMATCH");
            if (consumer == "StalkerFusionRuntime" && (effect.NetworkObjectIds.Length == 0 || !samples.Any(x => effect.NetworkObjectIds.All(id => x!.Stalkers.Any(s => s.NetworkId == id))))) return Fail("HOLD_WORLD_EFFECT_NOT_OBSERVED");
            if (consumer == "PlayerSpawner" && key == AEDV02Key.Zone3AdaptiveMinionCap && effect.ObservedValue == 1 &&
                !samples.Any(x => x!.Minions.Any(m => m.Zone == 3 && effect.NetworkObjectIds.Contains(m.NetworkId)))) return Fail("HOLD_WORLD_EFFECT_NOT_OBSERVED");
        }
        decision.ReceiptId = receipt.ReceiptId; decision.EffectEventId = receipt.EffectEventId; decision.AppliedTick = receipt.AppliedTick;
        decision.ApplyStatus = "APPLIED"; decision.AppliedAtUtc = clock.GetUtcNow().UtcDateTime;
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (Exception ex) when (ex is DbUpdateException || ex is Npgsql.PostgresException { SqlState: "40001" or "40P01" or "23505" })
        { db.Entry(decision).State = EntityState.Detached; return Fail("AED_V02_RECEIPT_CONFLICT"); }
        return ServiceResult<AEDV02DecisionResult>.Success(Map(decision, false));
    }

    public async Task<ServiceResult<AEDV02DecisionResult>> AbortAsync(Guid caller, Guid matchId, Guid decisionId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var binding = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        var row = await db.AEDV02Decisions.SingleOrDefaultAsync(x => x.MatchId == matchId && x.DecisionId == decisionId, ct);
        if (binding == null || binding.HostUserId != caller || binding.LeaseExpiresAtUtc <= clock.GetUtcNow().UtcDateTime || row == null) return Fail("AED_V02_HOST_REQUIRED", ErrorCodes.MatchAuthorityForbidden);
        if (row.ApplyStatus == "APPLIED") return Fail("AED_V02_APPLIED_CANNOT_ABORT");
        bool replay = row.ApplyStatus == "ABORTED"; row.ApplyStatus = "ABORTED";
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (Exception ex) when (ex is DbUpdateException || ex is Npgsql.PostgresException { SqlState: "40001" or "40P01" or "23505" })
        { db.Entry(row).State = EntityState.Detached; return Fail("AED_V02_RECEIPT_CONFLICT"); }
        return ServiceResult<AEDV02DecisionResult>.Success(Map(row, replay));
    }
    private static ServiceResult<AEDV02DecisionResult> Fail(string reason, string code = ErrorCodes.ScenarioApplyConflict) => ServiceResult<AEDV02DecisionResult>.Failure(reason, code);
    private static AEDV02DecisionResult Map(AEDV02Decision x, bool replay) => new(x.DecisionId, x.MatchId, x.PhaseOrdinal, x.DecisionPoint,
        x.WindowSequence, x.WindowId, x.PlanRevision, x.ExpectedPlanRevision, x.PreviousPlanFingerprint, x.PlanFingerprint,
        JsonSerializer.Deserialize<double[]>(x.PlanValuesJson)!, x.RosterIdentity, x.SourceFingerprint, x.SceneManifestFingerprint,
        x.PolicyVersion, x.CatalogVersion, x.ApprovalStatus, x.ApplyStatus, x.ReasonCode, x.ExpiresAtUtc, x.TargetPhaseName, x.SnapshotId, x.SnapshotFingerprint, replay);
}
