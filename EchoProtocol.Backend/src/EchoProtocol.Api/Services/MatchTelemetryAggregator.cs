using System.Security.Cryptography;
using System.Text;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using EchoProtocol.Api.Services.Models;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed class MatchTelemetryAggregator : IMatchTelemetryAggregator
{
    private const string SupportedSchemaVersion = "1.1";
    private readonly AppDbContext _db;
    private readonly ITelemetryEventRepository _repository;
    private readonly IPlayerAIProfilePolicy _policy;

    public MatchTelemetryAggregator(
        AppDbContext db,
        ITelemetryEventRepository repository,
        IPlayerAIProfilePolicy policy)
    {
        _db = db;
        _repository = repository;
        _policy = policy;
    }

    public async Task<MatchTelemetryAggregation> AggregateAsync(
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var result = await _db.MatchResults
            .AsNoTracking()
            .Include(item => item.Match)
            .Include(item => item.Players)
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        var boundUsers = (await _db.MatchPlayerBindings.AsNoTracking()
            .Where(item => item.MatchId == matchId)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken)).ToHashSet();
        var isBound = boundUsers.Contains(userId);

        var documents = (await _repository.LoadAcceptedMatchEventsAsync(matchId, cancellationToken))
            .OrderBy(item => item.EventSequence)
            .ThenBy(item => item.Id)
            .ToArray();
        var sourceFingerprint = Fingerprint(documents.Select(item => item.SemanticFingerprint));

        if (result is null)
        {
            return Empty(matchId, userId, MatchProfileEligibilityStatus.Pending,
                TelemetryCompleteness.Unknown, "MATCH_NOT_FINALIZED", sourceFingerprint);
        }

        if (!isBound || result.Players.All(item => item.UserId != userId))
        {
            return Empty(matchId, userId, MatchProfileEligibilityStatus.Ineligible,
                TelemetryCompleteness.Invalid, "PLAYER_IDENTITY_INVALID", sourceFingerprint);
        }

        if (result.Match.Status != MatchAuthorityStatus.Ended || result.Match.EndedAtUtc is null)
        {
            return Empty(matchId, userId, MatchProfileEligibilityStatus.Ineligible,
                TelemetryCompleteness.Invalid, "MATCH_NOT_FINALIZED", sourceFingerprint);
        }

        var integrityReasons = ValidateIntegrity(documents, matchId, boundUsers);
        if (integrityReasons.Count > 0)
        {
            return Empty(matchId, userId, MatchProfileEligibilityStatus.Ineligible,
                TelemetryCompleteness.Invalid, integrityReasons, sourceFingerprint);
        }

        var starts = documents.Where(item => item.EventType == "MATCH_STARTED").ToArray();
        var ends = documents.Where(item => item.EventType == "MATCH_ENDED").ToArray();
        if (starts.Length != 1 || starts[0].EventSequence != 1)
        {
            return Empty(matchId, userId, MatchProfileEligibilityStatus.Ineligible,
                TelemetryCompleteness.Invalid, "MISSING_MATCH_START", sourceFingerprint);
        }

        if (ends.Length != 1)
        {
            return Empty(matchId, userId, MatchProfileEligibilityStatus.Ineligible,
                TelemetryCompleteness.Incomplete, "MISSING_MATCH_END", sourceFingerprint);
        }

        var terminal = ends[0];
        if (terminal.ReasonCode == "MATCH_ABORTED")
        {
            return Empty(matchId, userId, MatchProfileEligibilityStatus.Ineligible,
                TelemetryCompleteness.Complete, "MATCH_ABORTED", sourceFingerprint);
        }

        if (!TerminalAgreesWithResult(terminal, result))
        {
            return Empty(matchId, userId, MatchProfileEligibilityStatus.Ineligible,
                TelemetryCompleteness.Invalid, "PROVENANCE_INVALID", sourceFingerprint);
        }

        var complete = terminal.EventSequence == documents.Length &&
            documents.Select(item => item.EventSequence).SequenceEqual(
                Enumerable.Range(1, documents.Length).Select(item => (long)item));
        var completeness = complete ? TelemetryCompleteness.Complete : TelemetryCompleteness.Incomplete;
        var reasons = complete ? Array.Empty<string>() : ["STREAM_INCOMPLETE"];

        var terminalFacts = documents
            .Where(item => item.UserId == userId &&
                           item.EventType is "PLAYER_ESCAPED" or "PLAYER_ELIMINATED")
            .ToArray();
        AggregatedMetric survival;
        if (terminalFacts.Length == 1)
        {
            survival = new AggregatedMetric(
                PlayerAIDimension.Survival,
                terminalFacts[0].EventType == "PLAYER_ESCAPED" ? 100m : 0m,
                MetricAvailability.Available,
                "FINAL_TERMINAL_FACT",
                Fingerprint([terminalFacts[0].SemanticFingerprint]));
        }
        else if (terminalFacts.Length == 0)
        {
            survival = new AggregatedMetric(PlayerAIDimension.Survival, null,
                MetricAvailability.Unavailable, "TERMINAL_FACT_MISSING", Fingerprint([]));
        }
        else
        {
            survival = new AggregatedMetric(PlayerAIDimension.Survival, null,
                MetricAvailability.Invalid, "CONTRADICTORY_TERMINAL_FACT", Fingerprint(terminalFacts.Select(item => item.SemanticFingerprint)));
        }

        var noiseEvents = documents
            .Where(item => item.UserId == userId && item.EventType == "NOISE_EMITTED")
            .ToArray();
        AggregatedMetric noise;
        if (!complete)
        {
            noise = new AggregatedMetric(PlayerAIDimension.Noise, null,
                MetricAvailability.Unavailable, "SOURCE_COVERAGE_INCOMPLETE", Fingerprint(noiseEvents.Select(item => item.SemanticFingerprint)));
        }
        else if (!_policy.TryValidate(PlayerAIDimension.Noise, out var configReason))
        {
            noise = new AggregatedMetric(PlayerAIDimension.Noise, null,
                MetricAvailability.Invalid, configReason, Fingerprint(noiseEvents.Select(item => item.SemanticFingerprint)));
        }
        else
        {
            var penaltyCount = noiseEvents.Count(item =>
                TryReadString(item.ValueJson, "data", "noiseType", out var noiseType) &&
                _policy.IsNoisePenalty(noiseType));
            noise = new AggregatedMetric(PlayerAIDimension.Noise, penaltyCount,
                MetricAvailability.Available, "FILTERED_PENALTY_COUNT", Fingerprint(noiseEvents.Select(item => item.SemanticFingerprint)));
        }

        var metrics = new Dictionary<PlayerAIDimension, AggregatedMetric>
        {
            [PlayerAIDimension.Survival] = survival,
            [PlayerAIDimension.Noise] = noise
        };
        var researchEnabled = TryReadBoolean(starts[0].ValueJson, "context", "researchCaptureEnabled", out var enabled) && enabled;

        return new MatchTelemetryAggregation(
            matchId, userId, result.EndedAtUtc, MatchProfileEligibilityStatus.Eligible,
            completeness, reasons, SupportedSchemaVersion, sourceFingerprint,
            researchEnabled, null, metrics);
    }

    private static List<string> ValidateIntegrity(
        IReadOnlyList<TelemetryEventDocument> documents,
        Guid matchId,
        IReadOnlySet<Guid> boundUsers)
    {
        var reasons = new List<string>();
        if (documents.Any(item => item.MatchId != matchId)) reasons.Add("IDENTITY_CONFLICT");
        if (documents.Any(item => item.UserId.HasValue && !boundUsers.Contains(item.UserId.Value))) reasons.Add("IDENTITY_CONFLICT");
        if (documents.Any(item => item.SchemaVersion != SupportedSchemaVersion)) reasons.Add("UNSUPPORTED_SCHEMA_VERSION");
        if (documents.GroupBy(item => item.Id).Any(group => group.Count() > 1)) reasons.Add("IDENTITY_CONFLICT");
        if (documents.GroupBy(item => item.EventSequence).Any(group => group.Count() > 1) ||
            documents.Any(item => item.EventSequence <= 0)) reasons.Add("SEQUENCE_CONFLICT");
        if (documents.Any(item => item.SemanticFingerprint.Length != 64 ||
                                  !item.SemanticFingerprint.All(Uri.IsHexDigit))) reasons.Add("PROVENANCE_INVALID");
        return reasons.Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool TerminalAgreesWithResult(TelemetryEventDocument terminal, EchoProtocol.Api.Entities.MatchResult result)
    {
        var expectedSurvivors = result.Players.Count(item => item.Survived);
        return TryReadString(terminal.ValueJson, "data", "outcome", out var value) &&
               TryReadInt32(terminal.ValueJson, "data", "survivorCount", out var survivorCount) &&
               value == (result.Outcome == MatchOutcome.WIN ? "SUCCESS" : "FAILURE") &&
               terminal.ReasonCode == (result.Outcome == MatchOutcome.WIN ? "TEAM_ESCAPED" : "TEAM_ELIMINATED") &&
               survivorCount == expectedSurvivors;
    }

    private static bool TryReadString(BsonDocument root, string parent, string name, out string value)
    {
        value = string.Empty;
        return root.TryGetValue(parent, out var parentValue) && parentValue.IsBsonDocument &&
               parentValue.AsBsonDocument.TryGetValue(name, out var field) && field.IsString &&
               !string.IsNullOrWhiteSpace(value = field.AsString);
    }

    private static bool TryReadBoolean(BsonDocument root, string parent, string name, out bool value)
    {
        value = false;
        if (!root.TryGetValue(parent, out var parentValue) || !parentValue.IsBsonDocument ||
            !parentValue.AsBsonDocument.TryGetValue(name, out var field) || !field.IsBoolean)
        {
            return false;
        }

        value = field.AsBoolean;
        return true;
    }

    private static bool TryReadInt32(BsonDocument root, string parent, string name, out int value)
    {
        value = 0;
        return root.TryGetValue(parent, out var parentValue) && parentValue.IsBsonDocument &&
               parentValue.AsBsonDocument.TryGetValue(name, out var field) && field.IsInt32 &&
               (value = field.AsInt32) >= 0;
    }

    private static string Fingerprint(IEnumerable<string> parts) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))));

    private static MatchTelemetryAggregation Empty(
        Guid matchId, Guid userId, MatchProfileEligibilityStatus status,
        TelemetryCompleteness completeness, string reason, string fingerprint) =>
        Empty(matchId, userId, status, completeness, [reason], fingerprint);

    private static MatchTelemetryAggregation Empty(
        Guid matchId, Guid userId, MatchProfileEligibilityStatus status,
        TelemetryCompleteness completeness, IReadOnlyList<string> reasons, string fingerprint) =>
        new(matchId, userId, null, status, completeness, reasons, SupportedSchemaVersion,
            fingerprint, false, null, new Dictionary<PlayerAIDimension, AggregatedMetric>());
}
