using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Entities;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed class AEDV02SourceVerifier(AppDbContext db, ITelemetryEventRepository telemetry, TimeProvider clock)
{
    public async Task<ServiceResult<AEDV02SourceResult>> VerifyAsync(Guid matchId, AEDV02SourceRequest request, CancellationToken ct = default)
    {
        if (request == null || matchId == Guid.Empty || request.PhaseOrdinal < 1 || request.StartTick < 0 ||
            request.EndTick <= request.StartTick || !AEDV02WorldSample.Hash(request.SceneManifestFingerprint)) return Hold("AED_V02_SOURCE_INVALID");
        var content = await db.AEDV02SceneContents.AsNoTracking().SingleOrDefaultAsync(x => x.Fingerprint == request.SceneManifestFingerprint, ct);
        if (content == null || AEDV02Plan.Hash(content.ManifestJson) != content.Fingerprint) return Hold("HOLD_UNTRUSTED_SCENE_MANIFEST");
        AEDV02SceneManifest? manifest;
        try { manifest = JsonSerializer.Deserialize<AEDV02SceneManifest>(content.ManifestJson); }
        catch (JsonException) { return Hold("HOLD_UNTRUSTED_SCENE_MANIFEST"); }
        if (manifest == null || !manifest.IsValid()) return Hold("HOLD_UNTRUSTED_SCENE_MANIFEST");
        var geometryResults = new Dictionary<string, bool>(StringComparer.Ordinal);
        var users = await db.MatchPlayerBindings.AsNoTracking().Where(x => x.MatchId == matchId).Select(x => x.UserId).ToArrayAsync(ct);
        if (users.Length == 0 || await db.MatchPlayerBindings.AnyAsync(x => x.MatchId == matchId && x.DisconnectedAtUtc != null, ct)) return Hold("HOLD_ROSTER_NOT_CURRENT");
        var roster = AEDV02Plan.Hash(matchId.ToString("D") + "|" + string.Join(',', users.Order().Select(x => x.ToString("D"))));
        var accepted = await telemetry.LoadAcceptedMatchEventsAsync(matchId, ct);
        var frames = new List<(TelemetryEventDocument Event, AEDV02WorldSample Sample)>();
        foreach (var item in accepted.Where(x => x.MatchId == matchId && x.EventType == "AED_V02_WORLD_SAMPLE").OrderBy(x => x.EventSequence))
        {
            try
            {
                var sample = JsonSerializer.Deserialize<AEDV02WorldSample>(item.ValueJson["data"]["world"].ToJson());
                if (sample == null || sample.Tick < request.StartTick || sample.Tick > request.EndTick) continue;
                if (item.SchemaVersion != "1.3" || !sample.IsValid() || sample.Tick != item.ValueJson["context"]["authorityTick"].ToInt64() ||
                    item.EventSequence != item.ValueJson["context"]["eventSequence"].ToInt64() ||
                    sample.PhaseOrdinal != request.PhaseOrdinal || sample.RosterIdentity != roster ||
                    sample.SceneManifestFingerprint != content.Fingerprint || sample.EvidenceExclusion.Length != 0 ||
                    !sample.Players.Select(x => x.UserId).ToHashSet().SetEquals(users)) return Hold("HOLD_INSUFFICIENT_EVIDENCE");
                frames.Add((item, sample));
            }
            catch (Exception ex) when (ex is JsonException or InvalidCastException or KeyNotFoundException or FormatException) { return Hold("AED_V02_SOURCE_INVALID"); }
        }
        if (frames.Count < 2 || frames[0].Sample.Tick != request.StartTick || frames[^1].Sample.Tick != request.EndTick) return Hold("HOLD_SOURCE_RANGE_MISSING");
        var last = frames[^1]; var tickRate = last.Sample.TickRate;
        var sourceRange = accepted.Where(x => x.MatchId == matchId && x.EventSequence >= frames[0].Event.EventSequence &&
            x.EventSequence <= last.Event.EventSequence).OrderBy(x => x.EventSequence).ToArray();
        if (sourceRange.Length != last.Event.EventSequence - frames[0].Event.EventSequence + 1 ||
            sourceRange.Select(x => x.EventSequence).Distinct().Count() != sourceRange.Length) return Hold("HOLD_SOURCE_SEQUENCE_GAP");
        if (request.EndTick - request.StartTick < tickRate * AEDV02PilotPolicy.ObservationSeconds ||
            request.EndTick - request.StartTick > tickRate * AEDV02PilotPolicy.WindowSeconds ||
            clock.GetUtcNow().UtcDateTime - last.Event.Ts > TimeSpan.FromSeconds(AEDV02PilotPolicy.EvidenceMaxAgeSeconds) ||
            last.Event.Ts > clock.GetUtcNow().UtcDateTime.AddSeconds(2)) return Hold("HOLD_STALE_SOURCE");
        var facts = users.ToDictionary(x => x, _ => new List<AEDV02PressureFact>());
        for (int i = 0; i < frames.Count; i++)
        {
            var frame = frames[i]; var sample = frame.Sample;
            var phaseStarts = accepted.Where(x => x.MatchId == matchId && x.EventType == "PHASE_STARTED" && x.EventSequence < frame.Event.EventSequence)
                .OrderBy(x => x.EventSequence).ToArray();
            if (phaseStarts.Length != sample.PhaseOrdinal || phaseStarts[^1].ValueJson["context"]["phase"].AsString != sample.PhaseName ||
                sample.Stalkers.Concat(sample.Minions).Select(x => x.NetworkId).Distinct(StringComparer.Ordinal).Count() != sample.Stalkers.Length + sample.Minions.Length)
                return Hold("HOLD_PHASE_SOURCE_MISMATCH");
            if (sample.TickRate != tickRate || sample.PlanRevision != last.Sample.PlanRevision || sample.PlanFingerprint != last.Sample.PlanFingerprint ||
                Math.Abs((frame.Event.Ts - frames[0].Event.Ts).TotalSeconds - (sample.Tick - frames[0].Sample.Tick) / (double)tickRate) > 2 ||
                (i > 0 && (sample.Tick <= frames[i - 1].Sample.Tick || sample.Tick - frames[i - 1].Sample.Tick > tickRate * 2))) return Hold("HOLD_SOURCE_COVERAGE_GAP");
            if (!AEDPhase8LifecycleReconcilerV1.TryReconstruct(accepted, matchId, frame.Event.EventSequence, users.ToHashSet(), false,
                out var tools, out var minions, out _)) return Hold("HOLD_LIFECYCLE_RECONCILIATION");
            var required = accepted.Where(x => x.MatchId == matchId && x.EventSequence < frame.Event.EventSequence && x.EventType == "TEAM_TOOL_LIFECYCLE")
                .Select(x => x.ValueJson["data"].AsBsonDocument).Where(x => x["action"].AsString == "SPAWN" && x["zone"].AsInt32 == sample.Zone)
                .Select(x => x["toolId"].AsInt32).ToHashSet();
            if (!new[] { 1, 2, 3, 4, 6 }.All(required.Contains)) return Hold("HOLD_REQUIRED_SUPPLY_RECEIPTS_MISSING");
            if (minions.Count != sample.Minions.Length || sample.Minions.Any(x => !minions.TryGetValue(x.NetworkId, out var zone) || zone != x.Zone)) return Hold("HOLD_MINION_CENSUS_MISMATCH");
            if (tools.Count != sample.Tools.Length || sample.Tools.Any(x => !tools.TryGetValue(x.InstanceId, out var tool) ||
                tool.ToolId != x.ToolId || tool.Zone != x.Zone || tool.Location != x.Location || tool.Owner != x.OwnerUserId ||
                tool.Charges != x.RemainingCharges || tool.Origin != x.Origin)) return Hold("HOLD_TOOL_CENSUS_MISMATCH");
            foreach (var tool in sample.Tools.Where(x => x.Location == "World"))
            {
                var marker = manifest.Markers.SingleOrDefault(x => x.Id == tool.MarkerId && x.Kind == "TOOL");
                if (tool.Origin is "WorldSpawn" or "ScenePlaced" && (marker == null || !marker.AllowedToolIds.Contains(tool.ToolId))) return Hold("HOLD_TOOL_PLACEMENT_WHITELIST");
                if (manifest.NavCells.Length != 0)
                {
                    var entry = manifest.Markers.SingleOrDefault(x => x.Id == "Z" + (marker?.Zone ?? tool.Zone) + "_ENTRY");
                    if (entry == null) return Hold("HOLD_SUPPLY_ROUTE_INVALID");
                    string identity = JsonSerializer.Serialize(new { entry.Id, tool.X, tool.Y, tool.Z, sample.Doors });
                    if (!geometryResults.TryGetValue(identity, out var reachable)) geometryResults.Add(identity, reachable = manifest.HasReachableGeometry(
                        [entry.X, entry.Y, entry.Z], [tool.X, tool.Y, tool.Z], sample.Doors));
                    if (!reachable) return Hold("HOLD_SUPPLY_ROUTE_INVALID");
                }
                else if (marker == null || marker.Zone != tool.Zone || tool.Origin == "PlayerDrop" ||
                    Math.Abs(marker.X - tool.X) > .5 || Math.Abs(marker.Y - tool.Y) > 1 || Math.Abs(marker.Z - tool.Z) > .5 ||
                    !manifest.TryRoute("Z" + tool.Zone + "_ENTRY", tool.MarkerId, tool.RouteEdgeIds, sample.Doors, out _)) return Hold("HOLD_SUPPLY_ROUTE_INVALID");
            }
            foreach (var player in sample.Players)
            {
                var life = "ALIVE";
                foreach (var item in accepted.Where(x => x.MatchId == matchId && x.UserId == player.UserId && x.EventSequence < frame.Event.EventSequence).OrderBy(x => x.EventSequence))
                {
                    if (item.EventType == "PLAYER_DOWNED") { if (life != "ALIVE") return Hold("HOLD_PLAYER_LIFECYCLE_MISMATCH"); life = "DOWNED"; }
                    else if (item.EventType == "PLAYER_REVIVED") { if (life != "DOWNED") return Hold("HOLD_PLAYER_LIFECYCLE_MISMATCH"); life = "ALIVE"; }
                    else if (item.EventType == "PLAYER_ELIMINATED") { if (life == "ELIMINATED") return Hold("HOLD_PLAYER_LIFECYCLE_MISMATCH"); life = "ELIMINATED"; }
                }
                if (life != player.LifeState) return Hold("HOLD_PLAYER_LIFECYCLE_MISMATCH");
                var held = sample.Tools.SingleOrDefault(x => x.Location == "Player" && x.OwnerUserId == player.UserId);
                if ((held?.InstanceId ?? string.Empty) != player.HeldToolInstanceId) return Hold("HOLD_INVENTORY_CENSUS_MISMATCH");
            }
            if (i == 0) continue;
            var previous = frames[i - 1].Sample;
            foreach (var threat in previous.Stalkers.Where(x => x.TargetUserId.HasValue && x.State is "CHASE" or "ATTACK"))
                Add(threat.TargetUserId!.Value, threat.EpisodeId, "S:" + threat.NetworkId, AEDV02PressureChannel.Stalker, previous.Tick, sample.Tick);
            foreach (var threat in previous.Minions.Where(x => x.TargetUserId.HasValue && x.State == "HARASS"))
                Add(threat.TargetUserId!.Value, threat.EpisodeId, "M:" + threat.NetworkId, AEDV02PressureChannel.Minion, previous.Tick, sample.Tick);
            foreach (var player in previous.Players.Where(x => x.ObjectiveState == "THREAT_INTERRUPTED"))
            {
                var cause = previous.Stalkers.Concat(previous.Minions).SingleOrDefault(x => x.NetworkId == player.ObjectiveCauseEntityId && x.TargetUserId == player.UserId && x.State is "CHASE" or "ATTACK" or "HARASS");
                if (cause == null || !manifest.Markers.Any(x => x.Id == player.ObjectiveUnitId && x.Kind == "OBJECTIVE")) return Hold("HOLD_OBJECTIVE_CAUSE_UNVERIFIED");
                Add(player.UserId, player.ObjectiveUnitId + ":" + cause.EpisodeId, "O:" + player.ObjectiveUnitId,
                    AEDV02PressureChannel.Objective, previous.Tick, sample.Tick, cause.EpisodeId);
            }
            foreach (var player in previous.Players.Where(x => x.RequiredToolId != 0))
            {
                if (!manifest.Markers.Any(x => x.Id == player.ObjectiveUnitId && x.Kind == "OBJECTIVE")) return Hold("HOLD_RESOURCE_NEED_UNVERIFIED");
                if (!previous.Tools.Any(x => x.ToolId == player.RequiredToolId && x.RemainingCharges != 0 && x.Location is "World" or "Player"))
                    Add(player.UserId, "need:" + player.ObjectiveUnitId, "R:" + player.ObjectiveUnitId,
                        AEDV02PressureChannel.Resource, previous.Tick, sample.Tick);
            }
            void Add(Guid user, string episode, string identity, AEDV02PressureChannel channel, long start, long end, string? cause = null)
            {
                facts[user].Add(new AEDV02PressureFact { MatchId = matchId, UserId = user, EventId = frame.Event.Id + ":" + identity + ":" + user,
                    EpisodeId = episode, Channel = channel, StartedSeconds = start / (double)tickRate, EndedSeconds = end / (double)tickRate, CauseEventId = cause });
            }
        }
        foreach (var item in accepted.Where(x => x.MatchId == matchId && x.EventType == "PLAYER_DOWNED" &&
            x.EventSequence > frames[0].Event.EventSequence && x.EventSequence <= last.Event.EventSequence))
        {
            if (!item.UserId.HasValue || !facts.ContainsKey(item.UserId.Value)) return Hold("HOLD_DOWN_SOURCE_UNVERIFIED");
            if (!item.ValueJson.TryGetValue("context", out var downContext) || !downContext.AsBsonDocument.TryGetValue("authorityTick", out var downTick) ||
                !downTick.IsNumeric || downTick.ToInt64() < request.StartTick || downTick.ToInt64() > request.EndTick ||
                Math.Abs((item.Ts - frames[0].Event.Ts).TotalSeconds - (downTick.ToInt64() - request.StartTick) / (double)tickRate) > 2) return Hold("HOLD_DOWN_SOURCE_UNVERIFIED");
            double seconds = downTick.ToInt64() / (double)tickRate;
            facts[item.UserId.Value].Add(new AEDV02PressureFact { MatchId = matchId, UserId = item.UserId.Value,
                EventId = item.Id.ToString("D"), EpisodeId = "down:" + item.Id, Channel = AEDV02PressureChannel.Down,
                StartedSeconds = seconds, EndedSeconds = seconds });
        }
        double now = request.EndTick / (double)tickRate;
        var pressure = facts.ToDictionary(x => x.Key, x => AEDV02PressureNormalizer.Project(matchId, x.Key, now,
            request.StartTick / (double)tickRate, Enumerable.Repeat(now, 5).ToArray(), x.Value));
        if (pressure.Values.Any(x => !x.Complete)) return Hold("HOLD_INSUFFICIENT_EVIDENCE");
        var safe = last.Sample.Stalkers.All(x => x.State is "PATROL" or "SEARCH") &&
            last.Sample.Minions.All(x => x.State is not ("TRACK" or "HARASS")) && last.Sample.Players.All(x => x.LifeState == "ALIVE" && x.ObjectiveState == "NONE");
        var fingerprint = AEDV02Plan.Hash(AEDV02WorldSample.SourceVersion + "|" + roster + "|" + content.Fingerprint + "|" +
            string.Join('|', sourceRange.Select(x => x.Id + ":" + x.EventSequence + ":" + x.SemanticFingerprint + ":" + x.ValueJson.ToJson())));
        var result = new AEDV02SourceResult(fingerprint, request.PhaseOrdinal, request.StartTick, request.EndTick, roster,
            content.Fingerprint, pressure.ToDictionary(x => x.Key, x => x.Value.Score),
            pressure.ToDictionary(x => x.Key, x => x.Value.Components.ToArray()),
            facts.ToDictionary(x => x.Key, x => x.Value.Where(f => f.Channel == AEDV02PressureChannel.Stalker)
                .OrderByDescending(f => f.EndedSeconds).Select(f => f.EpisodeId).FirstOrDefault() ?? string.Empty),
            accepted.Where(x => x.MatchId == matchId && x.UserId.HasValue && x.EventType is "PLAYER_DOWNED" or "PLAYER_REVIVED" &&
                x.Ts >= last.Event.Ts.AddSeconds(-AEDV02PilotPolicy.DownRecoverySeconds) && x.EventSequence <= last.Event.EventSequence)
                .Select(x => x.UserId!.Value).Distinct().ToArray(), safe, last.Sample.LogicalPhase, last.Sample.Zone, last.Sample.PhaseName,
            last.Sample.PlanRevision, last.Sample.PlanFingerprint, last.Event.Ts);
        if (!await db.AEDV02VerifiedSources.AnyAsync(x => x.Fingerprint == fingerprint, ct))
        {
            db.AEDV02VerifiedSources.Add(new AEDV02VerifiedSource { Fingerprint = fingerprint, MatchId = matchId,
                PhaseOrdinal = request.PhaseOrdinal, StartTick = request.StartTick, EndTick = request.EndTick,
                EndEventSequence = last.Event.EventSequence, RosterIdentity = roster, SceneManifestFingerprint = content.Fingerprint,
                ReconstructedJson = JsonSerializer.Serialize(result), VerifiedAtUtc = clock.GetUtcNow().UtcDateTime, SourceEndedAtUtc = last.Event.Ts });
            await db.SaveChangesAsync(ct);
        }
        return ServiceResult<AEDV02SourceResult>.Success(result, "AED v0.2 source reconstructed");
    }
    private static ServiceResult<AEDV02SourceResult> Hold(string reason) => ServiceResult<AEDV02SourceResult>.Failure(reason, ErrorCodes.ScenarioApplyConflict);
}
