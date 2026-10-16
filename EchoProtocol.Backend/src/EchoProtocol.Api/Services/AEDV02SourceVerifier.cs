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

public sealed partial class AEDV02SourceVerifier(AppDbContext db, ITelemetryEventRepository telemetry, TimeProvider clock)
{
    public const string VerificationVersion = "AED_V02_SOURCE_VERIFICATION_REV02_V6";
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
        var geometryResults = new Dictionary<string, bool?>(StringComparer.Ordinal);
        var users = await db.MatchPlayerBindings.AsNoTracking().Where(x => x.MatchId == matchId).Select(x => x.UserId).ToArrayAsync(ct);
        if (users.Length == 0 || await db.MatchPlayerBindings.AnyAsync(x => x.MatchId == matchId && x.DisconnectedAtUtc != null, ct)) return Hold("HOLD_ROSTER_NOT_CURRENT");
        var roster = AEDV02Plan.Hash(matchId.ToString("D") + "|" + string.Join(',', users.Order().Select(x => x.ToString("D"))));
        var accepted = await telemetry.LoadAcceptedMatchEventsAsync(matchId, ct);
        if (accepted.Any(x => x.MatchId == matchId && (x.EventType == "AED_V02_CAPTURE_EXCLUDED" ||
            x.EventType == "AED_V02_CAPTURE_SEAL" && x.ValueJson["data"]["exclusion"] != "NONE"))) return Hold("HOLD_SOURCE_CAPTURE_EXCLUDED");
        var usedReviveTools = new HashSet<Guid>();
        foreach (var revived in accepted.Where(x => x.MatchId == matchId && x.EventType == "PLAYER_REVIVED").OrderBy(x => x.EventSequence))
        {
            if (!revived.UserId.HasValue || !users.Contains(revived.UserId.Value) ||
                !revived.ValueJson["data"].AsBsonDocument.TryGetValue("reviverPlayerId", out var actor) || !actor.IsString ||
                !Guid.TryParse(actor.AsString, out var reviver) || !users.Contains(reviver) || reviver == revived.UserId.Value ||
                !revived.ValueJson["data"].AsBsonDocument.TryGetValue("usedFirstAidKit", out var kit) || !kit.IsBoolean || !kit.AsBoolean)
                return Hold("HOLD_REVIVE_TOOL_SOURCE_MISMATCH");
            var down = accepted.Where(x => x.MatchId == matchId && x.UserId == revived.UserId && x.EventType == "PLAYER_DOWNED" &&
                x.EventSequence < revived.EventSequence).OrderByDescending(x => x.EventSequence).FirstOrDefault();
            var consume = accepted.Where(x => x.MatchId == matchId && x.EventType == "TEAM_TOOL_LIFECYCLE" &&
                x.EventSequence > (down?.EventSequence ?? long.MaxValue) && x.EventSequence < revived.EventSequence &&
                x.Ts <= revived.Ts && revived.Ts - x.Ts <= TimeSpan.FromSeconds(2)).OrderByDescending(x => x.EventSequence)
                .FirstOrDefault(x => x.ValueJson["data"]["action"] == "CONSUME" && x.ValueJson["data"]["toolId"] == 3 &&
                    x.ValueJson["data"]["ownerUserId"] == reviver.ToString("D") && x.ValueJson["data"]["remainingCharges"] == 0);
            if (consume == null || !usedReviveTools.Add(consume.Id) || consume.ValueJson["context"]["authorityTick"] != revived.ValueJson["context"]["authorityTick"])
                return Hold("HOLD_REVIVE_TOOL_SOURCE_MISMATCH");
        }
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
        var planRow = last.Sample.PlanRevision == 0 ? null : await db.AEDV02Decisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.MatchId == matchId && x.PlanRevision == last.Sample.PlanRevision && x.ApprovalStatus == "APPROVED" && x.ApplyStatus != "ABORTED", ct);
        var observedPlan = planRow == null ? AEDV02Plan.Normal() : new AEDV02Plan(JsonSerializer.Deserialize<double[]>(planRow.PlanValuesJson)!);
        if (last.Sample.PlanFingerprint != observedPlan.Fingerprint() || last.Sample.PlanRevision > 0 && planRow == null)
            return Hold("HOLD_PLAN_LINEAGE_MISMATCH");
        var sourceRange = accepted.Where(x => x.MatchId == matchId && x.EventSequence >= frames[0].Event.EventSequence &&
            x.EventSequence <= last.Event.EventSequence).OrderBy(x => x.EventSequence).ToArray();
        if (sourceRange.Length != last.Event.EventSequence - frames[0].Event.EventSequence + 1 ||
            sourceRange.Select(x => x.EventSequence).Distinct().Count() != sourceRange.Length) return Hold("HOLD_SOURCE_SEQUENCE_GAP");
        if (request.EndTick - request.StartTick < tickRate * (request.AllowEmergencyWindow ? AEDV02PilotOpportunities.ScarcityMinimumSeconds : AEDV02PilotPolicy.ObservationSeconds) ||
            request.EndTick - request.StartTick > tickRate * AEDV02PilotPolicy.WindowSeconds ||
            clock.GetUtcNow().UtcDateTime - last.Event.Ts > TimeSpan.FromSeconds(AEDV02PilotPolicy.EvidenceMaxAgeSeconds) ||
            last.Event.Ts > clock.GetUtcNow().UtcDateTime.AddSeconds(2)) return Hold("HOLD_STALE_SOURCE");
        var facts = users.ToDictionary(x => x, _ => new List<AEDV02PressureFact>());
        var supportInstances = await AcceptedSupportInstances(matchId, accepted, ct);
        bool pilotOpportunities = frames.All(x => x.Sample.OpportunityContractVersion == AEDV02PilotOpportunities.Version);
        if (!TryReadImpacts(accepted, frames, out var impacts)) return Hold("HOLD_CAUSAL_IMPACT_LINEAGE_INVALID");
        if (!TryResolveResourceManagementOpportunities(accepted, frames, manifest, out _))
            return Hold("HOLD_RESOURCE_MANAGEMENT_LINEAGE_INVALID");
        if (!TryResolveNoiseMakerOpportunities(accepted, frames, manifest, out _)) return Hold("HOLD_TOOL_NOISE_LINEAGE_INVALID");
        if (!TryResolveJammerOpportunities(accepted, frames, manifest, out _)) return Hold("HOLD_TOOL_JAMMER_LINEAGE_INVALID");
        if (!TryResolveScannerOpportunities(accepted, frames, manifest, out _)) return Hold("HOLD_TOOL_SCANNER_LINEAGE_INVALID");
        if (!TryResolveStabilizerOpportunities(accepted, frames, manifest, out _, out var stabilizerReason)) return Hold(stabilizerReason);
        var carrierEvents = accepted.Where(x => x.MatchId == matchId && x.EventType == "AED_V02_TOOL_CARRIER").OrderBy(x => x.EventSequence).ToArray();
        var carriers = new Dictionary<string, string>(StringComparer.Ordinal);
        var relocatedWorldSpawnTools = new HashSet<string>(StringComparer.Ordinal);
        int carrierIndex = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            var frame = frames[i]; var sample = frame.Sample;
            if (sample.Supply != null && !VerifySupply(sample, manifest, observedPlan, planRow?.InitialOptionalBudget ?? 0, accepted, frame.Event.EventSequence, supportInstances))
                return Hold("HOLD_SUPPLY_RESERVATION_MISMATCH");
            if (sample.Stalkers.Length == 0) return Hold("HOLD_STALKER_CENSUS_MISSING");
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
                .Select(x => x.ValueJson["data"].AsBsonDocument).Where(x => x["action"].AsString == "SPAWN" && x["zone"].AsInt32 == sample.Zone &&
                    x.TryGetValue("origin", out var origin) && origin == "WorldSpawn")
                .Select(x => x["toolId"].AsInt32).ToHashSet();
            if (!new[] { 1, 2, 3, 4, 6 }.All(required.Contains)) return Hold("HOLD_REQUIRED_SUPPLY_RECEIPTS_MISSING");
            if (minions.Count != sample.Minions.Length || sample.Minions.Any(x => !minions.TryGetValue(x.NetworkId, out var zone) || zone != x.Zone)) return Hold("HOLD_MINION_CENSUS_MISMATCH");
            if (tools.Count != sample.Tools.Length || sample.Tools.Any(x => !tools.TryGetValue(x.InstanceId, out var tool) ||
                tool.ToolId != x.ToolId || tool.Zone != x.Zone || tool.Location != x.Location || tool.Owner != x.OwnerUserId ||
                tool.Charges != x.RemainingCharges || tool.Origin != x.Origin)) return Hold("HOLD_TOOL_CENSUS_MISMATCH");
            while (carrierIndex < carrierEvents.Length && carrierEvents[carrierIndex].EventSequence < frame.Event.EventSequence)
            {
                var item = carrierEvents[carrierIndex++];
                AEDV02ToolCarrierSource? transition;
                try { transition = JsonSerializer.Deserialize<AEDV02ToolCarrierSource>(item.ValueJson["data"]["carrier"].ToJson()); }
                catch (JsonException) { return Hold("HOLD_TOOL_CARRIER_SOURCE_INVALID"); }
                if (item.SchemaVersion != "1.3" || transition == null || !transition.IsValid() || transition.Tick > sample.Tick || transition.Tick != item.ValueJson["context"]["authorityTick"].ToInt64() ||
                    !AEDPhase8LifecycleReconcilerV1.TryReconstruct(accepted, matchId, item.EventSequence, users.ToHashSet(), false, out var priorTools, out var priorMinions, out _) ||
                    !priorTools.TryGetValue(transition.ToolInstanceId, out var priorTool) || priorTool.Location != "World") return Hold("HOLD_TOOL_CARRIER_SOURCE_INVALID");
                if (transition.Action == "STEAL")
                {
                    if (!priorMinions.ContainsKey(transition.MinionNetworkId) || carriers.ContainsKey(transition.ToolInstanceId) || carriers.Values.Contains(transition.MinionNetworkId))
                        return Hold("HOLD_TOOL_CARRIER_SOURCE_INVALID");
                    carriers.Add(transition.ToolInstanceId, transition.MinionNetworkId);
                }
                else
                {
                    if (!carriers.Remove(transition.ToolInstanceId, out var owner) ||
                        owner != transition.MinionNetworkId)
                        return Hold("HOLD_TOOL_CARRIER_SOURCE_INVALID");

                    relocatedWorldSpawnTools.Add(transition.ToolInstanceId);
                }
            }
            if (sample.Tools.Any(x => x.CarrierMinionId != carriers.GetValueOrDefault(x.InstanceId, string.Empty)) ||
                carriers.Keys.Any(x => !tools.ContainsKey(x))) return Hold("HOLD_TOOL_CARRIER_CENSUS_MISMATCH");
            foreach (var tool in sample.Tools.Where(x => x.Location == "World"))
            {
                if (tool.CarrierMinionId.Length != 0) continue;
                var marker = manifest.Markers.SingleOrDefault(x => x.Id == tool.MarkerId && x.Kind == "TOOL");
                if (tool.Origin is "WorldSpawn" or "ScenePlaced" && (marker == null || !marker.AllowedToolIds.Contains(tool.ToolId))) return Hold("HOLD_TOOL_PLACEMENT_WHITELIST");
                if (tool.Origin == "WorldSpawn")
                {
                    if (marker == null ||
                        marker.Zone != tool.Zone ||
                        !manifest.TryIsInsideSafeRoom(marker.X, marker.Y, marker.Z, out var markerInside) ||
                        markerInside ||
                        !manifest.TryIsInsideSafeRoom(tool.X, tool.Y, tool.Z, out var toolInside) ||
                        toolInside)
                        return Hold("HOLD_TOOL_PLACEMENT_SAFE_ROOM_INVALID");

                    if (!relocatedWorldSpawnTools.Contains(tool.InstanceId))
                    {
                        double dx = tool.X - marker.X;
                        double dz = tool.Z - marker.Z;
                        double dy = Math.Abs(tool.Y - marker.Y);

                        if (dx * dx + dz * dz > 0.75 * 0.75 ||
                            dy > 1.0)
                            return Hold("HOLD_TOOL_PLACEMENT_SAFE_ROOM_INVALID");
                    }
                }
                if (manifest.NavCells.Length != 0)
                {
                    var entry = manifest.Markers.SingleOrDefault(x => x.Id == "Z" + (marker?.Zone ?? tool.Zone) + "_ENTRY");
                    if (entry == null) return Hold("HOLD_SUPPLY_ROUTE_INVALID");
                    if (!Reach([entry.X, entry.Y, entry.Z], [tool.X, tool.Y, tool.Z], sample.Doors).HasValue) return Hold("HOLD_SUPPLY_ROUTE_INVALID");
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
            foreach (var threat in previous.Stalkers.Where(x => x.TargetUserId.HasValue && previous.Players.Single(p => p.UserId == x.TargetUserId.Value).LifeState == "ALIVE" && x.State is "CHASE" or "ATTACK"))
                Add(threat.TargetUserId!.Value, threat.EpisodeId, "S:" + threat.NetworkId, AEDV02PressureChannel.Stalker, previous.Tick, sample.Tick);
            foreach (var threat in previous.Minions.Where(x => x.TargetUserId.HasValue && previous.Players.Single(p => p.UserId == x.TargetUserId.Value).LifeState == "ALIVE" && x.State == "HARASS"))
                Add(threat.TargetUserId!.Value, threat.EpisodeId, "M:" + threat.NetworkId, AEDV02PressureChannel.Minion, previous.Tick, sample.Tick);
            // A threat target alone does not independently prove an interrupted opportunity or a tool need.
            foreach (var player in previous.Players)
            {
                if (player.ObjectiveState == "THREAT_INTERRUPTED")
                {
                    if (!pilotOpportunities || !Guid.TryParse(player.ObjectiveCauseEventId, out var cause) || !impacts.TryGetValue(cause, out var impact) ||
                        impact.UserId != player.UserId || impact.EntityId != player.ObjectiveCauseEntityId || impact.ObjectiveUnitId != player.ObjectiveUnitId ||
                        impact.Tick > previous.Tick || player.LifeState == "ALIVE" && !player.IsCaught && player.SlowUntilTick <= previous.Tick &&
                            !(impact.Kind == "MINION_CORE_DROP" && previous.Minions.Any(x => x.NetworkId == impact.EntityId &&
                                x.TargetUserId == player.UserId && x.State is "TRACK" or "HARASS")) ||
                        previous.Players.Any(x => x.UserId != player.UserId && x.ObjectiveUnitId == player.ObjectiveUnitId && x.ObjectiveState == "OPERATING"))
                        return Hold("HOLD_OBJECTIVE_OPPORTUNITY_SOURCE_REQUIRED");
                    Add(player.UserId, "objective:" + player.ObjectiveUnitId, "O:" + cause, AEDV02PressureChannel.Objective, previous.Tick, sample.Tick, cause.ToString("D"));
                }
                else if (pilotOpportunities && player.ObjectiveState == "NONE" &&
                    (player.LifeState == "DOWNED" || player.IsCaught || player.SlowUntilTick > previous.Tick ||
                        previous.Minions.Any(x => x.TargetUserId == player.UserId && x.State is "TRACK" or "HARASS")))
                {
                    var causal = impacts.Where(x => x.Value.UserId == player.UserId && x.Value.Tick <= previous.Tick && x.Value.ObjectiveUnitId.Length != 0)
                        .OrderByDescending(x => x.Value.Tick).FirstOrDefault();
                    bool constrained = causal.Value != null && (player.LifeState == "DOWNED" || player.IsCaught || player.SlowUntilTick > previous.Tick ||
                        causal.Value.Kind == "MINION_CORE_DROP" && previous.Minions.Any(x => x.NetworkId == causal.Value.EntityId &&
                            x.TargetUserId == player.UserId && x.State is "TRACK" or "HARASS"));
                    if (constrained && causal.Value is { } verifiedCause && !previous.Players.Any(x => x.ObjectiveState == "OPERATING" && x.ObjectiveUnitId == verifiedCause.ObjectiveUnitId))
                        Add(player.UserId, "objective:" + verifiedCause.ObjectiveUnitId, "O:" + causal.Key, AEDV02PressureChannel.Objective,
                            previous.Tick, sample.Tick, causal.Key.ToString("D"));
                }
                if (player.RequiredToolId != 0)
                {
                    if (!pilotOpportunities || !Guid.TryParse(player.ResourceCauseEventId, out var cause) || !impacts.TryGetValue(cause, out var impact) ||
                        impact.UserId != player.UserId || impact.Kind != "MINION_TOOL_LOSS" || impact.ToolId != player.RequiredToolId ||
                        previous.Tick < impact.Tick || previous.Tick > impact.UntilTick) return Hold("HOLD_RESOURCE_NEED_CONTRACT_REQUIRED");
                }
                if (pilotOpportunities)
                    foreach (var cause in impacts.Where(x => x.Value.UserId == player.UserId && x.Value.Kind == "MINION_TOOL_LOSS" &&
                        x.Value.Tick <= previous.Tick && previous.Tick <= x.Value.UntilTick))
                    {
                        var access = UsableSupply(previous, player, cause.Value.ToolId);
                        if (!access.HasValue) return Hold("HOLD_RESOURCE_ACCESS_UNVERIFIED");
                        if (!access.Value) Add(player.UserId, "loss:" + cause.Key, "R:" + cause.Key, AEDV02PressureChannel.Resource,
                            previous.Tick, sample.Tick, cause.Key.ToString("D"));
                    }
            }
            foreach (var player in previous.Players.Where(x => x.LifeState == "DOWNED" && !x.IsReviving))
            {
                var down = accepted.Where(x => x.MatchId == matchId && x.UserId == player.UserId && x.EventType == "PLAYER_DOWNED" &&
                    x.EventSequence < frames[i - 1].Event.EventSequence).OrderByDescending(x => x.EventSequence).First();
                var access = UsableSupply(previous, player, 3);
                if (!access.HasValue) return Hold("HOLD_RESOURCE_ACCESS_UNVERIFIED");
                if (!access.Value) Add(player.UserId, "revive:" + down.Id, "R:revive", AEDV02PressureChannel.Resource, previous.Tick, sample.Tick);
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
        var cabinetClues = new List<Guid>();
        foreach (var occurrence in sourceRange.Where(x => x.EventType == "AED_V02_CABINET_CLUE"))
        {
            var clue = JsonSerializer.Deserialize<AEDV02CabinetClueSource>(occurrence.ValueJson["data"]["clue"].ToJson());
            if (clue == null || !clue.IsValid() || clue.SceneManifestFingerprint != content.Fingerprint ||
                clue.Tick != occurrence.ValueJson["context"]["authorityTick"].ToInt64() || !users.Contains(clue.UserId) ||
                manifest.MaximumVisionRange <= 0 || clue.ObservedDistance > manifest.MaximumVisionRange * AEDV02PilotPolicy.Sensory(0) ||
                !manifest.Markers.Any(x => x.Id == clue.SceneMarkerId && x.Kind == "CABINET" && x.HideSpotId == clue.HideSpotId))
                return Hold("HOLD_CABINET_CLUE_SOURCE_INVALID");
            var cabinet = manifest.Markers.Single(x => x.Id == clue.SceneMarkerId);
            var position = new[] { clue.PlayerX, clue.PlayerY, clue.PlayerZ };
            var distanceSquared = Enumerable.Range(0, 3).Sum(i => Math.Pow(Math.Max(0,
                Math.Max(cabinet.InteractionBounds[i] - position[i], position[i] - cabinet.InteractionBounds[i + 3])), 2));
            if (manifest.MaximumCabinetInteractionDistance <= 0 || distanceSquared > Math.Pow(manifest.MaximumCabinetInteractionDistance, 2))
                return Hold("HOLD_CABINET_CLUE_SOURCE_INVALID");
            var before = frames.LastOrDefault(x => x.Sample.Tick < clue.Tick);
            var after = frames.FirstOrDefault(x => x.Sample.Tick >= clue.Tick);
            if (before.Sample == null || after.Sample == null) continue;
            var previousPlayer = before.Sample.Players.Single(x => x.UserId == clue.UserId);
            var hiddenPlayer = after.Sample.Players.Single(x => x.UserId == clue.UserId);
            if (previousPlayer.LifeState != "ALIVE" || previousPlayer.IsCaught || previousPlayer.HideSpotId != "0" ||
                hiddenPlayer.LifeState != "ALIVE" || hiddenPlayer.HideSpotId != clue.HideSpotId ||
                occurrence.ValueJson["context"]["phase"].AsString != before.Sample.PhaseName || after.Sample.PhaseName != before.Sample.PhaseName ||
                !manifest.Markers.Any(x => x.Id == clue.SceneMarkerId && x.Zone == after.Sample.Zone) ||
                !before.Sample.Stalkers.Any(x => x.NetworkId == clue.StalkerNetworkId) || !after.Sample.Stalkers.Any(x => x.NetworkId == clue.StalkerNetworkId))
                return Hold("HOLD_CABINET_CLUE_LINEAGE_MISMATCH");
            cabinetClues.Add(occurrence.Id);
        }
        double now = request.EndTick / (double)tickRate;
        if (pilotOpportunities)
            foreach (var list in facts.Values)
            {
                var shortScarcity = list.Where(x => x.Channel == AEDV02PressureChannel.Resource).GroupBy(x => x.EpisodeId)
                    .Where(g => g.Sum(x => x.EndedSeconds - x.StartedSeconds) < AEDV02PilotOpportunities.ScarcityMinimumSeconds).Select(g => g.Key).ToHashSet();
                list.RemoveAll(x => x.Channel == AEDV02PressureChannel.Resource && shortScarcity.Contains(x.EpisodeId));
            }
        var pressure = facts.ToDictionary(x => x.Key, x => AEDV02PressureNormalizer.Project(matchId, x.Key, now,
            request.StartTick / (double)tickRate, Enumerable.Repeat(now, 5).ToArray(), x.Value));
        if (pressure.Values.Any(x => !x.Complete && (!request.AllowEmergencyWindow || x.Reason != "HOLD_INSUFFICIENT_EVIDENCE"))) return Hold("HOLD_INSUFFICIENT_EVIDENCE");
        bool fullPressure = pilotOpportunities && pressure.Values.All(x => x.Complete);
        var team = AEDV02PilotOpportunities.TeamPressure(pressure.Values.ToArray(), last.Sample.Players.Count(x => x.LifeState != "ALIVE"),
            users.Count(user => last.Sample.Stalkers.Any(x => x.TargetUserId == user && x.State is "CHASE" or "ATTACK") &&
                last.Sample.Minions.Any(x => x.TargetUserId == user && x.State is "TRACK" or "HARASS")));
        var safe = last.Sample.Stalkers.All(x => x.State is "PATROL" or "SEARCH") &&
            last.Sample.Minions.All(x => x.State is not ("TRACK" or "HARASS")) && last.Sample.Players.All(x => x.LifeState == "ALIVE" && !x.IsCaught && !x.IsReviving && x.ObjectiveState == "NONE");
        var fingerprint = AEDV02Plan.Hash(VerificationVersion + "|" + AEDV02PilotPolicy.Version + "|" + AEDV02WorldSample.SourceVersion + "|" + roster + "|" + content.Fingerprint + "|" +
            string.Join('|', sourceRange.Select(x => x.Id + ":" + x.EventSequence + ":" + x.SemanticFingerprint + ":" + x.ValueJson.ToJson())));
        var needs = new List<AEDV02ResourceNeed>();
        if (pilotOpportunities)
            foreach (var player in last.Sample.Players)
            {
                if (player.LifeState == "DOWNED" && !player.IsReviving && UsableSupply(last.Sample, player, 3) == false)
                {
                    var down = accepted.Where(x => x.UserId == player.UserId && x.EventType == "PLAYER_DOWNED" && x.EventSequence < last.Event.EventSequence)
                        .OrderByDescending(x => x.EventSequence).FirstOrDefault();
                    if (down != null && facts[player.UserId].Any(x => x.Channel == AEDV02PressureChannel.Resource && x.EpisodeId == "revive:" + down.Id))
                        needs.Add(new AEDV02ResourceNeed { UserId = player.UserId, CauseEventId = down.Id, ToolId = 3, Downed = true });
                }
                if (player.LifeState != "ALIVE") continue;
                foreach (var impact in impacts.Where(x => x.Value.UserId == player.UserId && x.Value.Kind == "MINION_TOOL_LOSS" && x.Value.UntilTick >= last.Sample.Tick &&
                    facts[player.UserId].Any(f => f.Channel == AEDV02PressureChannel.Resource && f.EpisodeId == "loss:" + x.Key)))
                    if (UsableSupply(last.Sample, player, impact.Value.ToolId) == false)
                        needs.Add(new AEDV02ResourceNeed { UserId = player.UserId, CauseEventId = impact.Key, ToolId = impact.Value.ToolId });
            }
        bool supportSafe = last.Sample.Stalkers.All(x => x.State is "PATROL" or "SEARCH") && last.Sample.Minions.All(x => x.State is not ("TRACK" or "HARASS")) &&
            last.Sample.Players.Any(x => x.LifeState == "ALIVE") && last.Sample.Players.All(x => !x.IsCaught && !x.IsReviving && x.ObjectiveState == "NONE");
        var result = new AEDV02SourceResult(fingerprint, request.PhaseOrdinal, request.StartTick, request.EndTick, roster,
            content.Fingerprint, pressure.ToDictionary(x => x.Key, x => x.Value.Score),
            pressure.ToDictionary(x => x.Key, x => x.Value.Components.ToArray()),
            facts.ToDictionary(x => x.Key, x => x.Value.Where(f => f.Channel == AEDV02PressureChannel.Stalker)
                .OrderByDescending(f => f.EndedSeconds).Select(f => f.EpisodeId).FirstOrDefault() ?? string.Empty),
            accepted.Where(x => x.MatchId == matchId && x.UserId.HasValue && x.EventType is "PLAYER_DOWNED" or "PLAYER_REVIVED" &&
                x.Ts >= last.Event.Ts.AddSeconds(-AEDV02PilotPolicy.DownRecoverySeconds) && x.EventSequence <= last.Event.EventSequence)
                .Select(x => x.UserId!.Value).Distinct().ToArray(), safe, last.Sample.LogicalPhase, last.Sample.Zone, last.Sample.PhaseName,
            last.Sample.PlanRevision, last.Sample.PlanFingerprint, last.Event.Ts, fullPressure,
            fullPressure ? [] : pilotOpportunities ? ["MinimumObservation"] : ["ResourceNeedOutsideRevive", "ObjectiveThreatInterruption"], cabinetClues.ToArray(),
            fullPressure && team.Complete ? "Verified" : "Unsupported", last.Sample.Supply, last.Sample.Minions.Count(x => x.Zone == 3),
            pilotOpportunities ? AEDV02PilotOpportunities.Version : string.Empty, pilotOpportunities ? team.Tier : -1, pilotOpportunities ? team.Severity : 0,
            pilotOpportunities ? ResolveOpportunities(accepted, frames, manifest) : [], AEDV02PilotOpportunities.ComparisonContext(content.Fingerprint, users.Length), needs.ToArray(), supportSafe);
        if (!await db.AEDV02VerifiedSources.AnyAsync(x => x.Fingerprint == fingerprint, ct))
        {
            db.AEDV02VerifiedSources.Add(new AEDV02VerifiedSource { Fingerprint = fingerprint, MatchId = matchId,
                PhaseOrdinal = request.PhaseOrdinal, StartTick = request.StartTick, EndTick = request.EndTick,
                EndEventSequence = last.Event.EventSequence, RosterIdentity = roster, SceneManifestFingerprint = content.Fingerprint,
                ReconstructedJson = JsonSerializer.Serialize(result), VerifiedAtUtc = clock.GetUtcNow().UtcDateTime, SourceEndedAtUtc = last.Event.Ts });
            await db.SaveChangesAsync(ct);
        }
        return ServiceResult<AEDV02SourceResult>.Success(result, "AED v0.2 source reconstructed");

        bool? Reach(double[] from, double[] to, AEDV02DoorSource[] doors)
        {
            string identity = JsonSerializer.Serialize(new { from, to, doors });
            if (!geometryResults.TryGetValue(identity, out var reachable))
                geometryResults.Add(identity, reachable = manifest.TryReachableGeometry(from, to, doors, out bool found) ? found : null);
            return reachable;
        }
        bool? UsableSupply(AEDV02WorldSample sample, AEDV02PlayerSource need, int toolId)
        {
            var candidates = sample.Tools.Where(x => x.ToolId == toolId && x.RemainingCharges != 0 && x.CarrierMinionId.Length == 0 && x.Location is "World" or "Player").ToArray();
            if (candidates.Length == 0) return false;
            if (candidates.Any(x => x.Location == "Player" && x.OwnerUserId == need.UserId && need.LifeState == "ALIVE" && !need.IsCaught)) return true;
            var helpers = sample.Players.Where(x => x.LifeState == "ALIVE" && !x.IsCaught && x.UserId != need.UserId).ToArray();
            if (need.LifeState == "ALIVE" && !need.IsCaught) helpers = helpers.Append(need).ToArray();
            bool unknown = false;
            foreach (var helper in helpers) foreach (var tool in candidates.Where(x => x.Location == "World" || x.OwnerUserId == helper.UserId))
            {
                var toTool = tool.Location == "Player" ? true : Reach([helper.X, helper.Y, helper.Z], [tool.X, tool.Y, tool.Z], sample.Doors);
                var toNeed = Reach([tool.X, tool.Y, tool.Z], [need.X, need.Y, need.Z], sample.Doors);
                if (toTool == true && toNeed == true) return true;
                unknown |= !toTool.HasValue || !toNeed.HasValue;
            }
            return unknown ? null : false;
        }
    }
    internal static bool VerifySupply(AEDV02WorldSample world, AEDV02SceneManifest manifest, AEDV02Plan plan,
        int initialBudget, IEnumerable<TelemetryEventDocument> accepted, long sequence, ISet<string>? supportInstances = null)
    {
        var supply = world.Supply;
        if (supply == null || !supply.IsValid() || supply.InitialOptionalBudget != initialBudget ||
            supply.OptionalBudget != plan.Get(AEDV02Key.OptionalToolBudget) || supply.ReservationGrade != plan.Get(AEDV02Key.ResourceGrade)) return false;
        var spawns = accepted.Where(x => x.EventSequence < sequence && x.EventType == "TEAM_TOOL_LIFECYCLE")
            .Select(x => x.ValueJson["data"].AsBsonDocument)
            .Where(x => x["action"] == "SPAWN" && x.TryGetValue("origin", out var origin) && origin == "WorldSpawn" &&
                supportInstances?.Contains(x["instanceId"].AsString) != true).ToArray();
        int materialized = 0;
        for (int zone = 1; zone <= 3; zone++)
        {
            var actual = spawns.Where(x => x["zone"].ToInt32() == zone).Select(x => x["toolId"].ToInt32()).ToArray();
            var reservations = supply.Reservations.Where(x => x.Zone == zone).ToArray();
            bool spawned = supply.SpawnedZones.Contains(zone);
            if (spawned && reservations.Length != 0 || !spawned && actual.Length != 0) return false;
            var ids = spawned ? actual : reservations.Select(x => x.ToolId).ToArray();
            int limit = 5 + Math.Min(1, Math.Max(0, (spawned ? initialBudget : supply.OptionalBudget) - zone + 1));
            if (ids.Length < 5 || ids.Length > limit || !new[] { 1, 2, 3, 4, 6 }.All(ids.Contains)) return false;
            if (spawned) materialized += actual.Length - 5;
            foreach (var item in reservations)
            {
                var marker = manifest.Markers.SingleOrDefault(x => x.Id == item.MarkerId && x.Zone == zone && x.Kind == "TOOL" && x.AllowedToolIds.Contains(item.ToolId));
                if (marker == null || !AEDV02ResourcePlacement.AllowsRole(supply.ReservationGrade, marker.ResourceRole)) return false;
                if (supply.ReservationGrade != 0)
                {
                    var entry = manifest.Markers.SingleOrDefault(x => x.Id == "Z" + zone + "_ENTRY");
                    if (entry == null || item.EntryPath.Length == 0 || !manifest.TryValidatePlacementPath(item.EntryPath, world.Doors, out _) ||
                        Enumerable.Range(0, 3).Sum(i => Math.Pow(item.EntryPath[0][i] - new[] { entry.X, entry.Y, entry.Z }[i], 2)) > 4 ||
                        Enumerable.Range(0, 3).Sum(i => Math.Pow(item.EntryPath[^1][i] - new[] { marker.X, marker.Y, marker.Z }[i], 2)) > 2.25) return false;
                }
            }
        }
        return materialized == supply.OptionalMaterialized;
    }
    private static ServiceResult<AEDV02SourceResult> Hold(string reason) => ServiceResult<AEDV02SourceResult>.Failure(reason, ErrorCodes.ScenarioApplyConflict);
}
