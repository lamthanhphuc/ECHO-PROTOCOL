using System.Text.RegularExpressions;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data.Telemetry;
using MongoDB.Bson;

namespace EchoProtocol.Api.Services;

public sealed partial class AEDV02SourceVerifier
{
    private static bool TryResolveNoiseMakerOpportunities(IReadOnlyList<TelemetryEventDocument> accepted,
        List<(TelemetryEventDocument Event, AEDV02WorldSample Sample)> frames, AEDV02SceneManifest manifest,
        out AEDV02ResolvedOpportunity[] opportunities)
    {
        opportunities = [];
        if (frames.Count < 2) return false;
        var first = frames[0]; var last = frames[^1];
        accepted = accepted.Where(x => x.MatchId == last.Event.MatchId && x.EventSequence <= last.Event.EventSequence).ToArray();
        var actions = accepted.Where(x => x.MatchId == last.Event.MatchId && x.EventType == "TEAM_TOOL_USED" && x.SchemaVersion == "1.1" &&
            x.UserId.HasValue && x.ValueJson["data"]["toolType"] == "NOISE_MAKER" && x.EventSequence <= last.Event.EventSequence &&
            x.ValueJson["context"]["phase"] == last.Sample.PhaseName).ToArray();
        var references = frames.SelectMany(frame => frame.Sample.Stalkers.Where(x => x.InvestigationNoiseId.Length != 0)
            .Select(stalker => (Frame: frame, Stalker: stalker))).ToArray();
        var noiseActions = new Dictionary<string, TelemetryEventDocument>();
        foreach (var noiseId in references.Select(x => x.Stalker.InvestigationNoiseId).Distinct())
        {
            var noises = accepted.Where(x => x.MatchId == last.Event.MatchId && x.EventType == "NOISE_EMITTED" && x.SchemaVersion == "1.1" &&
                x.ValueJson["data"]["noiseEventId"] == noiseId).ToArray();
            if (noises.Length != 1) return false;
            var noise = noises[0];
            if (!noise.UserId.HasValue || noise.ValueJson["data"]["noiseType"] != "NOISE_MAKER" ||
                noise.ValueJson["context"]["phase"] != last.Sample.PhaseName) return false;
            // Join the real beacon stream to the accepted activation; pulses never create extra opportunities.
            var identity = Regex.Match(noiseId, "^" + last.Event.MatchId.ToString("N") + @":noise:team-tool:(.+):noise-maker:([0-9]+):NOISE_MAKER:([0-9]+)$");
            if (!identity.Success || !uint.TryParse(identity.Groups[2].Value, out _) || !uint.TryParse(identity.Groups[3].Value, out var pulse) || pulse == 0) return false;
            string occurrence = "player:" + identity.Groups[1].Value + ":tool:" + identity.Groups[2].Value;
            var matching = actions.Where(x => x.UserId == noise.UserId && x.EventSequence < noise.EventSequence && x.Ts <= noise.Ts &&
                x.ValueJson["context"]["occurrenceKey"] == occurrence).ToArray();
            if (matching.Length != 1 || !HasConsumedNoiseMaker(accepted, matching[0], last.Sample.Players.Select(x => x.UserId).ToHashSet())) return false;
            noiseActions.Add(noiseId, matching[0]);
            var position = noise.ValueJson["context"]["position"].AsBsonDocument;
            double nx = position["x"].ToDouble(), ny = position["y"].ToDouble(), nz = position["z"].ToDouble();
            if (!new[] { nx, ny, nz }.All(AEDV02PilotPolicy.Finite)) return false;
            foreach (var observation in references.Where(x => x.Stalker.InvestigationNoiseId == noiseId))
            {
                var world = observation.Frame.Sample; var stalker = observation.Stalker;
                var goal = new[] { stalker.DestinationX, stalker.DestinationY, stalker.DestinationZ };
                if (noise.EventSequence >= observation.Frame.Event.EventSequence || noise.Ts > observation.Frame.Event.Ts ||
                    observation.Frame.Event.Ts - noise.Ts > TimeSpan.FromSeconds(12) ||
                    noise.ValueJson["context"]["authorityTick"].ToInt64() > world.Tick ||
                    Distance(goal[0], goal[1], goal[2], nx, ny, nz) > AEDV02PilotOpportunities.NoiseProjectionRadius + .000001 ||
                    !manifest.HasReachableGeometry(new[] { stalker.X, stalker.Y, stalker.Z }, goal, world.Doors)) return false;
            }
        }
        var outcomes = new Dictionary<string, AEDV02ResolvedOpportunity>();
        if (frames.Any(x => x.Sample.OpportunityContractVersion != AEDV02PilotOpportunities.Version)) return true;
        foreach (var action in actions.Where(x => x.EventSequence >= first.Event.EventSequence && x.EventSequence <= last.Event.EventSequence).OrderBy(x => x.EventSequence))
        {
            long tick = action.ValueJson["context"]["authorityTick"].ToInt64();
            var context = frames.Where(x => x.Sample.Tick <= tick && tick - x.Sample.Tick <= x.Sample.TickRate * AEDV02PilotOpportunities.ToolThreatContextSeconds)
                .SelectMany(x => x.Sample.Stalkers.Where(s => s.TargetUserId == action.UserId && s.State is "CHASE" or "ATTACK")
                    .Select(s => (Frame: x, Stalker: s))).OrderByDescending(x => x.Frame.Sample.Tick).FirstOrDefault();
            if (context.Stalker == null || context.Stalker.EpisodeId.Length == 0) continue;
            if (!HasConsumedNoiseMaker(accepted, action, last.Sample.Players.Select(x => x.UserId).ToHashSet())) return false;
            string id = "Tool:2:" + context.Stalker.NetworkId + ":" + context.Stalker.EpisodeId;
            if (outcomes.ContainsKey(id)) continue;
            var diverted = references.Where(x => x.Stalker.NetworkId == context.Stalker.NetworkId &&
                    noiseActions[x.Stalker.InvestigationNoiseId].Id == action.Id && x.Frame.Sample.Tick > tick)
                .OrderBy(x => x.Frame.Sample.Tick).ToArray();
            bool success = false; long end = last.Sample.Tick;
            foreach (var start in diverted)
            {
                var finish = diverted.FirstOrDefault(x => x.Frame.Sample.Tick - start.Frame.Sample.Tick >=
                    last.Sample.TickRate * AEDV02PilotOpportunities.NoiseDiversionSeconds && x.Frame.Sample.Tick - start.Frame.Sample.Tick <= last.Sample.TickRate * 3);
                if (finish.Stalker == null) continue;
                var goal = finish.Stalker;
                double progress = Distance(start.Stalker.X, start.Stalker.Y, start.Stalker.Z, goal.DestinationX, goal.DestinationY, goal.DestinationZ) -
                    Distance(goal.X, goal.Y, goal.Z, goal.DestinationX, goal.DestinationY, goal.DestinationZ);
                bool continuous = frames.Where(x => x.Sample.Tick >= start.Frame.Sample.Tick && x.Sample.Tick <= finish.Frame.Sample.Tick)
                    .All(x => x.Sample.Stalkers.Any(s => s.NetworkId == goal.NetworkId && s.InvestigationNoiseId.Length != 0 &&
                        noiseActions.TryGetValue(s.InvestigationNoiseId, out var a) && a.Id == action.Id));
                if (continuous && progress >= AEDV02PilotOpportunities.NoiseDiversionMeters)
                { success = true; end = finish.Frame.Sample.Tick; break; }
            }
            outcomes.Add(id, new AEDV02ResolvedOpportunity { Id = id, Axis = "Tool", ToolId = 2, UserId = action.UserId!.Value,
                StartTick = tick, EndTick = Math.Max(tick + 1, end), Resolution = success ? "Success" : "Censored" });
        }
        opportunities = outcomes.Values.ToArray();
        return true;
    }

    private static bool HasConsumedNoiseMaker(IReadOnlyList<TelemetryEventDocument> accepted, TelemetryEventDocument action, HashSet<Guid> users)
    {
        if (!AEDPhase8LifecycleReconcilerV1.TryReconstruct(accepted, action.MatchId, action.EventSequence, users, false, out var tools, out _, out _)) return false;
        var held = tools.SingleOrDefault(x => x.Value.ToolId == 2 && x.Value.Location == "Player" && x.Value.Owner == action.UserId);
        return held.Value != null && held.Value.Charges > 0 && accepted.Any(x => x.MatchId == action.MatchId && x.EventType == "TEAM_TOOL_LIFECYCLE" &&
            x.EventSequence > action.EventSequence && x.ValueJson["context"]["authorityTick"] == action.ValueJson["context"]["authorityTick"] &&
            x.ValueJson["data"]["action"].AsString is "USE" or "CONSUME" && x.ValueJson["data"]["toolId"] == 2 &&
            x.ValueJson["data"]["instanceId"] == held.Key && x.ValueJson["data"]["ownerUserId"] == action.UserId?.ToString("D"));
    }

    private static double Distance(double ax, double ay, double az, double bx, double by, double bz) =>
        Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by) + (az - bz) * (az - bz));
}
