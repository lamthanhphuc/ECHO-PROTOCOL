using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data.Telemetry;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed partial class AEDV02SourceVerifier
{
    internal static bool TryReadImpacts(IReadOnlyList<TelemetryEventDocument> accepted,
        List<(TelemetryEventDocument Event, AEDV02WorldSample Sample)> frames, out Dictionary<Guid, AEDV02CausalImpact> impacts)
    {
        impacts = new();
        var latest = frames[^1].Sample;
        var history = accepted.Where(x => x.EventType == "AED_V02_WORLD_SAMPLE" && x.EventSequence <= frames[^1].Event.EventSequence)
            .Select(x => (Event: x, World: JsonSerializer.Deserialize<AEDV02WorldSample>(x.ValueJson["data"]["world"].ToJson())!))
            .Where(x => x.World?.IsValid() == true && x.World.PhaseOrdinal == latest.PhaseOrdinal && x.World.RosterIdentity == latest.RosterIdentity &&
                x.World.SceneManifestFingerprint == latest.SceneManifestFingerprint && x.World.EvidenceExclusion.Length == 0).ToArray();
        var referenced = frames.SelectMany(x => x.Sample.Players).SelectMany(x => new[] { x.ObjectiveCauseEventId, x.ResourceCauseEventId })
            .Where(x => x.Length != 0).ToHashSet();
        foreach (var item in accepted.Where(x => x.EventType == "AED_V02_CAUSAL_IMPACT" &&
            (referenced.Contains(x.Id.ToString("D")) || x.EventSequence >= frames[0].Event.EventSequence && x.EventSequence <= frames[^1].Event.EventSequence)))
        {
            AEDV02CausalImpact? impact;
            try { impact = JsonSerializer.Deserialize<AEDV02CausalImpact>(item.ValueJson["data"]["impact"].ToJson()); }
            catch (JsonException) { return false; }
            if (item.SchemaVersion != "1.3" || impact == null || !impact.IsValid() || impact.Tick != item.ValueJson["context"]["authorityTick"].ToInt64() ||
                item.ValueJson["context"]["phase"] != latest.PhaseName || !latest.Players.Any(x => x.UserId == impact.UserId)) return false;
            var before = history.Where(x => x.Event.EventSequence < item.EventSequence && x.World.Tick <= impact.Tick).OrderByDescending(x => x.World.Tick).FirstOrDefault();
            var after = history.Where(x => x.Event.EventSequence > item.EventSequence && x.World.Tick >= impact.Tick).OrderBy(x => x.World.Tick).FirstOrDefault();
            // An unobserved onset is not a verified cause; a later retry can reconcile it once the after sample arrives.
            if (before.World == null || after.World == null || impact.Tick - before.World.Tick > latest.TickRate * 2 ||
                after.World.Tick - impact.Tick > latest.TickRate * 2) return false;
            var victim = before.World.Players.SingleOrDefault(x => x.UserId == impact.UserId);
            var consequence = after.World.Players.SingleOrDefault(x => x.UserId == impact.UserId);
            if (victim == null || consequence == null || victim.LifeState != "ALIVE" ||
                impact.ObjectiveUnitId.Length != 0 && (victim.ObjectiveUnitId != impact.ObjectiveUnitId || victim.ObjectiveState != "OPERATING")) return false;
            var actors = impact.Kind == "STALKER_DOWN" ? before.World.Stalkers : before.World.Minions;
            if (!actors.Any(x => x.NetworkId == impact.EntityId && x.TargetUserId == impact.UserId && x.State is "CHASE" or "ATTACK" or "TRACK" or "HARASS")) return false;
            if (impact.Kind == "STALKER_DOWN" && !accepted.Any(x => x.EventType == "PLAYER_DOWNED" && x.UserId == impact.UserId &&
                x.ValueJson["context"]["authorityTick"].ToInt64() == impact.Tick && x.EventSequence < item.EventSequence)) return false;
            if (impact.Kind == "MINION_SLOW" && (impact.UntilTick <= impact.Tick || consequence.SlowUntilTick != impact.UntilTick)) return false;
            if (impact.Kind == "MINION_CORE_DROP" && (!victim.ObjectiveUnitId.StartsWith("core:", StringComparison.Ordinal) ||
                consequence.ObjectiveState == "OPERATING" && consequence.ObjectiveUnitId == victim.ObjectiveUnitId)) return false;
            if (impact.Kind == "MINION_TOOL_LOSS" && (victim.HeldToolInstanceId != impact.ToolInstanceId ||
                !after.World.Tools.Any(x => x.InstanceId == impact.ToolInstanceId && x.ToolId == impact.ToolId && x.CarrierMinionId == impact.EntityId) ||
                !accepted.Any(x => x.EventType == "AED_V02_TOOL_CARRIER" && x.EventSequence < item.EventSequence &&
                    x.ValueJson["data"]["carrier"]["ToolInstanceId"] == impact.ToolInstanceId &&
                    x.ValueJson["data"]["carrier"]["MinionNetworkId"] == impact.EntityId && x.ValueJson["data"]["carrier"]["Action"] == "STEAL"))) return false;
            impacts.Add(item.Id, impact);
        }
        if (frames.All(x => x.Sample.OpportunityContractVersion == AEDV02PilotOpportunities.Version))
            for (int i = 1; i < frames.Count; i++)
                foreach (var player in frames[i].Sample.Players)
                {
                    var previous = frames[i - 1].Sample.Players.Single(x => x.UserId == player.UserId);
                    var interval = impacts.Values.Where(x => x.UserId == player.UserId && x.Tick >= frames[i - 1].Sample.Tick && x.Tick <= frames[i].Sample.Tick).ToArray();
                    if (player.SlowUntilTick > frames[i].Sample.Tick && player.SlowUntilTick > previous.SlowUntilTick &&
                        !interval.Any(x => x.Kind == "MINION_SLOW" && x.UntilTick == player.SlowUntilTick)) return false;
                    if (previous.LifeState == "ALIVE" && player.LifeState == "DOWNED" &&
                        !interval.Any(x => x.Kind == "STALKER_DOWN")) return false;
                    if (previous.HeldToolInstanceId.Length != 0 && frames[i].Sample.Tools.Any(x => x.InstanceId == previous.HeldToolInstanceId && x.CarrierMinionId.Length != 0) &&
                        !interval.Any(x => x.Kind == "MINION_TOOL_LOSS" && x.ToolInstanceId == previous.HeldToolInstanceId)) return false;
                }
        var verifiedImpacts = impacts;
        return referenced.All(x => Guid.TryParse(x, out var id) && verifiedImpacts.ContainsKey(id));
    }

    internal static AEDV02ResolvedOpportunity[] ResolveOpportunities(IReadOnlyList<TelemetryEventDocument> accepted,
        List<(TelemetryEventDocument Event, AEDV02WorldSample Sample)> frames, AEDV02SceneManifest? manifest = null)
    {
        var results = new List<AEDV02ResolvedOpportunity>();
        if (frames.Count < 2 || !TryReadImpacts(accepted, frames, out var impacts)) return [];
        var noiseOutcomes = Array.Empty<AEDV02ResolvedOpportunity>();
        if (manifest != null && !TryResolveNoiseMakerOpportunities(accepted, frames, manifest, out noiseOutcomes)) return [];
        results.AddRange(noiseOutcomes);
        var stabilizerOutcomes = Array.Empty<AEDV02ResolvedOpportunity>();
        if (manifest != null && !TryResolveStabilizerOpportunities(accepted, frames, manifest, out stabilizerOutcomes, out _)) return [];
        results.AddRange(stabilizerOutcomes);
        var jammerOutcomes = Array.Empty<AEDV02ResolvedOpportunity>();
        if (manifest != null && !TryResolveJammerOpportunities(accepted, frames, manifest, out jammerOutcomes)) return [];
        results.AddRange(jammerOutcomes);
        var scannerOutcomes = Array.Empty<AEDV02ResolvedOpportunity>();
        if (manifest != null && !TryResolveScannerOpportunities(accepted, frames, manifest, out scannerOutcomes)) return [];
        results.AddRange(scannerOutcomes);
        var samples = frames.Select(x => x.Sample).ToArray();
        int rate = samples[^1].TickRate;
        foreach (bool minion in new[] { false, true })
        {
            var episodes = samples.SelectMany(s => (minion ? s.Minions : s.Stalkers)
                .Where(x => x.TargetUserId.HasValue && x.EpisodeId.Length != 0 && x.State is "CHASE" or "ATTACK" or "HARASS")
                .Select(x => (Sample: s, Threat: x))).GroupBy(x => (x.Threat.NetworkId, x.Threat.EpisodeId, User: x.Threat.TargetUserId!.Value));
            foreach (var episode in episodes)
            {
                long start = episode.Min(x => x.Sample.Tick), end = episode.Max(x => x.Sample.Tick);
                double exposure = episode.Sum(x => samples.FirstOrDefault(s => s.Tick > x.Sample.Tick) is { } next
                    ? (next.Tick - x.Sample.Tick) / (double)rate : 0);
                if (exposure < AEDV02PilotOpportunities.MinimumExposureSeconds) continue;
                var onset = samples.LastOrDefault(x => x.Tick < start);
                bool observedOnset = onset != null && (minion ? onset.Minions : onset.Stalkers).Any(x => x.NetworkId == episode.Key.NetworkId &&
                    (x.TargetUserId != episode.Key.User || x.State is "IDLE" or "FLEE" or "PATROL" or "SEARCH"));
                var down = accepted.FirstOrDefault(x => x.EventType == "PLAYER_DOWNED" && x.UserId == episode.Key.User &&
                    x.ValueJson["context"]["authorityTick"].ToInt64() >= start && x.ValueJson["context"]["authorityTick"].ToInt64() <= end + rate &&
                    (!minion && impacts.Values.Any(impact => impact.Kind == "STALKER_DOWN" && impact.EntityId == episode.Key.NetworkId &&
                        impact.UserId == episode.Key.User && impact.Tick == x.ValueJson["context"]["authorityTick"].ToInt64())));
                var minionImpact = minion ? impacts.Values.Where(x => x.EntityId == episode.Key.NetworkId && x.UserId == episode.Key.User &&
                    x.Tick >= start && x.Tick <= end + rate && x.Kind is "MINION_SLOW" or "MINION_CORE_DROP" or "MINION_TOOL_LOSS")
                    .OrderBy(x => x.Tick).FirstOrDefault() : null;
                var resolved = samples.Where(x => x.Tick > end && x.Tick <= end + rate * AEDV02PilotOpportunities.SafeResolutionSeconds).ToArray();
                bool success = observedOnset && resolved.Length >= 2 && resolved[^1].Tick >= end + rate * AEDV02PilotOpportunities.SafeResolutionSeconds &&
                    resolved.All(s => IsThreatFree(s, episode.Key.User) &&
                        (minion ? s.Minions : s.Stalkers).Any(x => x.NetworkId == episode.Key.NetworkId &&
                            x.State is "IDLE" or "FLEE" or "DYING" or "PATROL" or "SEARCH"));
                results.Add(new AEDV02ResolvedOpportunity { Id = (minion ? "Minion:" : "Stalker:") + episode.Key.NetworkId + ":" + episode.Key.EpisodeId,
                    UserId = episode.Key.User, Axis = minion ? "Minion" : "Stalker", StartTick = start,
                    EndTick = Math.Max(start + 1, down != null ? down.ValueJson["context"]["authorityTick"].ToInt64() :
                        minionImpact != null ? minionImpact.Tick : success ? resolved[^1].Tick : samples[^1].Tick),
                    Resolution = !observedOnset ? "Censored" : down != null || minionImpact != null ? "Failure" : success ? "Success" : "Censored" });
            }
        }
        var range = accepted.Where(x => x.EventSequence >= frames[0].Event.EventSequence && x.EventSequence <= frames[^1].Event.EventSequence)
            .OrderBy(x => x.EventSequence).ToArray();
        foreach (var pickup in range.Where(x => x.EventType == "CORE_PICKED_UP" && x.UserId.HasValue))
        {
            var core = pickup.ValueJson["data"]["coreId"].AsString;
            var unit = "core:" + core;
            long start = pickup.ValueJson["context"]["authorityTick"].ToInt64();
            var end = range.FirstOrDefault(x => x.EventSequence > pickup.EventSequence && x.UserId == pickup.UserId &&
                x.EventType is "CORE_PLACED" or "CORE_DROPPED" && x.ValueJson["data"]["coreId"] == core);
            long finish = end?.ValueJson["context"]["authorityTick"].ToInt64() ?? samples[^1].Tick;
            var carrying = samples.Where(x => x.Tick >= start && x.Tick < finish &&
                x.Players.Any(p => p.UserId == pickup.UserId && p.ObjectiveUnitId == unit && p.ObjectiveState == "OPERATING")).ToArray();
            double exposure = carrying.Sum(x => samples.FirstOrDefault(s => s.Tick > x.Tick) is { } next
                ? (Math.Min(next.Tick, finish) - x.Tick) / (double)rate : 0);
            if (exposure < AEDV02PilotOpportunities.MinimumExposureSeconds) continue;
            var consequence = impacts.Values.FirstOrDefault(x => x.UserId == pickup.UserId && x.ObjectiveUnitId == unit &&
                x.Tick >= start && x.Tick <= finish && x.Kind is "STALKER_DOWN" or "MINION_CORE_DROP");
            results.Add(new AEDV02ResolvedOpportunity { Id = "Objective:" + core + ":" + pickup.Id, Axis = "Objective", UserId = pickup.UserId!.Value,
                StartTick = start, EndTick = Math.Max(start + 1, finish), Resolution = end?.EventType == "CORE_PLACED" ? "Success" :
                    end != null && consequence != null ? "Failure" : "Censored" });
        }
        foreach (var attempt in samples.SelectMany(s => s.Players.Where(p => p.IsReviving && p.ReviverUserId.HasValue)
            .Select(p => (Sample: s, Victim: p))).GroupBy(x => (Victim: x.Victim.UserId, Helper: x.Victim.ReviverUserId!.Value)))
        {
            // Each actual Down creates one opportunity per helper, regardless of retries or overlapping source windows.
            foreach (var down in accepted.Where(x => x.EventType == "PLAYER_DOWNED" && x.UserId == attempt.Key.Victim &&
                x.EventSequence <= frames[^1].Event.EventSequence))
            {
                long downTick = down.ValueJson["context"]["authorityTick"].ToInt64();
                var resolution = range.FirstOrDefault(x => x.EventSequence > down.EventSequence && x.UserId == attempt.Key.Victim &&
                    x.EventType is "PLAYER_REVIVED" or "PLAYER_ELIMINATED");
                long endTick = resolution?.ValueJson["context"]["authorityTick"].ToInt64() ?? samples[^1].Tick;
                var observed = attempt.Where(x => x.Sample.Tick >= downTick && x.Sample.Tick < endTick &&
                    x.Sample.Players.Any(p => p.UserId == attempt.Key.Helper && p.LifeState == "ALIVE" && !p.IsCaught)).ToArray();
                if (observed.Length < 2 || observed[^1].Sample.Tick - observed[0].Sample.Tick < rate) continue;
                bool success = resolution?.EventType == "PLAYER_REVIVED" &&
                    resolution.ValueJson["data"]["reviverPlayerId"].AsString == attempt.Key.Helper.ToString("D");
                bool failure = resolution?.EventType == "PLAYER_ELIMINATED" && endTick - observed[^1].Sample.Tick <= 2 * rate;
                results.Add(new AEDV02ResolvedOpportunity { Id = "Teamwork:" + down.Id + ":" + attempt.Key.Helper.ToString("D"),
                    Axis = "Teamwork", UserId = attempt.Key.Helper, StartTick = observed[0].Sample.Tick,
                    EndTick = Math.Max(observed[0].Sample.Tick + 1, endTick), Resolution = success ? "Success" : failure ? "Failure" : "Censored" });
                var first = observed[0].Sample;
                var helper = first.Players.Single(x => x.UserId == attempt.Key.Helper);
                var kit = first.Tools.SingleOrDefault(x => x.Location == "Player" && x.OwnerUserId == helper.UserId &&
                    x.ToolId == 3 && x.RemainingCharges != 0 && x.CarrierMinionId.Length == 0);
                var consume = resolution == null ? null : range.LastOrDefault(x => x.EventType == "TEAM_TOOL_LIFECYCLE" &&
                    x.EventSequence > down.EventSequence && x.EventSequence < resolution.EventSequence &&
                    x.ValueJson["data"]["action"] == "CONSUME" && x.ValueJson["data"]["toolId"].ToInt32() == 3 &&
                    x.ValueJson["data"]["instanceId"] == kit?.InstanceId);
                if (kit != null && manifest?.HasReachableGeometry(new[] { helper.X, helper.Y, helper.Z },
                    new[] { observed[0].Victim.X, observed[0].Victim.Y, observed[0].Victim.Z }, first.Doors) == true)
                    results.Add(new AEDV02ResolvedOpportunity { Id = "Tool:3:" + down.Id + ":" + helper.UserId.ToString("D"),
                        Axis = "Tool", ToolId = 3, UserId = helper.UserId, StartTick = observed[0].Sample.Tick,
                        EndTick = Math.Max(observed[0].Sample.Tick + 1, endTick),
                        Resolution = success && consume != null && resolution!.ValueJson["data"].AsBsonDocument.GetValue("usedFirstAidKit", false).ToBoolean()
                            ? "Success" : failure ? "Failure" : "Censored" });
            }
        }
        return results.ToArray();
    }

    private static bool IsThreatFree(AEDV02WorldSample sample, Guid user)
    {
        var player = sample.Players.Single(x => x.UserId == user);
        return player.LifeState == "ALIVE" && !player.IsCaught && player.SlowUntilTick <= sample.Tick &&
            !sample.Stalkers.Any(x => x.TargetUserId == user && x.State is "DETECTION" or "CHASE" or "ATTACK" or "SPECIAL") &&
            !sample.Minions.Any(x => x.TargetUserId == user && x.State is "TRACK" or "HARASS");
    }
}
