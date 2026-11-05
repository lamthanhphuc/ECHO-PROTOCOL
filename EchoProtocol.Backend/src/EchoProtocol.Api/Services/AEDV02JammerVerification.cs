using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data.Telemetry;

namespace EchoProtocol.Api.Services;

public sealed partial class AEDV02SourceVerifier
{
    private static bool TryResolveJammerOpportunities(IReadOnlyList<TelemetryEventDocument> accepted,
        List<(TelemetryEventDocument Event, AEDV02WorldSample Sample)> frames, AEDV02SceneManifest manifest,
        out AEDV02ResolvedOpportunity[] opportunities)
    {
        opportunities = [];
        if (frames.Count < 2) return false;
        var last = frames[^1];
        accepted = accepted.Where(x => x.MatchId == last.Event.MatchId && x.EventSequence <= last.Event.EventSequence).ToArray();
        if (!TryReadImpacts(accepted, frames, out var impacts)) return false;
        if (frames.SelectMany(x => x.Sample.Jammers).GroupBy(x => x.Occurrence).Any(x => x.Select(j => j.NetworkId).Distinct().Count() != 1)) return false;
        var results = new Dictionary<string, AEDV02ResolvedOpportunity>();
        foreach (var deployment in frames.SelectMany(f => f.Sample.Jammers.Select(j => (Frame: f, Jammer: j))).GroupBy(x => x.Jammer.NetworkId))
        {
            var first = deployment.First(); var jammer = first.Jammer;
            if (jammer.MatchId != last.Event.MatchId) return false;
            if (manifest.JammerBreakSeconds <= 0 || Math.Abs(jammer.BreakSeconds - manifest.JammerBreakSeconds) > .000001) return false;
            if (deployment.Any(x => x.Jammer.MatchId != jammer.MatchId || x.Jammer.PhaseName != jammer.PhaseName || x.Jammer.UserId != jammer.UserId || x.Jammer.Occurrence != jammer.Occurrence ||
                x.Jammer.DeploymentTick != jammer.DeploymentTick || x.Jammer.DoorId != jammer.DoorId ||
                Math.Abs(x.Jammer.BreakSeconds - jammer.BreakSeconds) > .000001 ||
                Distance(x.Jammer.X, x.Jammer.Y, x.Jammer.Z, jammer.X, jammer.Y, jammer.Z) > .001) ||
                jammer.DoorId.Length != 0 && !manifest.DoorIds.Contains(jammer.DoorId)) return false;
            var activations = accepted.Where(x => x.EventType == "TEAM_TOOL_USED" && x.SchemaVersion == "1.1" && x.UserId == jammer.UserId &&
                x.ValueJson["data"]["toolType"] == "DOOR_JAMMER" && x.ValueJson["context"]["occurrenceKey"] == jammer.Occurrence &&
                x.ValueJson["context"]["authorityTick"].ToInt64() == jammer.DeploymentTick &&
                x.ValueJson["context"]["phase"] == jammer.PhaseName && x.EventSequence < first.Frame.Event.EventSequence).ToArray();
            if (activations.Length != 1) return false;
            var action = activations[0];
            if (!AEDPhase8LifecycleReconcilerV1.TryReconstruct(accepted, action.MatchId, action.EventSequence,
                last.Sample.Players.Select(x => x.UserId).ToHashSet(), false, out var tools, out _, out _)) return false;
            var held = tools.SingleOrDefault(x => x.Value.ToolId == 4 && x.Value.Location == "Player" && x.Value.Owner == action.UserId);
            if (held.Value == null || held.Value.Charges is not > 0 || !accepted.Any(x => x.EventType == "TEAM_TOOL_LIFECYCLE" &&
                x.EventSequence > action.EventSequence && x.EventSequence < first.Frame.Event.EventSequence &&
                x.ValueJson["context"]["authorityTick"].ToInt64() == jammer.DeploymentTick &&
                x.ValueJson["data"]["action"].AsString is "USE" or "CONSUME" && x.ValueJson["data"]["toolId"] == 4 &&
                x.ValueJson["data"]["instanceId"] == held.Key && x.ValueJson["data"]["ownerUserId"] == jammer.UserId.ToString("D"))) return false;
            var context = frames.Where(f => f.Sample.Tick <= jammer.DeploymentTick && jammer.DeploymentTick - f.Sample.Tick <=
                    f.Sample.TickRate * AEDV02PilotOpportunities.ToolThreatContextSeconds)
                .SelectMany(f => f.Sample.Stalkers.Where(s => s.TargetUserId == jammer.UserId && s.State is "CHASE" or "ATTACK" && s.EpisodeId.Length != 0))
                .LastOrDefault();
            if (jammer.PhaseName != last.Sample.PhaseName || context == null || frames.Any(f => f.Sample.OpportunityContractVersion != AEDV02PilotOpportunities.Version)) continue;
            string id = "Tool:4:" + context.NetworkId + ":" + context.EpisodeId;
            double delayed = 0; long end = last.Sample.Tick;
            for (int i = 1; i < frames.Count; i++)
            {
                var before = frames[i - 1]; var after = frames[i];
                var a = before.Sample.Stalkers.SingleOrDefault(s => s.NetworkId == context.NetworkId && s.JammerBlockerId == jammer.NetworkId);
                var b = after.Sample.Stalkers.SingleOrDefault(s => s.NetworkId == context.NetworkId && s.JammerBlockerId == jammer.NetworkId);
                if (a == null || b == null) continue;
                double elapsed = (after.Sample.Tick - before.Sample.Tick) / (double)last.Sample.TickRate;
                if (a.TargetUserId != jammer.UserId || b.TargetUserId != jammer.UserId || a.EpisodeId != context.EpisodeId ||
                    b.EpisodeId != context.EpisodeId || a.State is not ("CHASE" or "ATTACK") || b.State is not ("CHASE" or "ATTACK") ||
                    b.JammerProgress <= a.JammerProgress || Math.Abs((b.JammerProgress - a.JammerProgress) * jammer.BreakSeconds - elapsed) > .1)
                    continue;
                delayed += elapsed; end = after.Sample.Tick;
            }
            bool down = impacts.Values.Any(x => x.Kind == "STALKER_DOWN" && x.UserId == jammer.UserId && x.EntityId == context.NetworkId &&
                x.Tick >= jammer.DeploymentTick && x.Tick <= end);
            string resolution = delayed >= AEDV02PilotOpportunities.JammerDelaySeconds ? "Success" : down ? "Failure" : "Censored";
            var outcome = new AEDV02ResolvedOpportunity { Id = id, Axis = "Tool", ToolId = 4, UserId = jammer.UserId,
                StartTick = jammer.DeploymentTick, EndTick = Math.Max(jammer.DeploymentTick + 1, end), Resolution = resolution };
            if (!results.TryGetValue(id, out var previous) || previous.Resolution == "Censored") results[id] = outcome;
        }
        opportunities = results.Values.ToArray(); return true;
    }
}
