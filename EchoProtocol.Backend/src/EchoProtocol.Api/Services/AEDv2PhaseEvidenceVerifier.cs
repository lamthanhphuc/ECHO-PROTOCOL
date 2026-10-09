using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class AEDv2PhaseEvidenceVerifier(
    AppDbContext db, ITelemetryEventRepository telemetry) : IAEDv2PhaseEvidenceVerifier
{
    public async Task<bool> VerifyAsync(Guid matchId, int phaseOrdinal,
        string evidenceFingerprint, CancellationToken cancellationToken = default)
    {
        if (matchId == Guid.Empty || phaseOrdinal < 1 || evidenceFingerprint?.Length != 64
            || !evidenceFingerprint.All(Uri.IsHexDigit)) return false;

        var match = await db.MatchAuthorityBindings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MatchId == matchId, cancellationToken);
        if (match?.Status != MatchAuthorityStatus.InMatch) return false;
        var users = (await db.MatchPlayerBindings.AsNoTracking()
            .Where(item => item.MatchId == matchId).Select(item => item.UserId)
            .ToListAsync(cancellationToken)).ToHashSet();
        if (users.Count == 0) return false;

        var all = (await telemetry.LoadAcceptedMatchEventsAsync(matchId, cancellationToken))
            .Where(item => item.MatchId == matchId)
            .OrderBy(item => item.EventSequence).ThenBy(item => item.Id).ToArray();
        if (all.GroupBy(item => item.Id).Any(group => group.Select(item => item.SemanticFingerprint)
                .Distinct(StringComparer.Ordinal).Count() != 1)) return false;
        all = all.DistinctBy(item => item.Id).ToArray();
        if (all.Any(item => item.EventSequence <= 0)
            || all.GroupBy(item => item.EventSequence).Any(group => group.Count() != 1)
            || all.Any(item => item.UserId.HasValue && !users.Contains(item.UserId.Value))) return false;

        var starts = all.Where(item => item.EventType == "PHASE_STARTED").ToArray();
        if (starts.Length != phaseOrdinal) return false;
        var start = starts[phaseOrdinal - 1];
        var phase = ReadPhase(start);
        if (string.IsNullOrWhiteSpace(phase)) return false;
        var ends = all.Where(item => item.EventType == "PHASE_COMPLETED" && ReadPhase(item) == phase).ToArray();
        if (ends.Length != 1 || start.EventSequence <= 0 || ends[0].EventSequence <= start.EventSequence
            || ends[0].Ts < start.Ts
            || starts.Any(item => item.EventSequence >= ends[0].EventSequence && item.Id != start.Id)) return false;
        var end = ends[0];
        var phaseWindow = all.Where(item => item.EventSequence >= start.EventSequence
            && item.EventSequence <= end.EventSequence).ToArray();
        if (!phaseWindow.Select(item => item.EventSequence).SequenceEqual(
                Enumerable.Range(0, phaseWindow.Length).Select(offset => start.EventSequence + offset)))
            return false;
        var events = all.Where(item => item.EventSequence > start.EventSequence
            && item.EventSequence < end.EventSequence).ToArray();
        var phaseEvents = events.Where(item => IsPhaseEvent(item.EventType)).ToArray();
        if (phaseEvents.Any(item => ReadPhase(item) != phase)) return false;
        var playerEvents = phaseEvents.Where(item => item.EventType is "PLAYER_DOWNED" or "PLAYER_REVIVED"
            or "PLAYER_ELIMINATED" or "TEAM_TOOL_USED" or "NOISE_EMITTED").ToArray();
        if (playerEvents.Any(item => !item.UserId.HasValue || !users.Contains(item.UserId.Value))) return false;

        var down = playerEvents.Count(item => item.EventType == "PLAYER_DOWNED");
        var revive = playerEvents.Count(item => item.EventType == "PLAYER_REVIVED");
        var eliminated = playerEvents.Count(item => item.EventType == "PLAYER_ELIMINATED");
        var noise = playerEvents.Count(item => item.EventType == "NOISE_EMITTED");
        var tools = playerEvents.Count(item => item.EventType == "TEAM_TOOL_USED");
        var objectives = phaseEvents.Count(item => item.EventType == "PUZZLE_COMPLETED");
        if (eliminated > users.Count) return false;
        var actual = ComputeCanonicalFingerprint(matchId, phase, phaseOrdinal,
            RosterIdentity(matchId, users), start.Ts, end.Ts, users.Count - eliminated,
            down, revive, eliminated, noise, objectives, tools, true, Array.Empty<string>());
        return string.Equals(actual, evidenceFingerprint, StringComparison.Ordinal);
    }

    public static string ComputeCanonicalFingerprint(Guid matchId, string phase, int phaseOrdinal,
        string rosterIdentity, DateTime startedAtUtc, DateTime endedAtUtc, int alivePlayers,
        int downCount, int reviveCount, int eliminatedCount, int noiseCount,
        int objectiveProgress, int teamToolUseCount, bool telemetryComplete,
        IReadOnlyCollection<string> reasonCodes)
    {
        var content = string.Join("|", "AED_V2_PHASE_EVIDENCE_V2", matchId.ToString("D"), phase,
            phaseOrdinal.ToString(CultureInfo.InvariantCulture), rosterIdentity,
            startedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            endedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            alivePlayers.ToString(CultureInfo.InvariantCulture), downCount.ToString(CultureInfo.InvariantCulture),
            reviveCount.ToString(CultureInfo.InvariantCulture), eliminatedCount.ToString(CultureInfo.InvariantCulture),
            noiseCount.ToString(CultureInfo.InvariantCulture), objectiveProgress.ToString(CultureInfo.InvariantCulture),
            teamToolUseCount.ToString(CultureInfo.InvariantCulture), telemetryComplete ? "1" : "0",
            string.Join(",", reasonCodes));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private static bool IsPhaseEvent(string type) => type is "PLAYER_DOWNED" or "PLAYER_REVIVED"
        or "PLAYER_ELIMINATED" or "TEAM_TOOL_USED" or "NOISE_EMITTED" or "PUZZLE_COMPLETED"
        or "SECURITY_HOLD_INTERRUPTED";

    private static string ReadPhase(TelemetryEventDocument item) =>
        item.ValueJson.TryGetValue("context", out var context) && context.IsBsonDocument
        && context.AsBsonDocument.TryGetValue("phase", out var value) && value.IsString
            ? value.AsString : string.Empty;

    private static string RosterIdentity(Guid matchId, IEnumerable<Guid> users) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            matchId.ToString("D") + "|" + string.Join(',', users.Select(id => id.ToString("D")).Order(StringComparer.Ordinal)))))
            .ToLowerInvariant();
}
