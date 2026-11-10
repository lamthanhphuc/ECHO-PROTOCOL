using System.Data;
using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Admin;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EchoProtocol.Api.Services;

public sealed class ScenarioService(
    AppDbContext db,
    IScenarioConfigRegistry registry,
    IAdaptiveInputSnapshotBuilder snapshotBuilder,
    TimeProvider timeProvider,
    IOptions<AEDPreMatchPolicySettings> policyOptions) : IScenarioService
{
    public async Task<ServiceResult<ScenarioDecisionResponse>> ResolvePreMatchAsync(
        Guid callerUserId, Guid matchId, ResolveScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DecisionId == Guid.Empty)
            return Fail("DecisionId is required", ErrorCodes.ValidationError);
        bool useFixedAfterV02Hold = !string.IsNullOrEmpty(request.FallbackFromDecisionId);
        Guid fallbackFrom = Guid.Empty;
        if (useFixedAfterV02Hold && (!Guid.TryParse(request.FallbackFromDecisionId, out fallbackFrom) ||
            fallbackFrom == Guid.Empty || request.DecisionId != AEDV02Plan.FixedFallbackDecisionId(fallbackFrom) ||
            request.ResolutionMode != ScenarioResolutionMode.Adaptive))
            return Fail("Invalid Fixed fallback decision", ErrorCodes.ValidationError);
        var requestFingerprint = ScenarioFingerprint.Hash(matchId.ToString("D"), request.ResolutionMode.ToString(),
            request.UnityCompatibilityVersion, request.ExperimentCondition?.Trim());
        if (useFixedAfterV02Hold) requestFingerprint = ScenarioFingerprint.Hash(requestFingerprint, fallbackFrom.ToString("D"));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var match = await db.MatchAuthorityBindings.SingleOrDefaultAsync(x => x.MatchId == matchId, cancellationToken);
            var auth = ValidateHost(match, callerUserId, requireLobby: false);
            if (auth is not null) return Fail(auth.Value.Message, auth.Value.Code);
            if (match!.BalanceBuildVersion != null && request.UnityCompatibilityVersion != match.BalanceBuildVersion)
                return Fail("Unity build does not match the locked balance", ErrorCodes.MatchSessionConflict);

            var existingById = await LoadDecisionAsync(request.DecisionId, cancellationToken);
            if (existingById is not null)
            {
                if (existingById.MatchId != matchId || existingById.HostUserId != callerUserId
                    || !string.Equals(existingById.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                    return Fail("DecisionId has conflicting semantics", ErrorCodes.ScenarioDecisionIdentityConflict);
                if (!existingById.IsCurrent)
                {
                    var replacement = await LoadDecisionAsync(AEDV02Plan.FixedFallbackDecisionId(existingById.DecisionId), cancellationToken);
                    if (replacement is not { IsCurrent: true, UsedFixedFallback: true } || replacement.MatchId != matchId ||
                        replacement.HostUserId != callerUserId || replacement.PolicyConfigVersion != existingById.PolicyConfigVersion)
                        return Fail("Scenario decision is no longer current", ErrorCodes.ScenarioDecisionConflict);
                    existingById = replacement;
                }
                if (!await RosterStillMatchesAsync(existingById, cancellationToken))
                    return Fail("Decision snapshot roster is stale", ErrorCodes.ScenarioDecisionStaleRoster);
                if (!CommittedConfigMatches(existingById))
                    return Fail("Committed scenario config has changed", ErrorCodes.ScenarioApplyConflict);
                await tx.RollbackAsync(cancellationToken);
                return ServiceResult<ScenarioDecisionResponse>.Success(Map(existingById, true), "Scenario decision replayed");
            }

            if (match!.Status != MatchAuthorityStatus.Lobby || match.StartedAtUtc is not null)
                return Fail("Pre-match scenario decision window is closed", ErrorCodes.ScenarioDecisionWindowClosed);

            var snapshotResult = await snapshotBuilder.BuildPreMatchAsync(matchId, cancellationToken);
            if (snapshotResult.Snapshot.TeamSize == 0)
                return Fail("A bound roster is required", ErrorCodes.ScenarioRosterEmpty);

            var current = await db.ScenarioDecisions.Include(x => x.Snapshot).Include(x => x.ApplyReceipt).Include(x => x.ScenarioConfig)
                .SingleOrDefaultAsync(x => x.MatchId == matchId && x.IsCurrent, cancellationToken);
            if (useFixedAfterV02Hold && (current is null || current.DecisionId != fallbackFrom || current.HostUserId != callerUserId ||
                current.UsedFixedFallback || current.ApplyReceipt is not null ||
                current.PolicyConfigVersion?.StartsWith(AdaptiveConfigurationService.VersionPrefix, StringComparison.Ordinal) != true ||
                current.ScenarioConfig.UnityCompatibilityVersion != request.UnityCompatibilityVersion ||
                current.ExperimentCondition != request.ExperimentCondition?.Trim() ||
                current.Snapshot.RosterIdentity != snapshotResult.Snapshot.RosterIdentity))
                return Fail("Adaptive candidate can no longer fall back", ErrorCodes.ScenarioDecisionConflict);
            if (current is not null)
            {
                if (!useFixedAfterV02Hold && string.Equals(current.Snapshot.RosterIdentity, snapshotResult.Snapshot.RosterIdentity, StringComparison.Ordinal))
                    return Fail("Match already has a different current decision", ErrorCodes.ScenarioDecisionConflict);
                current.IsCurrent = false;
            }

            var adaptive = request.ResolutionMode == ScenarioResolutionMode.Adaptive;
            var settings = policyOptions.Value;
            AdaptiveConfigurationVersion? published = null;
            if (adaptive && !useFixedAfterV02Hold)
            {
                published = match.AdaptivePolicyConfigVersion is { } pinnedPolicy
                    ? await db.AdaptiveConfigurationVersions.AsNoTracking().SingleOrDefaultAsync(x =>
                        x.Version == pinnedPolicy && x.Compatibility == request.UnityCompatibilityVersion, cancellationToken)
                    : await AdaptiveConfigurationService.LatestAsync(db, request.UnityCompatibilityVersion, cancellationToken);
                if (published is null && match.AdaptivePolicyConfigVersion is not null)
                    return Fail("Locked Adaptive policy is unavailable", ErrorCodes.MatchSessionConflict);
            }
            var managed = useFixedAfterV02Hold
                ? await AdaptiveConfigurationService.ForVersionAsync(db, current!.PolicyConfigVersion, cancellationToken)
                : published is null ? null : AdaptiveConfigurationService.Read(published);
            var fixedResult = managed is null
                ? await registry.GetProductionFixedFallbackAsync(request.UnityCompatibilityVersion, cancellationToken)
                : await AdaptiveConfigurationService.LoadAsync(registry, managed.Fixed, request.UnityCompatibilityVersion, cancellationToken);
            if (!fixedResult.IsSuccess) return Fail(fixedResult.Message, fixedResult.ErrorCode!);
            if (fixedResult.Data is not { } config || (managed is not null && !AdaptiveConfigurationService.IsApprovedFixed(config)))
                return Fail("Selected Fixed fallback is no longer approved or compatible", ErrorCodes.ScenarioFixedFallbackInvalid);
            var policyConfigVersion = useFixedAfterV02Hold ? current!.PolicyConfigVersion
                : published?.Version ?? (adaptive && settings.Enabled ? settings.PolicyConfigVersion : null);
            string? ruleId = null;

            string? fallbackReason = adaptive ? "POLICY_CONFIG_INVALID" : null;

            ScenarioCandidateValidationStatus? candidateStatus =
                adaptive ? ScenarioCandidateValidationStatus.NotEvaluated : null;

            var resolutionResult = adaptive
                ? "FIXED_FALLBACK"
                : "FIXED_CONFIG_ISSUED";

            if (useFixedAfterV02Hold)
            {
                ruleId = current!.PreMatchRule;
                var v02 = await db.AEDV02Decisions.Where(x => x.MatchId == matchId && x.SnapshotId == current.SnapshotId)
                    .OrderByDescending(x => x.PlanRevision).ToArrayAsync(cancellationToken);
                if (v02.Any(x => x.ApplyStatus is "APPLIED" or "FAILED_EFFECT"))
                    return Fail("Adaptive revision was already applied", ErrorCodes.ScenarioApplyConflict);
                fallbackReason = v02.FirstOrDefault(x => x.ApprovalStatus == "HOLD")?.ReasonCode ?? "HOLD_PREMATCH_V02_UNAVAILABLE";
                foreach (var pending in v02.Where(x => x.ApplyStatus == "PENDING"))
                {
                    pending.ApplyStatus = "ABORTED";
                    pending.ReasonCode = "HOLD_PREMATCH_FIXED_FALLBACK";
                }
            }
            else if (managed is not null)
            {
                var snapshot = snapshotResult.Snapshot;
                var samples = snapshot.Validity == AdaptiveSnapshotValidity.Invalid ? [] : snapshot.Players.Select(x =>
                    new AdaptiveSample(x.SurvivalStatus == "ACTIVE" ? x.SurvivalScore : null,
                        x.NoiseStatus == "ACTIVE" ? x.NoiseScore : null, x.SurvivalSampleCount ?? 0, x.NoiseSampleCount ?? 0)).ToArray();
                var evaluation = AdaptiveConfigurationService.Evaluate(managed, samples);
                ruleId = evaluation.Rule;
                fallbackReason = evaluation.Reason;
                candidateStatus = fallbackReason == "HOLD_RULE" ? ScenarioCandidateValidationStatus.Valid
                    : ScenarioCandidateValidationStatus.NotEvaluated;
                if (evaluation.Choice is not null)
                {
                    var chosen = await AdaptiveConfigurationService.LoadAsync(registry, evaluation.Choice,
                        request.UnityCompatibilityVersion, cancellationToken);
                    if (chosen.Data is { } candidate && AdaptiveConfigurationService.IsApprovedCandidate(ruleId!, config, candidate))
                    {
                        config = candidate;
                        candidateStatus = ScenarioCandidateValidationStatus.Valid;
                        resolutionResult = "ADAPTIVE_CONFIG_ISSUED";
                    }
                    else
                    {
                        fallbackReason = "ADAPTIVE_CONFIG_UNAVAILABLE";
                        candidateStatus = ScenarioCandidateValidationStatus.Invalid;
                    }
                }
            }
            else if (adaptive && settings.Enabled)
            {
                candidateStatus = ScenarioCandidateValidationStatus.Invalid;

                if (!settings.IsContractValid() ||
                    config.ContentWhitelistVersion != settings.ContentWhitelistVersion ||
                    !settings.SupportItemBudgetCandidateValues.Contains(
                        config.SupportItemBudget) ||
                    !settings.ChaseSpeedCandidateValues.Contains(config.ChaseSpeed))
                {
                    fallbackReason = "POLICY_CONFIG_INVALID";
                }
                else
                {
                    var snapshot = snapshotResult.Snapshot;

                    var evidenceComplete =
                        snapshot.Players.Count == snapshot.TeamSize &&
                        snapshot.Players.All(x =>
                            x.SurvivalStatus == "ACTIVE" &&
                            x.NoiseStatus == "ACTIVE" &&
                            x.SurvivalSampleCount >=
                                settings.MinimumSurvivalSampleCountPerObservedPlayer &&
                            x.NoiseSampleCount >=
                                settings.MinimumNoiseSampleCountPerObservedPlayer);

                    var rule = evidenceComplete
                        ? AEDPreMatchPolicyV1.SelectRule(
                            snapshot,
                            settings.SurvivalLowThreshold,
                            settings.SurvivalHighThreshold,
                            settings.NoiseLowThreshold,
                            settings.NoiseHighThreshold)
                        : null;
                    ruleId = rule;

                    if (rule is null)
                    {
                        fallbackReason = "INPUT_INVALID";
                    }
                    else if (rule == AEDPreMatchPolicyV1.Hold)
                    {
                        fallbackReason = "HOLD_RULE";
                        candidateStatus = ScenarioCandidateValidationStatus.Valid;
                    }
                    else
                    {
                        var candidates = await registry
                            .GetProductionAdaptiveCandidatesAsync(
                                request.UnityCompatibilityVersion,
                                cancellationToken);

                        if (!candidates.IsSuccess || candidates.Data is null)
                        {
                            fallbackReason = "ADAPTIVE_CONFIG_UNAVAILABLE";
                        }
                        else
                        {
                            var relief = rule is AEDPreMatchPolicyV1.SurvivalRelief or AEDPreMatchPolicyV1.NoiseRelief;

                            var values = relief
                                ? settings.SupportItemBudgetCandidateValues
                                : settings.ChaseSpeedCandidateValues;

                            if (AEDPreMatchCandidateV1.TrySelect(
                                    rule,
                                    config,
                                    candidates.Data,
                                    values,
                                    out var selected,
                                    out var reason) && selected is not null)
                            {
                                config = selected;
                                fallbackReason = null;
                                candidateStatus =
                                    ScenarioCandidateValidationStatus.Valid;
                                resolutionResult = "ADAPTIVE_CONFIG_ISSUED";
                            }
                            else
                            {
                                fallbackReason = reason;
                            }
                        }
                    }
                }
            }

            var historyPolicyVersion = useFixedAfterV02Hold ? current!.ProfileTuningVersion
                : adaptive ? (await ProfileTuningService.LatestAsync(db, cancellationToken))?.Version : null;
            var configFingerprint = ScenarioFingerprint.Config(config);
            var decisionFingerprint = ScenarioFingerprint.Hash(
                matchId.ToString("D"), "PRE_MATCH", request.ResolutionMode.ToString().ToUpperInvariant(),
                snapshotResult.Snapshot.SnapshotContentFingerprint, config.ScenarioConfigId,
                config.ScenarioConfigVersion, configFingerprint, config.PolicyVersion,
                config.ContentWhitelistVersion, config.FallbackConfigVersion, fallbackReason,
                policyConfigVersion,
                adaptive && settings.Enabled ? settings.EvidencePolicyVersion : null,
                adaptive && settings.Enabled ? settings.ParameterRegistryVersion : null);
            if (historyPolicyVersion is not null)
                decisionFingerprint = ScenarioFingerprint.Hash(decisionFingerprint, historyPolicyVersion);
            var decision = new ScenarioDecision
            {
                DecisionId = request.DecisionId, MatchId = matchId, HostUserId = callerUserId,
                SnapshotId = snapshotResult.Snapshot.SnapshotId, ResolutionMode = request.ResolutionMode,
                ExperimentCondition = request.ExperimentCondition?.Trim(), RequestFingerprint = requestFingerprint,
                DecisionSemanticFingerprint = decisionFingerprint, ScenarioConfigId = config.ScenarioConfigId,
                ScenarioConfigVersion = config.ScenarioConfigVersion, ScenarioConfigFingerprint = configFingerprint,
                PolicyVersion = config.PolicyVersion,
                PolicyConfigVersion = policyConfigVersion,
                EvidencePolicyVersion = adaptive && settings.Enabled
                    ? settings.EvidencePolicyVersion : null,
                ParameterRegistryVersion = adaptive && settings.Enabled
                    ? settings.ParameterRegistryVersion : null,
                ContentWhitelistVersion = config.ContentWhitelistVersion,
                FallbackConfigId = config.FallbackConfigId, FallbackConfigVersion = config.FallbackConfigVersion,
                UsedFixedFallback = adaptive && config.ConfigSource == ScenarioConfigSource.Fixed,
                FallbackReasonCode = fallbackReason,
                PreMatchRule = ruleId,
                ProfileTuningVersion = historyPolicyVersion,
                CandidateValidationStatus = candidateStatus,
                ResolutionResult = resolutionResult,
                Status = ScenarioDecisionStatus.Committed, IsCurrent = true,
                CommittedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                Snapshot = snapshotResult.Snapshot
            };
            db.ScenarioDecisions.Add(decision);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return ServiceResult<ScenarioDecisionResponse>.Success(Map(decision, false, config), "Scenario decision committed");
        }
        catch (Exception exception) when (IsConcurrencyFailure(exception))
        {
            await tx.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var stored = await LoadDecisionAsync(request.DecisionId, cancellationToken);
            if (stored is { IsCurrent: true } &&
                stored.MatchId == matchId && stored.HostUserId == callerUserId
                && string.Equals(stored.RequestFingerprint, requestFingerprint, StringComparison.Ordinal)
                && await RosterStillMatchesAsync(stored, cancellationToken)
                && CommittedConfigMatches(stored))
                return ServiceResult<ScenarioDecisionResponse>.Success(Map(stored, true), "Scenario decision replayed");
            return Fail("Concurrent scenario decision conflict", ErrorCodes.ScenarioDecisionConflict);
        }
    }

    public async Task<ServiceResult<ScenarioDecisionResponse>> ConfirmAppliedAsync(
        Guid callerUserId, Guid matchId, Guid decisionId, ConfirmScenarioAppliedRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var match = await db.MatchAuthorityBindings.SingleOrDefaultAsync(x => x.MatchId == matchId, cancellationToken);
            var auth = ValidateHost(match, callerUserId, requireLobby: false);
            if (auth is not null) return Fail(auth.Value.Message, auth.Value.Code);
            if (match!.Status == MatchAuthorityStatus.Ended)
                return Fail("Scenario apply confirmation is closed", ErrorCodes.ScenarioDecisionWindowClosed);
            var decision = await LoadDecisionAsync(decisionId, cancellationToken);
            if (decision is null || decision.MatchId != matchId)
                return Fail("Scenario decision was not found", ErrorCodes.ScenarioDecisionNotFound);
            if (!CommittedConfigMatches(decision))
                return Fail("Committed scenario config has changed", ErrorCodes.ScenarioApplyConflict);
            if (!decision.IsCurrent || !await RosterStillMatchesAsync(decision, cancellationToken))
                return Fail("Scenario decision snapshot is stale", ErrorCodes.ScenarioDecisionStaleRoster);
            if (!Matches(decision, request))
                return Fail("Applied config does not match committed decision", ErrorCodes.ScenarioApplyConflict);
            if (decision.ApplyReceipt is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                return ServiceResult<ScenarioDecisionResponse>.Success(Map(decision, true), "Scenario apply confirmation replayed");
            }
            decision.ApplyReceipt = new ScenarioApplyReceipt
            {
                DecisionId = decisionId, MatchId = matchId, ReportedByUserId = callerUserId,
                AppliedScenarioConfigId = request.ScenarioConfigId,
                AppliedScenarioConfigVersion = request.ScenarioConfigVersion,
                AppliedScenarioConfigFingerprint = request.ScenarioConfigFingerprint.ToLowerInvariant(),
                AppliedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return ServiceResult<ScenarioDecisionResponse>.Success(Map(decision, false), "Scenario apply confirmed");
        }
        catch (Exception exception) when (IsConcurrencyFailure(exception))
        {
            await tx.RollbackAsync(cancellationToken); db.ChangeTracker.Clear();
            var stored = await LoadDecisionAsync(decisionId, cancellationToken);
            if (stored is { IsCurrent: true } &&
                await RosterStillMatchesAsync(stored, cancellationToken) &&
                CommittedConfigMatches(stored)
                && Matches(stored, request) && stored.ApplyReceipt is not null)
                return ServiceResult<ScenarioDecisionResponse>.Success(Map(stored, true), "Scenario apply confirmation replayed");
            return Fail("Concurrent apply confirmation conflict", ErrorCodes.ScenarioApplyConflict);
        }
    }

    private (string Message, string Code)? ValidateHost(MatchAuthorityBinding? match, Guid caller, bool requireLobby)
    {
        if (match is null) return ("Match was not found", ErrorCodes.MatchNotFound);
        if (match.HostUserId != caller) return ("Only the verified host may resolve scenarios", ErrorCodes.MatchAuthorityForbidden);
        if (match.LeaseExpiresAtUtc < timeProvider.GetUtcNow().UtcDateTime) return ("Host authority lease expired", ErrorCodes.MatchLeaseExpired);
        return null;
    }
    private async Task<ScenarioDecision?> LoadDecisionAsync(Guid id, CancellationToken ct) =>
        await db.ScenarioDecisions.Include(x => x.Snapshot).ThenInclude(x => x.Players)
            .Include(x => x.ScenarioConfig).Include(x => x.ApplyReceipt)
            .SingleOrDefaultAsync(x => x.DecisionId == id, ct);
    private async Task<bool> RosterStillMatchesAsync(ScenarioDecision decision, CancellationToken ct)
    {
        var ids = await db.MatchPlayerBindings.AsNoTracking().Where(x => x.MatchId == decision.MatchId)
            .Select(x => x.UserId).OrderBy(x => x).ToArrayAsync(ct);
        return string.Equals(decision.Snapshot.RosterIdentity,
            ScenarioFingerprint.Hash(decision.MatchId.ToString("D"), string.Join(",", ids.Select(x => x.ToString("D")))), StringComparison.Ordinal);
    }
    private static bool Matches(ScenarioDecision d, ConfirmScenarioAppliedRequest r) =>
        string.Equals(d.ScenarioConfigId, r.ScenarioConfigId, StringComparison.Ordinal)
        && string.Equals(d.ScenarioConfigVersion, r.ScenarioConfigVersion, StringComparison.Ordinal)
        && string.Equals(d.ScenarioConfigFingerprint, r.ScenarioConfigFingerprint, StringComparison.OrdinalIgnoreCase);
    private static bool CommittedConfigMatches(ScenarioDecision decision) =>
        decision.ScenarioConfig is not null &&
        string.Equals(
            decision.ScenarioConfigFingerprint,
            ScenarioFingerprint.Config(decision.ScenarioConfig),
            StringComparison.Ordinal);
    private static bool IsConcurrencyFailure(Exception exception)
    {
        var hasDbUpdateException = false;
        var hasPostgresException = false;

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateException)
                hasDbUpdateException = true;

            if (current is PostgresException postgres)
            {
                hasPostgresException = true;
                if (postgres.SqlState == PostgresErrorCodes.SerializationFailure
                    || postgres.SqlState == PostgresErrorCodes.UniqueViolation)
                    return true;
            }
        }

        // Non-PostgreSQL providers are used only by isolated service tests.
        return hasDbUpdateException && !hasPostgresException;
    }
    private static ServiceResult<ScenarioDecisionResponse> Fail(string message, string code) =>
        ServiceResult<ScenarioDecisionResponse>.Failure(message, code);
    private static ScenarioDecisionResponse Map(ScenarioDecision d, bool replay, ScenarioConfigDefinition? resolvedConfig = null)
    {
        var reasons = JsonSerializer.Deserialize<string[]>(d.Snapshot.ReasonCodesJson) ?? [];
        var c = resolvedConfig ?? d.ScenarioConfig;
        return new(d.DecisionId, d.MatchId, d.SnapshotId, d.Snapshot.SnapshotContentFingerprint,
            d.Snapshot.RosterIdentity, d.Snapshot.Validity.ToString().ToUpperInvariant(), reasons,
            d.ResolutionMode.ToString().ToUpperInvariant(), d.ResolutionResult,
            d.CandidateValidationStatus switch
            {
                ScenarioCandidateValidationStatus.NotEvaluated => "NOT_EVALUATED",
                ScenarioCandidateValidationStatus.Valid => "VALID",
                ScenarioCandidateValidationStatus.Invalid => "INVALID",
                _ => null
            }, d.UsedFixedFallback,
            d.FallbackReasonCode, "COMMITTED", d.ApplyReceipt is null ? "PENDING" : "APPLIED",
            d.CommittedAtUtc, d.ApplyReceipt?.AppliedAtUtc, replay,
            new(c.ScenarioConfigId, c.ScenarioConfigVersion, c.SchemaVersion, c.PolicyVersion,
                c.ConfigSource.ToString().ToUpperInvariant(), c.MapId, c.MonsterType, c.ObjectiveSpawnSetId,
                c.SupportItemBudget, c.DetectionFillRate, c.DetectionDecayRate, c.ChaseSpeed,
                c.SearchDuration, c.RouteModifier, c.EscapeDoorTimerSeconds, c.FallbackConfigId,
                c.FallbackConfigVersion, c.ContentWhitelistVersion, c.UnityCompatibilityVersion,
                d.ScenarioConfigFingerprint), d.PolicyConfigVersion, d.PreMatchRule);
    }
}
