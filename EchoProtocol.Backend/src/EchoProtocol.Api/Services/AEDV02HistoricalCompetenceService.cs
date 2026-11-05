using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Enums;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed class AEDV02HistoricalCompetenceService(AppDbContext db, ITelemetryEventRepository telemetry, TimeProvider clock)
{
    public async Task<Dictionary<Guid, AEDV02HistoricalCompetence>> ReadAsync(Guid currentMatch, Guid[] users,
        string sceneFingerprint, string axis, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var context = AEDV02PilotOpportunities.ComparisonContext(sceneFingerprint, users.Length);
        var cutoff = now.AddDays(-AEDV02PilotOpportunities.HistoryMaxAgeDays);
        var matches = await db.MatchPlayerBindings.AsNoTracking().Where(x => users.Contains(x.UserId) && x.MatchId != currentMatch)
            .Select(x => x.MatchId).Distinct().ToArrayAsync(ct);
        var ended = await db.MatchAuthorityBindings.AsNoTracking().Where(x => matches.Contains(x.MatchId) && x.Status == MatchAuthorityStatus.Ended &&
                !db.MatchPlayerBindings.Any(player => player.MatchId == x.MatchId && (player.HadGameplayDisconnect || player.DisconnectedAtUtc != null)))
            .Select(x => x.MatchId).ToArrayAsync(ct);
        var proofs = await db.AEDV02VerifiedSources.AsNoTracking().Where(x => ended.Contains(x.MatchId) &&
            x.SceneManifestFingerprint == sceneFingerprint && x.SourceEndedAtUtc >= cutoff && x.SourceEndedAtUtc <= now)
            .OrderByDescending(x => x.SourceEndedAtUtc).Take(512).ToArrayAsync(ct);
        var observations = new List<AEDV02HistoricalOpportunity>();
        var content = await db.AEDV02SceneContents.AsNoTracking().SingleOrDefaultAsync(x => x.Fingerprint == sceneFingerprint, ct);
        AEDV02SceneManifest? manifest = null;
        try
        {
            if (content != null && AEDV02Plan.Hash(content.ManifestJson) == sceneFingerprint)
                manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(content.ManifestJson);
        }
        catch (JsonException) { }
        if (manifest?.IsValid() != true)
            return users.ToDictionary(user => user, user => AEDV02PilotOpportunities.Competence(user, axis, context, now, observations));
        var cache = new Dictionary<Guid, IReadOnlyList<TelemetryEventDocument>>();
        foreach (var proof in proofs)
        {
            AEDV02SourceResult? reconstructed;
            try { reconstructed = JsonSerializer.Deserialize<AEDV02SourceResult>(proof.ReconstructedJson); }
            catch (JsonException) { continue; }
            if (reconstructed == null || !reconstructed.PressureComplete || reconstructed.OpportunityContractVersion != AEDV02PilotOpportunities.Version ||
                reconstructed.ComparisonContext != context || reconstructed.Fingerprint != proof.Fingerprint ||
                reconstructed.RosterIdentity != proof.RosterIdentity || !AEDV02WorldSample.Hash(reconstructed.PlanFingerprint)) continue;
            if (!cache.TryGetValue(proof.MatchId, out var events)) cache.Add(proof.MatchId, events = await telemetry.LoadAcceptedMatchEventsAsync(proof.MatchId, ct));
            try
            {
                var terminals = events.Where(x => x.EventType == "MATCH_ENDED" && x.SchemaVersion == "1.1").ToArray();
                if (terminals.Length != 1 || events.Any(x => x.EventType is "MATCH_ABORTED" or "PLAYER_DISCONNECTED" or "AED_V02_CAPTURE_EXCLUDED")) continue;
                var terminal = terminals[0];
                if (terminal.ReasonCode == "MATCH_ABORTED" || terminal.ValueJson["data"].AsBsonDocument.GetValue("outcome", "").AsString == "ABORTED") continue;
                var seals = events.Where(x => x.EventType == "AED_V02_CAPTURE_SEAL" && x.SchemaVersion == "1.3" && x.EventSequence < terminal.EventSequence).ToArray();
                if (seals.Length != 1 || seals[0].ValueJson["data"]["exclusion"] != "NONE" || seals[0].Ts > terminal.Ts ||
                    seals[0].Ts < proof.SourceEndedAtUtc || seals[0].EventSequence <= proof.EndEventSequence ||
                    seals[0].ValueJson["context"]["authorityTick"].ToInt64() < proof.EndTick ||
                    seals[0].ValueJson["context"]["authorityTick"].ToInt64() > terminal.ValueJson["context"]["authorityTick"].ToInt64()) continue;
                if (terminal.Ts < proof.SourceEndedAtUtc || terminal.EventSequence <= proof.EndEventSequence) continue;
                var frames = events.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE" && x.SchemaVersion == "1.3" && x.EventSequence <= proof.EndEventSequence)
                    .Select(x => (Event: x, Sample: JsonSerializer.Deserialize<AEDV02WorldSample>(x.ValueJson["data"]["world"].ToJson())!))
                    .Where(x => x.Sample != null && x.Sample.Tick >= proof.StartTick && x.Sample.Tick <= proof.EndTick).OrderBy(x => x.Event.EventSequence).ToList();
                if (frames.Count < 2 || frames[0].Sample.Tick != proof.StartTick || frames[^1].Sample.Tick != proof.EndTick ||
                    frames.Any(x => !x.Sample.IsValid() || x.Sample.OpportunityContractVersion != AEDV02PilotOpportunities.Version ||
                        x.Sample.EvidenceExclusion.Length != 0 || x.Sample.RosterIdentity != proof.RosterIdentity || x.Sample.Players.Length != users.Length ||
                        x.Sample.PhaseOrdinal != proof.PhaseOrdinal || x.Sample.SceneManifestFingerprint != sceneFingerprint)) continue;
                var sourceRange = events.Where(x => x.EventSequence >= frames[0].Event.EventSequence && x.EventSequence <= frames[^1].Event.EventSequence)
                    .OrderBy(x => x.EventSequence).ToArray();
                if (sourceRange.Length != frames[^1].Event.EventSequence - frames[0].Event.EventSequence + 1 ||
                    sourceRange.Select(x => x.EventSequence).Distinct().Count() != sourceRange.Length) continue;
                var fingerprint = AEDV02Plan.Hash(AEDV02SourceVerifier.VerificationVersion + "|" + AEDV02PilotPolicy.Version + "|" + AEDV02WorldSample.SourceVersion + "|" +
                    proof.RosterIdentity + "|" + sceneFingerprint + "|" + string.Join('|', sourceRange.Select(x => x.Id + ":" + x.EventSequence + ":" + x.SemanticFingerprint + ":" + x.ValueJson.ToJson())));
                if (fingerprint != proof.Fingerprint) continue;
                // Rebuild from accepted events again: admin/legacy normalized observation rows confer no competence.
                foreach (var opportunity in AEDV02SourceVerifier.ResolveOpportunities(events, frames, manifest))
                    observations.Add(new AEDV02HistoricalOpportunity { MatchId = proof.MatchId, Context = context,
                        ContractVersion = AEDV02PilotOpportunities.Version, SourceFingerprint = fingerprint,
                        EndedAtUtc = terminal.Ts, Opportunity = opportunity });
            }
            catch (Exception error) when (error is JsonException or FormatException or InvalidCastException or KeyNotFoundException or OverflowException)
            {
                // A corrupt stored source cannot grant competence; repository and cancellation failures still propagate.
            }
        }
        return users.ToDictionary(user => user, user => AEDV02PilotOpportunities.Competence(user, axis, context, now, observations));
    }
}
