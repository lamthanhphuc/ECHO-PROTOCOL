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

public sealed partial class AEDV02DecisionService(AppDbContext db, AEDV02SourceVerifier sources,
    ITelemetryEventRepository telemetry, TimeProvider clock, IOptions<AEDV02PilotSettings> settings)
{
    internal const string ApprovedChaseRollback = "APPROVED_CHASE_ROLLBACK";
    internal const string ApprovedRelativeRollback = "APPROVED_RELATIVE_ROLLBACK";
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
        if (await db.Set<AEDV02Cancellation>().AnyAsync(x => x.DecisionId == request.DecisionId, ct)) return Fail("ABORT_DECISION_CANCELLED");
        var replay = await db.AEDV02Decisions.AsNoTracking().SingleOrDefaultAsync(x => x.DecisionId == request.DecisionId, ct);
        if (replay != null) return replay.MatchId == matchId && replay.HostUserId == caller && replay.RequestFingerprint == requestFingerprint
            ? ServiceResult<AEDV02DecisionResult>.Success(Map(replay, true)) : Fail("AED_V02_DECISION_IDENTITY_CONFLICT", ErrorCodes.ScenarioDecisionIdentityConflict);
        var scenario = await db.ScenarioDecisions.AsNoTracking().Include(x => x.Snapshot).Include(x => x.ScenarioConfig).SingleOrDefaultAsync(x => x.MatchId == matchId && x.IsCurrent, ct);
        if (scenario == null || scenario.ResolutionMode != ScenarioResolutionMode.Adaptive || scenario.SnapshotId != request.SnapshotId ||
            scenario.Snapshot.SnapshotContentFingerprint != request.SnapshotFingerprint || scenario.Snapshot.RosterIdentity != request.RosterIdentity)
            return Fail("HOLD_STALE_SNAPSHOT", ErrorCodes.ScenarioDecisionStaleRoster);
        var managed = await AdaptiveConfigurationService.ForVersionAsync(db, scenario.PolicyConfigVersion, ct);
        var policy = managed?.Pilot ?? AdaptiveConfigurationService.DefaultPilot;
        if (managed is not null && scenario.UsedFixedFallback) return Fail("HOLD_PREMATCH_FIXED_FALLBACK");
        var read = await new ScenarioSnapshotReadService(db, clock).GetAsync(caller, matchId, scenario.DecisionId, ct);
        bool coldStart = read.IsSuccess && read.Data!.SnapshotValidity == "PARTIAL" && read.Data.ReasonCodes is { Length: > 0 } &&
            read.Data.ReasonCodes.All(x => x is "PROFILE_MISSING" or "INSUFFICIENT_OBSERVED_PROFILE_EVIDENCE");
        bool neutralOrRelief = Enum.GetValues<AEDV02Key>().All(x => !AEDV02Plan.IsAdverse(x, AEDV02Plan.Normal().Get(x), proposed.Get(x)));
        // Rev02 permits current-evidence relief for cold-start players; their historical status stays PARTIAL.
        if (!read.IsSuccess || read.Data!.SnapshotValidity != "VALID" && !(coldStart && neutralOrRelief)) return Fail("HOLD_INVALID_SNAPSHOT");
        if (await db.MatchPlayerBindings.AnyAsync(x => x.MatchId == matchId && x.DisconnectedAtUtc != null, ct)) return Fail("HOLD_ROSTER_NOT_CURRENT");
        var previousRow = await db.AEDV02Decisions.AsNoTracking().Where(x => x.MatchId == matchId && x.ApprovalStatus == "APPROVED" && (x.ApplyStatus == "APPLIED" || x.ApplyStatus == "FAILED_EFFECT"))
            .OrderByDescending(x => x.PlanRevision).FirstOrDefaultAsync(ct);
        var previous = previousRow == null ? AEDV02Plan.Normal() : new AEDV02Plan(JsonSerializer.Deserialize<double[]>(previousRow.PlanValuesJson)!);
        if (request.ExpectedPlanRevision != (previousRow?.PlanRevision ?? 0) || request.PreviousPlanFingerprint != previous.Fingerprint() || (preMatch && previousRow != null)) return Fail("ABORT_STALE_REVISION");
        if (await db.AEDV02Decisions.AnyAsync(x => x.MatchId == matchId && x.ApprovalStatus == "APPROVED" && x.ApplyStatus == "PENDING", ct)) return Fail("HOLD_PREVIOUS_RECEIPT_PENDING");
        if (await db.AEDV02Decisions.AnyAsync(x => x.MatchId == matchId && x.PhaseOrdinal == request.PhaseOrdinal && x.DecisionPoint == request.DecisionPoint && x.WindowSequence >= request.WindowSequence, ct)) return Fail("HOLD_WINDOW_ALREADY_USED");
        var initialOptional = previousRow?.InitialOptionalBudget ?? (int)proposed.Get(AEDV02Key.OptionalToolBudget);
        AEDV02SourceResult? source = null;
        string hold = managed is not null && Enum.GetValues<AEDV02Key>().Any(key =>
            !managed.Parameters[key.ToString()].Tiers.Contains(proposed.Get(key))) ? "HOLD_ADMIN_TIER_DISABLED" : string.Empty;
        if (!preMatch)
        {
            if (!AEDV02WorldSample.Hash(request.SourceFingerprint)) return Fail("HOLD_INSUFFICIENT_EVIDENCE");
            var verified = await sources.VerifyAsync(matchId, new AEDV02SourceRequest(request.PhaseOrdinal, request.StartTick, request.EndTick, request.SceneManifestFingerprint, request.Support?.Kind == "EMERGENCY"), ct);
            if (!verified.IsSuccess || verified.Data!.Fingerprint != request.SourceFingerprint) return Fail(verified.IsSuccess ? "HOLD_SOURCE_FINGERPRINT_MISMATCH" : verified.Message);
            source = verified.Data;
            if (point == AEDV02DecisionPoint.SAFE_WINDOW && request.TargetPhaseName != source.PhaseName) return Fail("HOLD_PHASE_SOURCE_MISMATCH");
            if (source.PlanRevision != request.ExpectedPlanRevision || source.PlanFingerprint != request.PreviousPlanFingerprint) return Fail("ABORT_STALE_SOURCE_REVISION");
            if (!source.SafeWindow && !(request.Support?.Kind == "EMERGENCY" && source.SupportSafeWindow)) hold = "HOLD_SAFE_WINDOW_CLOSED";
            if (source.RosterIdentity != request.RosterIdentity) return Fail("HOLD_STALE_ROSTER");
        }
        var windowId = AEDV02Plan.WindowId(matchId, request.PhaseOrdinal, point, request.WindowSequence, request.StartTick, request.EndTick, request.SourceFingerprint);
        if (request.WindowId != windowId) return Fail("AED_V02_WINDOW_ID_INVALID", ErrorCodes.ValidationError);
        bool relative = authority.BalanceBuildVersion != null;
        if (!proposed.TryValidateChange(previous, point, source?.LogicalPhase == 4 || AEDV02PhaseMap.LogicalPhase(request.TargetPhaseName) == 4,
            initialOptional, out var changedKey, out var reason, relative))
        {
            if (!(request.Support?.IsValid() == true && !preMatch && proposed.ToArray().SequenceEqual(previous.ToArray()) && reason == "HOLD_SINGLE_DELTA_REQUIRED")) hold = reason;
        }
        if (proposed.Get(AEDV02Key.CabinetAmbushEnabled) != 0 && !settings.Value.CabinetAmbushEnabled) hold = "HOLD_CABINET_AMBUSH_DISABLED";
        var changed = Enum.GetValues<AEDV02Key>().Where(x => previous.Get(x) != proposed.Get(x)).ToArray();
        double[]? resolvedValues = null;
        double[]? previousResolved = null;
        double? previousChase = null;
        int? previousOptionalMask = null;
        int zone3Baseline = 0;
        Func<AEDV02Key, double, bool>? canResolve = null;
        if (relative)
        {
            if (authority.MapId != "SciFi" || authority.Difficulty != "NORMAL" || authority.ResolutionMode != "ADAPTIVE")
                return Fail("HOLD_AUTOMATIC_SCOPE_UNSUPPORTED");
            string? snapshot = null;
            if (authority.BalanceSource == "REMOTE_FIXED")
            {
                snapshot = await db.RemoteFixedBalanceVersions.AsNoTracking()
                    .Where(x => x.Version == authority.BalanceVersion)
                    .Select(x => x.SnapshotJson).SingleOrDefaultAsync(ct);
                if (snapshot == null || !RemoteFixedBalanceContract.IsValidSnapshot(snapshot, authority.MapId) ||
                    RemoteFixedBalanceContract.ComputeHash(snapshot) != authority.BalanceHash)
                    return Fail("HOLD_LOCKED_BALANCE_INVALID");
            }
            var (patrol, basis) = RemoteFixedBalanceContract.AdaptiveBasis(snapshot);
            canResolve = (key, tier) => AEDV02Plan.TryResolveRelative(key, patrol, basis[(int)key], tier, out _);
            var prior = previousRow == null ? null : ReadResolvedValues(previousRow);
            var applied = basis.ToArray();
            prior?.CopyTo(applied, 0);
            previousResolved = applied;
            previousOptionalMask = (int)applied[12];
            zone3Baseline = (int)basis[(int)AEDV02Key.Zone3AdaptiveMinionCap];
            previousChase = applied[(int)AEDV02Key.ChaseSpeed];
            resolvedValues = basis;
            foreach (var key in Enum.GetValues<AEDV02Key>())
            {
                int index = (int)key;
                if (index >= (int)AEDV02Key.OptionalToolBudget)
                    resolvedValues[index] = proposed.Get(key);
                else if (proposed.Get(key) == previous.Get(key))
                    resolvedValues[index] = applied[index];
                else if (AEDV02Plan.TryResolveRelative(key, patrol, basis[index], proposed.Get(key), out var value))
                    resolvedValues[index] = value;
                else
                    hold = key == AEDV02Key.ChaseSpeed ? "HOLD_RELATIVE_CHASE_LIMIT" : "HOLD_RELATIVE_FIELD_LIMIT";
            }
            if (AEDV02Plan.TryResolveOptionalMask((int)basis[12],
                (int)proposed.Get(AEDV02Key.OptionalToolBudget) - initialOptional, out var mask))
            {
                resolvedValues[12] = mask;
                resolvedValues[(int)AEDV02Key.OptionalToolBudget] = AEDV02Plan.OptionalCount(mask);
            }
            else hold = "HOLD_RELATIVE_FIELD_LIMIT";
        }
        bool adverse = changed.Any(x => AEDV02Plan.IsAdverse(x, previous.Get(x), proposed.Get(x)) && (relative && !preMatch || x != AEDV02Key.CabinetAmbushEnabled));
        bool preMatchLimit = false;
        if (preMatch)
        {
            var legacy = scenario.ScenarioConfig;
            var baseline = await db.ScenarioConfigs.AsNoTracking().SingleOrDefaultAsync(x =>
                x.ScenarioConfigId == legacy.FallbackConfigId && x.ScenarioConfigVersion == legacy.FallbackConfigVersion, ct);
            double mappedSpeed = 0d;
            bool mappedIncrease = baseline != null && AEDV02Plan.TryMapPreMatchChaseIncrease(baseline.ChaseSpeed, legacy.ChaseSpeed, out mappedSpeed);
            if (mappedIncrease && canResolve?.Invoke(AEDV02Key.ChaseSpeed, mappedSpeed) == false)
            {
                mappedSpeed = AEDV02Plan.Normal().Get(AEDV02Key.ChaseSpeed);
                preMatchLimit = true;
            }
            if (relative && changed.Any(key => key is AEDV02Key.Zone1MinionCap or AEDV02Key.Zone2MinionCap or AEDV02Key.Zone3AdaptiveMinionCap))
                hold = "HOLD_MINION_ZONE_INACTIVE";
            if (ScenarioFingerprint.Config(legacy) != scenario.ScenarioConfigFingerprint || baseline == null ||
                (managed is null ? !baseline.IsFixedFallback : !AdaptiveConfigurationService.IsApprovedFixed(baseline)) ||
                legacy.DetectionFillRate != baseline.DetectionFillRate || legacy.DetectionDecayRate != baseline.DetectionDecayRate ||
                legacy.ChaseSpeed != baseline.ChaseSpeed && !mappedIncrease || legacy.SearchDuration != baseline.SearchDuration)
                hold = "HOLD_PREMATCH_LEGACY_TUNING_UNMAPPED";
            else if (mappedIncrease && proposed.Get(AEDV02Key.ChaseSpeed) != mappedSpeed)
                hold = "HOLD_PREMATCH_CHASE_SPEED_MISMATCH";
            else if (legacy.SupportItemBudget is < 0 or > 2 || proposed.Get(AEDV02Key.OptionalToolBudget) != legacy.SupportItemBudget)
                hold = "HOLD_PREMATCH_SUPPLY_MISMATCH";
            else if (managed is not null && !MatchesManagedPreMatch(managed, proposed, mappedIncrease ? mappedSpeed : null, canResolve))
                hold = "HOLD_PREMATCH_CONFIG_MISMATCH";
            else if (hold.Length == 0 && (managed is null
                ? changed.Any(x => x is not (AEDV02Key.CabinetAmbushEnabled or AEDV02Key.OptionalToolBudget))
                : adverse))
            {
                var users = await db.MatchPlayerBindings.Where(x => x.MatchId == matchId).Select(x => x.UserId).ToArrayAsync(ct);
                var history = await new AEDV02HistoricalCompetenceService(db, telemetry, clock).ReadAsync(matchId, users, request.SceneManifestFingerprint, "Stalker", ct);
                if (changed.Count(x => (int)x < 6) > 1 || changed.Any(x => x is not (AEDV02Key.CabinetAmbushEnabled or AEDV02Key.OptionalToolBudget) && (int)x >= 6) ||
                    history.Values.Any(x => !x.ReadyForIncrease)) hold = "HOLD_HISTORICAL_OPPORTUNITIES_REQUIRED";
            }
        }
        if (preMatch && proposed.Get(AEDV02Key.ResourceGrade) != 0) hold = "HOLD_RESOURCE_PLACEMENT_SOURCE_REQUIRED";
        if (request.Support != null && (preMatch || !request.Support.IsValid() || changed.Length != 0 || request.ProposedReservations != null)) return Fail("AED_V02_SUPPORT_ACTION_INVALID", ErrorCodes.ValidationError);
        if (!preMatch && source != null && hold.Length == 0 && request.Support != null)
            hold = await ValidateSupport(matchId, request.Support, source, content.ManifestJson, policy, ct);
        if (!preMatch && source != null && changed.Contains(AEDV02Key.ResourceGrade) && hold.Length == 0 &&
            !await ValidatePlacement(matchId, request.ProposedReservations, source, (int)proposed.Get(AEDV02Key.ResourceGrade), content.ManifestJson, ct, relative)) hold = "HOLD_RESOURCE_PLACEMENT_SOURCE_REQUIRED";
        if (!preMatch && source != null && changed.Contains(AEDV02Key.ResourceGrade) && proposed.Get(AEDV02Key.ResourceGrade) == 3 &&
            source.RecentDownUsers.Length == 0 && source.PlayerPressure.Values.All(x => x < .85)) hold = "HOLD_RESOURCE_EMERGENCY_NOT_VERIFIED";
        if (request.ProposedReservations != null && !changed.Contains(AEDV02Key.ResourceGrade)) return Fail("AED_V02_PLACEMENT_ACTION_INVALID", ErrorCodes.ValidationError);
        if (!preMatch && source != null && hold.Length == 0)
        {
            if (coldStart && !source.PressureComplete && request.Support?.Kind != "EMERGENCY") hold = "HOLD_EVIDENCE_INCOMPLETE";
            int additionalZone3Minions = relative && source.Zone3MinionPopulation >= 0
                ? Math.Max(0, source.Zone3MinionPopulation - zone3Baseline) : source.Zone3MinionPopulation;
            if (!proposed.HasWorldPressureSlot(initialOptional, additionalZone3Minions, relative)) hold = "HOLD_PRESSURE_HANDOFF_WORLD_NOT_REVERTED";
            if (changedKey == AEDV02Key.OptionalToolBudget && relative)
            {
                if (source.Supply == null || source.Supply.ReservationGrade != 0 || source.Supply.SpawnedZones.Any(zone =>
                    AEDV02Plan.OptionalForZone(previousOptionalMask!.Value, zone) != AEDV02Plan.OptionalForZone((int)resolvedValues![12], zone)))
                    hold = "HOLD_OPTIONAL_ALREADY_MATERIALIZED";
            }
            else if (changedKey == AEDV02Key.OptionalToolBudget && (source.Supply == null ||
                proposed.Get(changedKey) < source.Supply.OptionalMaterialized || proposed.Get(changedKey) > source.Supply.InitialOptionalBudget))
                hold = "HOLD_OPTIONAL_ALREADY_MATERIALIZED";
            else if (changedKey == AEDV02Key.OptionalToolBudget && !source.Supply!.Reservations.GroupBy(x => x.Zone).Any(x =>
                x.Count() > 5 && !source.Supply.SpawnedZones.Contains(x.Key) &&
                Math.Min(1, Math.Max(0, previous.Get(changedKey) - x.Key + 1)) != Math.Min(1, Math.Max(0, proposed.Get(changedKey) - x.Key + 1))))
                hold = "HOLD_NO_UNSPAWNED_OPTIONAL";
            if (relative && changedKey == AEDV02Key.CabinetAmbushEnabled && adverse &&
                (source.Zone is not (1 or 2) || proposed.PositiveTokens(initialOptional, true) != 0))
                hold = "HOLD_CABINET_POLICY";
            if (relative && (changedKey is AEDV02Key.Zone1MinionCap or AEDV02Key.Zone2MinionCap or AEDV02Key.Zone3AdaptiveMinionCap) &&
                (source.Zone != (int)changedKey - (int)AEDV02Key.Zone1MinionCap + 1 ||
                 source.Zone != AEDV02PhaseMap.LogicalPhase(request.TargetPhaseName)))
                hold = "HOLD_MINION_ZONE_INACTIVE";
            if (changedKey == AEDV02Key.Zone3AdaptiveMinionCap && proposed.Get(changedKey) > previous.Get(changedKey) &&
                (source.Zone != 3 || source.PhaseName != "ZONE_3_FIND_FRIGATE")) hold = "HOLD_ZONE3_SAFE_ENTRY_REQUIRED";
            if (hold.Length == 0 && adverse)
            {
                if (!source.PressureComplete || source.OpportunityContractVersion != AEDV02PilotOpportunities.Version || source.TeamPressureStatus != "Verified")
                    hold = "HOLD_INSUFFICIENT_EVIDENCE";
                else if (source.TeamPressureTier < 3 || source.PlayerPressure.Values.Any(x => x >= policy.IncreasePressureCeiling) || source.RecentDownUsers.Length != 0) hold = "HOLD_TEAM_OVERLOADED";
                else
                {
                    string axis = (int)changedKey < 6 || relative && changedKey == AEDV02Key.CabinetAmbushEnabled
                        ? "Stalker"
                        : changedKey is AEDV02Key.Zone1MinionCap
                            or AEDV02Key.Zone2MinionCap
                            or AEDV02Key.Zone3AdaptiveMinionCap
                                ? "Minion"
                                : changedKey is AEDV02Key.ResourceGrade
                                    or AEDV02Key.OptionalToolBudget
                                    ? "ResourceManagement"
                                    : "Tool";
                    var history = await new AEDV02HistoricalCompetenceService(db, telemetry, clock).ReadAsync(matchId,
                        source.PlayerPressure.Keys.ToArray(), request.SceneManifestFingerprint, axis, ct);
                    if (history.Values.Any(x => !x.ReadyForIncrease)) hold = "HOLD_HISTORICAL_OPPORTUNITIES_REQUIRED";
                }
            }
            else if (hold.Length == 0 && request.Support == null && proposed.PositiveTokens(initialOptional, relative) >= previous.PositiveTokens(initialOptional, relative))
            {
                int channel = (int)changedKey < 6 || changedKey == AEDV02Key.CabinetAmbushEnabled ? 0 : changedKey is AEDV02Key.Zone1MinionCap or AEDV02Key.Zone2MinionCap or AEDV02Key.Zone3AdaptiveMinionCap ? 1 : 3;
                if ((source.RecentDownUsers.Length == 0 && source.PlayerPressure.Values.All(x => x < policy.ReliefThreshold) &&
                    !((changedKey == AEDV02Key.ResourceGrade || relative && changedKey == AEDV02Key.OptionalToolBudget) && source.ResourceNeeds?.Length > 0)) ||
                    source.PressureComponents.Values.All(x => x[channel] <= 0) &&
                    !(relative && changedKey == AEDV02Key.CabinetAmbushEnabled && source.RecentDownUsers.Length > 0)) hold = "HOLD_RELIEF_CAUSE_UNVERIFIED";
            }
            if (hold.Length == 0 && previousRow != null && previousRow.ChangedKey.Length != 0 &&
                (previousRow.AppliedAtUtc.HasValue || previousRow.ApplyStatus == "FAILED_EFFECT") &&
                (adverse || source.RecentDownUsers.Length == 0) &&
                now - (previousRow.AppliedAtUtc ?? previousRow.ExpiresAtUtc) <
                    TimeSpan.FromSeconds(adverse ? policy.IncreaseCooldownSeconds : policy.ReliefCooldownSeconds)) hold = "HOLD_PACING_COOLDOWN";
        }
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await db.ScenarioDecisions.AnyAsync(x => x.DecisionId == scenario.DecisionId && x.IsCurrent, ct))
            return Fail("HOLD_STALE_SNAPSHOT", ErrorCodes.ScenarioDecisionStaleRoster);
        if (await db.Set<AEDV02Cancellation>().AnyAsync(x => x.DecisionId == request.DecisionId, ct)) return Fail("ABORT_DECISION_CANCELLED");
        var appliedRevision = await db.AEDV02Decisions.Where(x => x.MatchId == matchId && (x.ApplyStatus == "APPLIED" || x.ApplyStatus == "FAILED_EFFECT"))
            .Select(x => (int?)x.PlanRevision).MaxAsync(ct) ?? 0;
        if (appliedRevision != request.ExpectedPlanRevision) return Fail("ABORT_STALE_REVISION");
        if (await db.AEDV02Decisions.AnyAsync(x => x.MatchId == matchId && x.ApprovalStatus == "APPROVED" && x.ApplyStatus == "PENDING", ct)) return Fail("HOLD_PREVIOUS_RECEIPT_PENDING");
        if (await db.AEDV02Decisions.AnyAsync(x => x.MatchId == matchId && x.PhaseOrdinal == request.PhaseOrdinal && x.DecisionPoint == request.DecisionPoint && x.WindowSequence >= request.WindowSequence, ct)) return Fail("HOLD_WINDOW_ALREADY_USED");
        if (!await db.MatchAuthorityBindings.AnyAsync(x => x.MatchId == matchId && x.HostUserId == caller && x.LeaseExpiresAtUtc > clock.GetUtcNow().UtcDateTime &&
            x.Status == (preMatch ? MatchAuthorityStatus.Lobby : MatchAuthorityStatus.InMatch), ct) ||
            await db.MatchPlayerBindings.AnyAsync(x => x.MatchId == matchId && x.DisconnectedAtUtc != null, ct)) return Fail("HOLD_AUTHORITY_NOT_CURRENT");
        int revision = (await db.AEDV02Decisions.Where(x => x.MatchId == matchId).Select(x => (int?)x.PlanRevision).MaxAsync(ct) ?? 0) + 1;
        if (hold.Length != 0) resolvedValues = previousResolved;
        bool rollback = false;
        if (hold.Length == 0 && resolvedValues != null && changed.Length == 1 &&
            previousRow is { ApplyStatus: "APPLIED", ExpectedPlanRevision: > 0 } &&
            previousRow.ChangedKey == changed[0].ToString())
        {
            var prior = await db.AEDV02Decisions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.MatchId == matchId && x.PlanRevision == previousRow.ExpectedPlanRevision &&
                x.ApplyStatus == "APPLIED" && x.ReceiptId != null, ct);
            rollback = prior != null && ReadResolvedValues(prior) is { Length: 13 } confirmed &&
                confirmed.SequenceEqual(resolvedValues);
        }
        var decision = new AEDV02Decision
        {
            DecisionId = request.DecisionId, MatchId = matchId, HostUserId = caller, PhaseOrdinal = request.PhaseOrdinal,
            DecisionPoint = request.DecisionPoint, WindowSequence = request.WindowSequence, WindowId = request.WindowId,
            TargetPhaseName = request.TargetPhaseName,
            ExpectedPlanRevision = request.ExpectedPlanRevision, PlanRevision = revision, PreviousPlanFingerprint = previous.Fingerprint(),
            PlanFingerprint = proposed.Fingerprint(), PlanValuesJson = JsonSerializer.Serialize(proposed.ToArray()),
            StalkerValuesJson = resolvedValues == null ? null : JsonSerializer.Serialize(resolvedValues),
            PreviousChaseSpeed = previousChase,
            SnapshotId = request.SnapshotId, SnapshotFingerprint = request.SnapshotFingerprint, RosterIdentity = request.RosterIdentity,
            SourceFingerprint = request.SourceFingerprint, SceneManifestFingerprint = request.SceneManifestFingerprint,
            PolicyVersion = request.PolicyVersion, CatalogVersion = request.CatalogVersion, RequestFingerprint = requestFingerprint,
            ProposalJson = JsonSerializer.Serialize(request), ActionFingerprint = request.Support?.Fingerprint(request.DecisionId) ?? AEDV02ProposedReservation.Fingerprint(request.ProposedReservations),
            InitialOptionalBudget = initialOptional, ChangedKey = request.Support != null ? "SupportTool" : changed.Length == 1 ? changedKey.ToString() : string.Empty,
            ApprovalStatus = hold.Length == 0 ? "APPROVED" : "HOLD", ApplyStatus = hold.Length == 0 ? "PENDING" : "NOT_APPLICABLE",
            ReasonCode = hold.Length == 0 ? (rollback ? ApprovedRelativeRollback : preMatchLimit ? "APPROVED_BASELINE_AT_RELATIVE_LIMIT" : "APPROVED_REV02_PILOT") : hold, CreatedAtUtc = now,
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

    private static bool MatchesManagedPreMatch(DTOs.Admin.AdaptiveConfiguration config, AEDV02Plan proposed, double? mappedSpeed,
        Func<AEDV02Key, double, bool>? canResolve)
    {
        var baseline = AdaptiveConfigurationService.Baseline(config);
        if (mappedSpeed.HasValue) baseline = baseline.With(AEDV02Key.ChaseSpeed, mappedSpeed.Value);
        if (canResolve != null) baseline = baseline.ForRelativePreMatch(canResolve);
        return Enum.GetValues<AEDV02Key>().All(key => key == AEDV02Key.OptionalToolBudget || proposed.Get(key) == baseline.Get(key));
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
        if (!HasAcceptedCommit(events, decision, receipt.AppliedTick)) return Fail("HOLD_HOST_COMMIT_NOT_ACCEPTED");
        var terminal = closed ? events.SingleOrDefault(x => x.MatchId == matchId && x.EventType == "MATCH_ENDED" && x.SchemaVersion == "1.1") : null;
        if (closed && terminal == null) return Fail("HOLD_MATCH_TERMINAL_PROOF_MISSING");
        var effectEvent = events.SingleOrDefault(x => x.Id == receipt.EffectEventId && x.MatchId == matchId && x.SchemaVersion == "1.3" && x.EventType == "AED_V02_ACTION_EFFECT");
        if (effectEvent == null || effectEvent.Ts < decision.CreatedAtUtc || effectEvent.Ts > decision.ExpiresAtUtc.AddSeconds(20) &&
            decision.ChangedKey is not ("SupportTool" or "ResourceGrade" or "Zone1MinionCap" or "Zone2MinionCap" or "Zone3AdaptiveMinionCap")) return Fail("HOLD_EFFECT_NOT_ACCEPTED");
        if (terminal != null && (effectEvent.EventSequence >= terminal.EventSequence || effectEvent.Ts > terminal.Ts)) return Fail("HOLD_EFFECT_AFTER_MATCH_END");
        AEDV02EffectSource? effect;
        try { effect = JsonSerializer.Deserialize<AEDV02EffectSource>(effectEvent.ValueJson["data"]["effect"].ToJson()); } catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidCastException) { return Fail("AED_V02_EFFECT_INVALID"); }
        var plan = new AEDV02Plan(JsonSerializer.Deserialize<double[]>(decision.PlanValuesJson)!);
        var resolved = ReadResolvedValues(decision);
        var stalkerValues = ReadStalkerValues(decision);
        var proposal = decision.ProposalJson.Length == 0 ? null : JsonSerializer.Deserialize<AEDV02DecisionRequest>(decision.ProposalJson);
        var content = await db.AEDV02SceneContents.AsNoTracking().SingleOrDefaultAsync(x => x.Fingerprint == decision.SceneManifestFingerprint, ct);
        if (content == null || AEDV02Plan.Hash(content.ManifestJson) != content.Fingerprint) return Fail("HOLD_UNTRUSTED_SCENE_MANIFEST");
        var manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(content.ManifestJson);
        if (manifest == null || !manifest.IsValid()) return Fail("HOLD_UNTRUSTED_SCENE_MANIFEST");
        if (effect == null || !effect.IsValid() || effect.DecisionId != decisionId || effect.PlanRevision != decision.PlanRevision ||
            effect.PlanFingerprint != plan.Fingerprint() || effect.ObservedTick < receipt.AppliedTick ||
            (decision.ChangedKey.Length != 0 && (effect.Key != decision.ChangedKey || Math.Abs(effect.ObservedValue -
                (decision.ChangedKey == "SupportTool" ? proposal?.Support?.ToolId ?? -1 :
                 EffectiveValue(plan, resolved, Enum.Parse<AEDV02Key>(decision.ChangedKey)))) > .00001))) return Fail("AED_V02_EFFECT_MISMATCH");
        if (decision.ChangedKey.Length == 0 && effect.Consumer != "AEDPolicyEnvelope") return Fail("AED_V02_EFFECT_MISMATCH");
        var observations = events.Where(x => x.SchemaVersion == "1.3" && x.EventType == "AED_V02_WORLD_SAMPLE" &&
                x.EventSequence > effectEvent.EventSequence && x.Ts >= effectEvent.Ts)
            .Where(x => terminal == null || x.EventSequence < terminal.EventSequence && x.Ts <= terminal.Ts)
            .Select(x => (Event: x, World: JsonSerializer.Deserialize<AEDV02WorldSample>(x.ValueJson["data"]["world"].ToJson())))
            .Where(x => x.World != null && x.World.IsValid() && x.World.EvidenceExclusion.Length == 0 && x.World.Tick >= effect.ObservedTick &&
                x.World.PlanRevision == decision.PlanRevision && x.World.PlanFingerprint == decision.PlanFingerprint && x.World.RosterIdentity == decision.RosterIdentity &&
                x.World.SceneManifestFingerprint == decision.SceneManifestFingerprint && ObservedWorldLineage(events, matchId, x.Event, x.World))
            .ToArray();
        var samples = observations.Select(x => x.World!).ToArray();
        if (samples.Length == 0) return Fail("HOLD_WORLD_EFFECT_NOT_OBSERVED");
        if ((decision.DecisionPoint == "PRE_MATCH" || effect.Consumer == "StalkerFusionRuntime") &&
            !samples.Any(world => world.Stalkers.Length > 0 && world.Stalkers.All(stalker => stalker.MatchesStalkerConsumer(plan, stalkerValues))))
            return Fail("HOLD_STALKER_CONSUMER_NOT_OBSERVED");
        var supportInstances = await sources.AcceptedSupportInstances(matchId, events, ct);
        int requiredOptionalMask = 0;
        if (!(decision.DecisionPoint == "PRE_MATCH" && decision.ExpectedPlanRevision == 0) &&
            decision.ChangedKey == nameof(AEDV02Key.OptionalToolBudget) && OptionalMask(resolved) is { } toolMask)
        {
            var prior = await db.AEDV02Decisions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.MatchId == matchId && x.PlanRevision == decision.ExpectedPlanRevision, ct);
            if (prior == null || OptionalMask(ReadResolvedValues(prior)) is not { } priorMask)
                return Fail("HOLD_SUPPLY_RESERVATION_SOURCE_REQUIRED");
            requiredOptionalMask = toolMask & ~priorMask;
        }
        if ((decision.DecisionPoint == "PRE_MATCH" && (resolved?.Length == 13 || plan.Get(AEDV02Key.OptionalToolBudget) > 0) || decision.ChangedKey is "OptionalToolBudget" or "ResourceGrade") &&
            !observations.Any(x => x.World!.Supply?.SpawnedZones.Contains(1) == true && AEDV02SourceVerifier.VerifySupply(x.World, manifest, plan,
                decision.InitialOptionalBudget, events.Where(e => e.MatchId == matchId), x.Event.EventSequence, supportInstances, OptionalMask(resolved), proposal?.ProposedReservations, requiredOptionalMask)))
            return Fail("HOLD_SUPPLY_RESERVATION_SOURCE_REQUIRED");
        if (decision.ChangedKey == "SupportTool")
        {
            var support = proposal?.Support;
            if (support?.IsValid() != true || support.Fingerprint(decision.DecisionId) != decision.ActionFingerprint || effect.NetworkObjectIds.Length != 1 ||
                effect.Consumer != "TeamToolWorldSpawn" || !supportInstances.Contains(effect.NetworkObjectIds[0]) ||
                !samples.Any(x => x.Tools.Any(t => t.InstanceId == effect.NetworkObjectIds[0] && t.ToolId == support.ToolId && t.Zone == support.Zone && t.MarkerId == support.MarkerId &&
                    t.Location == "World" && t.Origin == "WorldSpawn" && manifest.HasReachableGeometry(new[] { t.X, t.Y, t.Z },
                        new[] { x.Players.Single(p => p.UserId == support.UserId).X, x.Players.Single(p => p.UserId == support.UserId).Y, x.Players.Single(p => p.UserId == support.UserId).Z }, x.Doors))))
                return Fail("HOLD_SUPPORT_WORLD_EFFECT_NOT_OBSERVED");
        }
        else if (decision.ChangedKey.Length != 0)
        {
            var key = Enum.Parse<AEDV02Key>(decision.ChangedKey);
            var consumer = key is AEDV02Key.Zone1MinionCap or AEDV02Key.Zone2MinionCap or AEDV02Key.Zone3AdaptiveMinionCap ? "PlayerSpawner"
                : key is AEDV02Key.OptionalToolBudget or AEDV02Key.ResourceGrade ? "TeamToolWorldSpawn"
                : key == AEDV02Key.CabinetAmbushEnabled ? "AEDPolicyEnvelope" : "StalkerFusionRuntime";
            if (effect.Consumer != consumer) return Fail("AED_V02_EFFECT_MISMATCH");
            if (consumer == "TeamToolWorldSpawn" && key == AEDV02Key.ResourceGrade &&
                (proposal?.ProposedReservations == null || !samples.Any(world => proposal.ProposedReservations.All(p =>
                    world.Supply?.Reservations.Any(r => r.Zone == p.Zone && r.ToolId == p.ToolId && r.MarkerId == p.MarkerId) == true ||
                    world.Tools.Any(t => t.Zone == p.Zone && t.ToolId == p.ToolId && t.MarkerId == p.MarkerId && t.Origin == "WorldSpawn")))))
                return Fail("HOLD_RESOURCE_PLACEMENT_SOURCE_REQUIRED");
            if (consumer == "StalkerFusionRuntime" && (effect.NetworkObjectIds.Length == 0 || !samples.Any(x => effect.NetworkObjectIds.All(id => x!.Stalkers.Any(s => s.NetworkId == id))))) return Fail("HOLD_WORLD_EFFECT_NOT_OBSERVED");
            if (consumer == "PlayerSpawner" && key == AEDV02Key.Zone3AdaptiveMinionCap && plan.Get(key) == 1 &&
                !samples.Any(x => x.Minions.Count(m => m.Zone == 3 && effect.NetworkObjectIds.Contains(m.NetworkId)) >= effect.ObservedValue))
                return Fail("HOLD_WORLD_EFFECT_NOT_OBSERVED");
        }
        decision.ReceiptId = receipt.ReceiptId; decision.EffectEventId = receipt.EffectEventId; decision.AppliedTick = receipt.AppliedTick;
        decision.ApplyStatus = "APPLIED"; decision.AppliedAtUtc = clock.GetUtcNow().UtcDateTime;
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (Exception ex) when (ex is DbUpdateException || ex is Npgsql.PostgresException { SqlState: "40001" or "40P01" or "23505" })
        { db.Entry(decision).State = EntityState.Detached; return Fail("AED_V02_RECEIPT_CONFLICT"); }
        return ServiceResult<AEDV02DecisionResult>.Success(Map(decision, false));
    }

    private static bool ObservedWorldLineage(IEnumerable<TelemetryEventDocument> events, Guid matchId,
        TelemetryEventDocument occurrence, AEDV02WorldSample world)
    {
        var users = world.Players.Select(x => x.UserId).ToHashSet();
        var roster = AEDV02Plan.Hash(matchId.ToString("D") + "|" + string.Join(',', users.Order().Select(x => x.ToString("D"))));
        if (roster != world.RosterIdentity || !AEDPhase8LifecycleReconcilerV1.TryReconstruct(events, matchId, occurrence.EventSequence, users, false,
            out var tools, out var minions, out _)) return false;
        return tools.Count == world.Tools.Length && world.Tools.All(x => tools.TryGetValue(x.InstanceId, out var item) &&
            item.ToolId == x.ToolId && item.Zone == x.Zone && item.Location == x.Location && item.Origin == x.Origin &&
            item.Charges == x.RemainingCharges && item.Owner == x.OwnerUserId) &&
            minions.Count == world.Minions.Length && world.Minions.All(x => minions.TryGetValue(x.NetworkId, out var zone) && zone == x.Zone);
    }

    public async Task<ServiceResult<AEDV02DecisionResult>> AbortAsync(Guid caller, Guid matchId, Guid decisionId, CancellationToken ct = default, bool mayHaveCommitted = false)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var binding = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        var row = await db.AEDV02Decisions.SingleOrDefaultAsync(x => x.MatchId == matchId && x.DecisionId == decisionId, ct);
        if (binding == null || binding.HostUserId != caller || decisionId == Guid.Empty ||
            binding.Status != MatchAuthorityStatus.Ended && binding.LeaseExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
            return Fail("AED_V02_HOST_REQUIRED", ErrorCodes.MatchAuthorityForbidden);
        if (row == null)
        {
            if (mayHaveCommitted) return Fail("HOLD_HOST_COMMIT_NOT_ACCEPTED");
            var cancellation = await db.Set<AEDV02Cancellation>().SingleOrDefaultAsync(x => x.DecisionId == decisionId, ct);
            if (cancellation != null && (cancellation.MatchId != matchId || cancellation.HostUserId != caller)) return Fail("AED_V02_DECISION_IDENTITY_CONFLICT");
            bool cancelledReplay = cancellation != null;
            if (cancellation == null)
            {
                cancellation = new AEDV02Cancellation { DecisionId = decisionId, MatchId = matchId, HostUserId = caller, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
                db.Set<AEDV02Cancellation>().Add(cancellation);
            }
            try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
            catch (Exception ex) when (ex is DbUpdateException || ex is Npgsql.PostgresException { SqlState: "40001" or "40P01" or "23505" })
            { db.Entry(cancellation).State = EntityState.Detached; return Fail("AED_V02_RECEIPT_CONFLICT"); }
            return ServiceResult<AEDV02DecisionResult>.Success(Map(new AEDV02Decision { DecisionId = decisionId, MatchId = matchId,
                ApprovalStatus = "CANCELLED", ApplyStatus = "ABORTED", ReasonCode = "CANCELLED_BEFORE_APPROVAL", PlanValuesJson = "[]" }, cancelledReplay));
        }
        if (row.ApplyStatus == "APPLIED") return Fail("AED_V02_APPLIED_CANNOT_ABORT");
        bool replay = row.ApplyStatus is "ABORTED" or "FAILED_EFFECT";
        var events = await telemetry.LoadAcceptedMatchEventsAsync(matchId, ct);
        if (binding.Status == MatchAuthorityStatus.Ended && events.Count(x => x.MatchId == matchId && x.EventType == "MATCH_ENDED" && x.SchemaVersion == "1.1") != 1)
            return Fail("HOLD_MATCH_TERMINAL_PROOF_MISSING");
        bool committed = HasAcceptedCommit(events, row, null);
        if (mayHaveCommitted && !committed) return Fail("HOLD_HOST_COMMIT_NOT_ACCEPTED");
        row.ApplyStatus = committed ? "FAILED_EFFECT" : "ABORTED";
        if (row.ApplyStatus == "FAILED_EFFECT") row.ReasonCode = "COMMITTED_EFFECT_FAILED";
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (Exception ex) when (ex is DbUpdateException || ex is Npgsql.PostgresException { SqlState: "40001" or "40P01" or "23505" })
        { db.Entry(row).State = EntityState.Detached; return Fail("AED_V02_RECEIPT_CONFLICT"); }
        return ServiceResult<AEDV02DecisionResult>.Success(Map(row, replay));
    }
    private static ServiceResult<AEDV02DecisionResult> Fail(string reason, string code = ErrorCodes.ScenarioApplyConflict) => ServiceResult<AEDV02DecisionResult>.Failure(reason, code);
    internal static AEDV02DecisionResult Map(AEDV02Decision x, bool replay) => new(x.DecisionId, x.MatchId, x.PhaseOrdinal, x.DecisionPoint,
        x.WindowSequence, x.WindowId, x.PlanRevision, x.ExpectedPlanRevision, x.PreviousPlanFingerprint, x.PlanFingerprint,
        JsonSerializer.Deserialize<double[]>(x.PlanValuesJson)!, x.RosterIdentity, x.SourceFingerprint, x.SceneManifestFingerprint,
        x.PolicyVersion, x.CatalogVersion, x.ApprovalStatus, x.ApplyStatus, x.ReasonCode, x.ExpiresAtUtc, x.TargetPhaseName, x.SnapshotId, x.SnapshotFingerprint, replay, x.ActionFingerprint,
        ReadStalkerValues(x), x.PreviousChaseSpeed, ReadResolvedValues(x) is { Length: 13 } values ? values[..12] : null, OptionalMask(ReadResolvedValues(x)), x.InitialOptionalBudget);

    // Older revisions store six Stalker values. Relative revisions append the other consumers and the tool-zone mask.
    internal static int? OptionalMask(double[]? values) => values is { Length: 13 } ? (int)values[12] : null;

    internal static double EffectiveValue(AEDV02Plan plan, double[]? resolved, AEDV02Key key) =>
        resolved != null && (int)key < resolved.Length ? resolved[(int)key] : plan.Get(key);

    internal static double[]? ReadStalkerValues(AEDV02Decision x) => ReadResolvedValues(x)?[..6];

    internal static double[]? ReadResolvedValues(AEDV02Decision x) =>
        x.StalkerValuesJson == null ? null : JsonSerializer.Deserialize<double[]>(x.StalkerValuesJson);
}
