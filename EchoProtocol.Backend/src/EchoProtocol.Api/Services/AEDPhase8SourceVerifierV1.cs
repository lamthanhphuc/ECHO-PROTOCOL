using EchoProtocol.Api.Data;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed class AEDPhase8SourceVerifierV1(AppDbContext db, ITelemetryEventRepository telemetry, IAEDv2PhaseEvidenceVerifier canonicalVerifier)
{
    public async Task<AEDPhase8SourceVerificationV1> VerifyAsync(Guid matchId, AEDPhase8SourceUploadRequestV1 request, CancellationToken ct = default)
    {
        if (matchId == Guid.Empty || request == null || request.CompletedPhaseOrdinal < 1 || request.CompletedPhaseOrdinal == int.MaxValue || request.SourceEvents == null || request.Metrics == null || request.Supply == null || request.Minions == null || string.IsNullOrWhiteSpace(request.SourceVersion) || string.IsNullOrWhiteSpace(request.ComparisonContextKey) || request.SourceVersion.Length > 80 || request.ComparisonContextKey.Length > 128 || !ValidHash(request.EvidenceFingerprint) || !ValidHash(request.RosterIdentity))
            return new(false, false, false, false, false, "P8_SOURCE_REQUEST_INVALID");
        if (request.SourceVersion == "AED_PHASE8_SOURCE_REFERENCES_V1" &&
            (request.Metrics.Length != 0 || request.Supply.Length != 0 || request.Minions.Length != 0))
            return new(false, false, false, false, false, "P8_SOURCE_REFERENCE_ONLY");
        if (!await canonicalVerifier.VerifyAsync(matchId, request.CompletedPhaseOrdinal, request.EvidenceFingerprint, ct))
            return new(false, false, false, false, false, "P8_CANONICAL_UNVERIFIED");
        var binding = await db.MatchAuthorityBindings.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        var users = await db.MatchPlayerBindings.AsNoTracking().Where(x => x.MatchId == matchId).Select(x => x.UserId).ToArrayAsync(ct);
        if (binding == null || users.Length == 0 || !string.Equals(RosterIdentity(matchId, users), request.RosterIdentity, StringComparison.Ordinal))
            return new(true, false, false, false, false, "P8_ROSTER_IDENTITY_MISMATCH");
        var accepted = await telemetry.LoadAcceptedMatchEventsAsync(matchId, ct);
        var byId = accepted.Where(x => x.MatchId == matchId).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.ToArray());
        var phaseStarts = accepted.Where(x => x.MatchId == matchId && x.EventType == "PHASE_STARTED").OrderBy(x => x.EventSequence).ToArray();
        if (phaseStarts.Length < request.CompletedPhaseOrdinal) return new(true, false, false, false, false, "P8_SOURCE_PHASE_START_MISSING");
        var phaseStart = phaseStarts[request.CompletedPhaseOrdinal - 1];
        var phaseName = ReadPhase(phaseStart.ValueJson);
        var nextPhaseStartSequence = request.CompletedPhaseOrdinal < phaseStarts.Length
            ? phaseStarts[request.CompletedPhaseOrdinal].EventSequence
            : long.MaxValue;
        var phaseEnds = accepted.Where(x => x.MatchId == matchId && x.EventType == "PHASE_COMPLETED" &&
            x.EventSequence > phaseStart.EventSequence && x.EventSequence < nextPhaseStartSequence).ToArray();
        if (string.IsNullOrWhiteSpace(phaseName) || phaseEnds.Length != 1 ||
            ReadPhase(phaseEnds[0].ValueJson) != phaseName || phaseEnds[0].Ts < phaseStart.Ts)
            return new(true, false, false, false, false, "P8_SOURCE_PHASE_WINDOW_INVALID");
        if (request.SourceEvents.Any(x => x == null || x.EventId == Guid.Empty || x.EventSequence <= 0 || string.IsNullOrWhiteSpace(x.EventType) || string.IsNullOrWhiteSpace(x.OccurrenceKey)) ||
            request.SourceEvents.Select(x => x.EventId).Distinct().Count() != request.SourceEvents.Length ||
            request.SourceEvents.GroupBy(x => (x.EventType, x.OccurrenceKey)).Any(group => group.Count() > 1))
            return new(true, false, false, false, false, "P8_SOURCE_REFERENCE_INVALID");

        var phaseEventIds = accepted
            .Where(x =>
                x.MatchId == matchId &&
                x.EventSequence > phaseStart.EventSequence &&
                x.EventSequence < phaseEnds[0].EventSequence)
            .Select(x => x.Id)
            .ToHashSet();

        if (phaseEventIds.Count != request.SourceEvents.Length ||
            !phaseEventIds.SetEquals(
                request.SourceEvents.Select(x => x.EventId)))
        {
            return new(
                true, false, false, false, false,
                "P8_SOURCE_COVERAGE_MISMATCH");
        }

        foreach (var source in request.SourceEvents)
        {
            if (!byId.TryGetValue(source.EventId, out var matches) || matches.Length != 1) return new(true, false, false, false, false, "P8_SOURCE_EVENT_NOT_ACCEPTED");
            var item = matches[0];
            if (item.EventSequence != source.EventSequence || item.EventType != source.EventType || item.EventSequence <= phaseStart.EventSequence || item.EventSequence >= phaseEnds[0].EventSequence || (item.UserId.HasValue && !users.Contains(item.UserId.Value)) || !HasOccurrenceKey(item.ValueJson, source.OccurrenceKey) || ReadPhase(item.ValueJson) != phaseName)
                return new(true, false, false, false, false, "P8_SOURCE_EVENT_MISMATCH");
        }
        if (!AEDPhase8LifecycleReconcilerV1.TryVerify(accepted, matchId, phaseEnds[0].EventSequence, users.ToHashSet(), out var lifecycleReason))
            return new(true, false, false, false, false, lifecycleReason);
        var references = request.SourceEvents.Select(x => x.OccurrenceKey).ToHashSet(StringComparer.Ordinal);
        if (request.Metrics.Any(x => x == null || x.UserId == Guid.Empty || !users.Contains(x.UserId) || x.Eligible < 0 || x.ResolvedSuccess < 0 || x.ResolvedFailure < 0 || x.Censored < 0 || (long)x.ResolvedSuccess + x.ResolvedFailure + x.Censored > x.Eligible || x.MetricVersion != "AED_METRIC_OPPORTUNITY_V1" || (x.NormalizedValue.HasValue && (x.NormalizedValue < 0m || x.NormalizedValue > 1m)) || (x.ResolvedSuccess + x.ResolvedFailure > 0 && !x.NormalizedValue.HasValue) || string.IsNullOrWhiteSpace(x.MetricId) || string.IsNullOrWhiteSpace(x.ComparisonContextKey) || string.IsNullOrWhiteSpace(x.ConfidenceStatus) || x.SourceOccurrenceKeys == null || x.SourceOccurrenceKeys.Any(key => string.IsNullOrWhiteSpace(key) || !references.Contains(key))))
            return new(true, false, false, false, false, "P8_SOURCE_METRIC_INVALID");
        if (request.Supply.Any(x => x == null || string.IsNullOrWhiteSpace(x.InstanceId) || x.ToolId is not (1 or 2 or 3 or 4 or 6) || string.IsNullOrWhiteSpace(x.Origin) || string.IsNullOrWhiteSpace(x.Location) || (x.OwnerUserId.HasValue && !users.Contains(x.OwnerUserId.Value)) || (x.RemainingCharges.HasValue && x.RemainingCharges.Value < 0) || x.Zone is < 0 or > 3 || x.SourceTick < 0 || string.IsNullOrWhiteSpace(x.SourceOccurrenceKey) || !references.Contains(x.SourceOccurrenceKey)))
            return new(true, false, false, false, false, "P8_SOURCE_SUPPLY_INVALID");
        if (request.Supply.Select(x => x.SourceOccurrenceKey).Distinct(StringComparer.Ordinal).Count() != request.Supply.Length)
            return new(true, false, false, false, false, "P8_SOURCE_SUPPLY_INVALID");
        if (request.Minions.Any(x => x == null || string.IsNullOrWhiteSpace(x.NetworkInstanceId) || x.State is not ("SPAWNED" or "DESPAWNED") || x.Zone is < 0 or > 3 || x.SourceTick < 0 || string.IsNullOrWhiteSpace(x.SourceOccurrenceKey) || !references.Contains(x.SourceOccurrenceKey)))
            return new(true, false, false, false, false, "P8_SOURCE_MINION_INVALID");
        if (request.Minions.Select(x => x.SourceOccurrenceKey).Distinct(StringComparer.Ordinal).Count() != request.Minions.Length)
            return new(true, false, false, false, false, "P8_SOURCE_MINION_INVALID");
        if (request.SourceVersion != "AED_PHASE8_SOURCE_REFERENCES_V1" &&
            !AEDPhase8LifecycleReconcilerV1.TryMatchPhaseSidecars(accepted, matchId, phaseStart.EventSequence, phaseEnds[0].EventSequence, request.Supply, request.Minions, out var sidecarReason))
            return new(true, false, false, false, false, sidecarReason);
        // Historical skill, pressure sidecars, complete supply origins, and minion state are not independently reconstructible yet.
        return new(true, false, false, false, false, "P8_SOURCE_AUTHORITATIVE_COVERAGE_INCOMPLETE");
    }

    private static bool ValidHash(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool HasOccurrenceKey(BsonDocument document, string key) =>
        document.TryGetValue("context", out var context) &&
        context.IsBsonDocument &&
        context.AsBsonDocument.TryGetValue("occurrenceKey", out var occurrence) &&
        occurrence.IsString &&
        string.Equals(occurrence.AsString, key, StringComparison.Ordinal);

    private static string ReadPhase(BsonDocument document) => document.TryGetValue("context", out var context) && context.IsBsonDocument && context.AsBsonDocument.TryGetValue("phase", out var phase) && phase.IsString ? phase.AsString : string.Empty;

    private static string RosterIdentity(Guid matchId, IEnumerable<Guid> users) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(matchId.ToString("D") + "|" + string.Join(',', users.Order().Select(x => x.ToString("D")))))).ToLowerInvariant();
}
