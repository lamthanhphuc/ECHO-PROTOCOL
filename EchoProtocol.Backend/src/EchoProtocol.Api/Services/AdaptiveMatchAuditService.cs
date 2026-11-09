using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace EchoProtocol.Api.Services;

public sealed class AdaptiveMatchAuditService(AppDbContext db, ITelemetryEventRepository telemetry, ILogger<AdaptiveMatchAuditService> logger)
{
    public async Task<ServiceResult<object>> GetAsync(Guid matchId, CancellationToken ct)
    {
        var match = await db.MatchAuthorityBindings.AsNoTracking().Where(x => x.MatchId == matchId)
            .Select(x => new { x.Status, x.BalanceSource, x.BalanceVersion, x.BalanceHash, x.BalanceBuildVersion, x.MatchDataPolicy }).SingleOrDefaultAsync(ct);
        if (match is null)
            return ServiceResult<object>.Failure("Match was not found", ErrorCodes.NotFound);
        var preMatch = await db.ScenarioDecisions.AsNoTracking().Where(x => x.MatchId == matchId)
            .OrderByDescending(x => x.IsCurrent).ThenByDescending(x => x.CommittedAtUtc)
            .Select(x => new
            {
                x.DecisionId, x.ScenarioConfigId, x.ScenarioConfigVersion, x.PolicyConfigVersion, x.PolicyVersion, x.ProfileTuningVersion,
                x.ResolutionMode, x.ResolutionResult, x.UsedFixedFallback, x.FallbackReasonCode, x.PreMatchRule,
                x.FallbackConfigId, x.FallbackConfigVersion, x.CommittedAtUtc, x.IsCurrent,
                x.Snapshot.Validity, x.Snapshot.TeamSize, x.ScenarioConfig.SupportItemBudget, x.ScenarioConfig.ChaseSpeed,
                AppliedAtUtc = x.ApplyReceipt == null ? (DateTime?)null : x.ApplyReceipt.AppliedAtUtc
            }).ToArrayAsync(ct);
        var ledger = await db.AEDV02Decisions.AsNoTracking().Where(x => x.MatchId == matchId)
            .OrderBy(x => x.PlanRevision).ToArrayAsync(ct);
        var clients = await (from item in db.Set<AEDV02ClientObservation>().AsNoTracking()
                             join decision in db.AEDV02Decisions on item.DecisionId equals decision.DecisionId
                             where decision.MatchId == matchId
                             orderby item.ObservedAtUtc
                             select item).ToListAsync(ct);
        var clientsByDecision = clients.ToLookup(x => x.DecisionId);
        IReadOnlyList<TelemetryEventDocument>? accepted = null;
        var pending = ledger.Any(x => x.ApplyStatus == "PENDING");
        if (pending)
        {
            using var evidenceTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            evidenceTimeout.CancelAfter(TimeSpan.FromSeconds(3));
            try { accepted = await telemetry.LoadAcceptedMatchEventsAsync(matchId, evidenceTimeout.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Adaptive host evidence read timed out for {MatchId}", matchId);
            }
            catch (Exception ex) when (ex is MongoException or TimeoutException)
            {
                logger.LogWarning(ex, "Adaptive host evidence unavailable for {MatchId}", matchId);
            }
        }
        var plans = ledger.ToDictionary(x => x.PlanRevision, x => JsonSerializer.Deserialize<double[]>(x.PlanValuesJson)!);
        var normal = AEDV02Plan.Normal().ToArray();
        var resolvedByRevision = ledger.ToDictionary(x => x.PlanRevision, AEDV02DecisionService.ReadResolvedValues);
        var snapshot = resolvedByRevision.Values.Any(x => x?.Length == 13) &&
            match.BalanceSource == "REMOTE_FIXED" && match.BalanceVersion != null
            ? await db.RemoteFixedBalanceVersions.AsNoTracking().Where(x => x.Version == match.BalanceVersion)
                .Select(x => x.SnapshotJson).SingleOrDefaultAsync(ct) : null;
        var locked = RemoteFixedBalanceContract.AdaptiveBasis(
            snapshot != null && RemoteFixedBalanceContract.ComputeHash(snapshot) == match.BalanceHash ? snapshot : null).Values;
        var keys = Enum.GetNames<AEDV02Key>();
        var decisions = ledger.Select(x =>
        {
            var before = plans.GetValueOrDefault(x.ExpectedPlanRevision, normal).ToArray();
            var after = plans[x.PlanRevision].ToArray();
            var resolved = resolvedByRevision[x.PlanRevision];
            if (resolved is { Length: 13 })
            {
                before = locked[..12];
                resolvedByRevision.GetValueOrDefault(x.ExpectedPlanRevision)?.Take(12).ToArray().CopyTo(before, 0);
                after = resolved[..12];
            }
            else if (resolved != null)
            {
                int chase = (int)AEDV02Key.ChaseSpeed;
                before[chase] = x.PreviousChaseSpeed ?? resolved[chase];
                after[chase] = resolved[chase];
                for (int i = 0; i < after.Length; i++)
                    if (i != chase) after[i] = before[i];
            }
            var hostApplied = x.ApplyStatus is "APPLIED" or "FAILED_EFFECT" || x.ApplyStatus == "PENDING" &&
                accepted is not null && AEDV02DecisionService.HasAcceptedCommit(accepted, x, null);
            return new
            {
                x.DecisionId, x.PhaseOrdinal, x.DecisionPoint, x.TargetPhaseName, x.ChangedKey, x.PolicyVersion, x.CatalogVersion,
                x.ApprovalStatus, x.ApplyStatus, x.ReasonCode, x.CreatedAtUtc, x.ExpectedPlanRevision, x.PlanRevision,
                HostAppliedRevision = hostApplied ? x.PlanRevision : (int?)null,
                Changes = keys.Select((key, index) => new { Key = key, Before = before[index], After = after[index] })
                    .Where(change => change.Before != change.After).ToArray(),
                ClientObservations = clientsByDecision[x.DecisionId].Select(client => new { client.UserId, client.ObservedAtUtc }).ToArray(),
                ApplyReceipt = x.ReceiptId == null ? null : new { x.ReceiptId, x.EffectEventId, x.AppliedTick, x.AppliedAtUtc }
            };
        }).ToArray();
        var profile = await db.MatchProfileProcessingJobs.AsNoTracking().Where(x => x.MatchId == matchId)
            .Select(x => new { x.Status, x.Attempts, x.LastError, x.NextAttemptAtUtc, x.ProfileTuningVersion }).SingleOrDefaultAsync(ct);
        var mayReprocess = match.Status == MatchAuthorityStatus.Ended && match.MatchDataPolicy != RemoteFixedBalanceContract.TestOnly &&
            profile?.Status is "COMPLETED" or "FAILED";
        var teamProfile = await db.TeamProfiles.AsNoTracking().Where(x => x.MatchId == matchId).Select(x => new
        {
            x.ProcessingRevision, Status = x.ProcessingStatus.ToString(), x.ProcessingReason, x.TelemetryCompleteness,
            x.ProfileFormulaVersion, x.TeamPerformanceFormulaVersion, x.UpdatedAtUtc,
            x.ObjectiveTimeSeconds, ObjectiveTimeStatus = x.ObjectiveTimeStatus.ToString(),
            x.SurvivalScore, SurvivalStatus = x.SurvivalStatus.ToString(),
            x.TeamworkScore, TeamworkStatus = x.TeamworkStatus.ToString(),
            x.ResourceEfficiencyScore, ResourceStatus = x.ResourceEfficiencyScoreStatus.ToString(),
            x.TeamPerformanceScore, PerformanceStatus = x.TeamPerformanceStatus.ToString()
        }).SingleOrDefaultAsync(ct);
        return ServiceResult<object>.Success(new
        {
            Baseline = match.BalanceSource is null ? null : new
            {
                Source = match.BalanceSource, Version = match.BalanceVersion,
                Hash = match.BalanceHash, match.MatchDataPolicy, BuildVersion = match.BalanceBuildVersion
            },
            preMatch, decisions, profile, teamProfile, mayReprocess, HostEvidenceUnavailable = pending && accepted is null
        });
    }
}
