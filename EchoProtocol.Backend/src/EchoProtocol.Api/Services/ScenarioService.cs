using System.Data;
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

public sealed class ScenarioService(
    AppDbContext db,
    IScenarioConfigRegistry registry,
    IAdaptiveInputSnapshotBuilder snapshotBuilder,
    TimeProvider timeProvider) : IScenarioService
{
    public async Task<ServiceResult<ScenarioDecisionResponse>> ResolvePreMatchAsync(
        Guid callerUserId, Guid matchId, ResolveScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DecisionId == Guid.Empty)
            return Fail("DecisionId is required", ErrorCodes.ValidationError);
        var requestFingerprint = ScenarioFingerprint.Hash(matchId.ToString("D"), request.ResolutionMode.ToString(),
            request.UnityCompatibilityVersion, request.ExperimentCondition?.Trim());
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var match = await db.MatchAuthorityBindings.SingleOrDefaultAsync(x => x.MatchId == matchId, cancellationToken);
            var auth = ValidateHost(match, callerUserId, requireLobby: false);
            if (auth is not null) return Fail(auth.Value.Message, auth.Value.Code);

            var existingById = await LoadDecisionAsync(request.DecisionId, cancellationToken);
            if (existingById is not null)
            {
                if (existingById.MatchId != matchId || existingById.HostUserId != callerUserId
                    || !string.Equals(existingById.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                    return Fail("DecisionId has conflicting semantics", ErrorCodes.ScenarioDecisionIdentityConflict);
                if (!await RosterStillMatchesAsync(existingById, cancellationToken))
                    return Fail("Decision snapshot roster is stale", ErrorCodes.ScenarioDecisionStaleRoster);
                await tx.RollbackAsync(cancellationToken);
                return ServiceResult<ScenarioDecisionResponse>.Success(Map(existingById, true), "Scenario decision replayed");
            }

            if (match!.Status != MatchAuthorityStatus.Lobby || match.StartedAtUtc is not null)
                return Fail("Pre-match scenario decision window is closed", ErrorCodes.ScenarioDecisionWindowClosed);

            var snapshotResult = await snapshotBuilder.BuildPreMatchAsync(matchId, cancellationToken);
            if (snapshotResult.Snapshot.TeamSize == 0)
                return Fail("A bound roster is required", ErrorCodes.ScenarioRosterEmpty);

            var current = await db.ScenarioDecisions.Include(x => x.Snapshot)
                .SingleOrDefaultAsync(x => x.MatchId == matchId && x.IsCurrent, cancellationToken);
            if (current is not null)
            {
                if (string.Equals(current.Snapshot.RosterIdentity, snapshotResult.Snapshot.RosterIdentity, StringComparison.Ordinal))
                    return Fail("Match already has a different current decision", ErrorCodes.ScenarioDecisionConflict);
                current.IsCurrent = false;
            }

            var fixedResult = await registry.GetProductionFixedFallbackAsync(
                request.UnityCompatibilityVersion, cancellationToken);
            if (!fixedResult.IsSuccess)
                return Fail(fixedResult.Message, fixedResult.ErrorCode!);
            var config = fixedResult.Data!;
            var adaptive = request.ResolutionMode == ScenarioResolutionMode.Adaptive;
            var fallbackReason = adaptive ? "POLICY_CONFIG_INVALID" : null;
            var configFingerprint = ScenarioFingerprint.Config(config);
            var decisionFingerprint = ScenarioFingerprint.Hash(
                matchId.ToString("D"), "PRE_MATCH", request.ResolutionMode.ToString().ToUpperInvariant(),
                snapshotResult.Snapshot.SnapshotContentFingerprint, config.ScenarioConfigId,
                config.ScenarioConfigVersion, configFingerprint, config.PolicyVersion,
                config.ContentWhitelistVersion, config.FallbackConfigVersion, fallbackReason);
            var decision = new ScenarioDecision
            {
                DecisionId = request.DecisionId, MatchId = matchId, HostUserId = callerUserId,
                SnapshotId = snapshotResult.Snapshot.SnapshotId, ResolutionMode = request.ResolutionMode,
                ExperimentCondition = request.ExperimentCondition?.Trim(), RequestFingerprint = requestFingerprint,
                DecisionSemanticFingerprint = decisionFingerprint, ScenarioConfigId = config.ScenarioConfigId,
                ScenarioConfigVersion = config.ScenarioConfigVersion, ScenarioConfigFingerprint = configFingerprint,
                PolicyVersion = config.PolicyVersion, ContentWhitelistVersion = config.ContentWhitelistVersion,
                FallbackConfigId = config.FallbackConfigId, FallbackConfigVersion = config.FallbackConfigVersion,
                UsedFixedFallback = adaptive, FallbackReasonCode = fallbackReason,
                CandidateValidationStatus = adaptive ? ScenarioCandidateValidationStatus.NotEvaluated : null,
                ResolutionResult = adaptive ? "FIXED_FALLBACK" : "FIXED_CONFIG_ISSUED",
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
            if (stored is not null && stored.MatchId == matchId && stored.HostUserId == callerUserId
                && string.Equals(stored.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
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
            if (stored is not null && Matches(stored, request) && stored.ApplyReceipt is not null)
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
                d.ScenarioConfigFingerprint));
    }
}
