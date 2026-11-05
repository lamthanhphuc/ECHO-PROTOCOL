using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data.Telemetry;

namespace EchoProtocol.Api.Services;

public sealed partial class AEDV02SourceVerifier
{
    private static bool TryResolveStabilizerOpportunities(IReadOnlyList<TelemetryEventDocument> accepted,
        List<(TelemetryEventDocument Event, AEDV02WorldSample Sample)> frames, AEDV02SceneManifest manifest,
        out AEDV02ResolvedOpportunity[] opportunities, out string reason)
    {
        opportunities = []; reason = "HOLD_TOOL_STABILIZER_LINEAGE_INVALID";
        if (frames.Count < 2) return false;
        if (!frames.Any(x => x.Sample.Players.Any(p => p.StabilizerUntilTick > x.Sample.Tick || p.StabilizerBuffed))) return true;
        if (manifest.CoreStabilizerRadius <= 0 || manifest.CoreStabilizerDurationSeconds <= 0)
        { reason = "HOLD_TOOL_STABILIZER_SOURCE_UNSUPPORTED"; return false; }
        foreach (var frame in frames)
        {
            var world = frame.Sample;
            var active = world.Players.Where(x => x.StabilizerUntilTick > world.Tick).ToArray();
            foreach (var owner in active)
                if (owner.StabilizerActivationOrdinal == 0 || owner.StabilizerUntilTick - world.Tick > manifest.CoreStabilizerDurationSeconds * world.TickRate + 1 ||
                    !world.Tools.Any(x => x.InstanceId == owner.HeldToolInstanceId && x.ToolId == 6 && x.Location == "Player" && x.OwnerUserId == owner.UserId)) return false;
            foreach (var player in world.Players)
            {
                bool covered = player.LifeState == "ALIVE" && !player.IsCaught && active.Any(owner =>
                    Distance(player.X, player.Y, player.Z, owner.X, owner.Y, owner.Z) <= manifest.CoreStabilizerRadius);
                if (covered != player.StabilizerBuffed) return false;
                if (covered && player.ObjectiveState == "OPERATING" && player.ObjectiveUnitId.StartsWith("core:", StringComparison.Ordinal) &&
                    !VerifiedCoreCarrier(accepted, frame.Event, player)) return false;
            }
        }
        if (frames.Any(x => x.Sample.OpportunityContractVersion != AEDV02PilotOpportunities.Version)) return true;
        var activations = frames.SelectMany(frame => frame.Sample.Players.Where(p => p.StabilizerUntilTick > frame.Sample.Tick)
                .Select(owner => (Frame: frame, Owner: owner)))
            .GroupBy(x => (x.Owner.UserId, x.Owner.HeldToolInstanceId, x.Owner.StabilizerActivationOrdinal, x.Owner.StabilizerUntilTick));
        var outcomes = new List<AEDV02ResolvedOpportunity>();
        foreach (var activation in activations)
        {
            var first = activation.OrderBy(x => x.Frame.Sample.Tick).First();
            var before = frames.LastOrDefault(x => x.Sample.Tick < first.Frame.Sample.Tick);
            bool onset = before.Sample != null && before.Sample.Players.Any(p => p.UserId == activation.Key.UserId && p.StabilizerUntilTick <= before.Sample.Tick);
            double useful = 0; long ended = first.Frame.Sample.Tick;
            foreach (var observed in activation.OrderBy(x => x.Frame.Sample.Tick))
            {
                var next = frames.FirstOrDefault(x => x.Sample.Tick > observed.Frame.Sample.Tick);
                if (next.Sample == null || next.Sample.Tick > activation.Key.StabilizerUntilTick) continue;
                bool meaningful = observed.Frame.Sample.Players.Any(player => player.StabilizerBuffed && player.LifeState == "ALIVE" && !player.IsCaught &&
                    Distance(player.X, player.Y, player.Z, observed.Owner.X, observed.Owner.Y, observed.Owner.Z) <= manifest.CoreStabilizerRadius &&
                    (VerifiedCoreCarrier(accepted, observed.Frame.Event, player) &&
                        next.Sample.Players.Any(p => p.UserId == player.UserId && VerifiedCoreCarrier(accepted, next.Event, p)) ||
                        frames.Any(prior => prior.Sample.Tick <= observed.Frame.Sample.Tick &&
                            observed.Frame.Sample.Tick - prior.Sample.Tick <= observed.Frame.Sample.TickRate * AEDV02PilotOpportunities.ToolThreatContextSeconds &&
                            (prior.Sample.Stalkers.Any(s => s.TargetUserId == player.UserId && s.State is "CHASE" or "ATTACK") ||
                             prior.Sample.Minions.Any(m => m.TargetUserId == player.UserId && m.State is "TRACK" or "HARASS")))));
                if (!meaningful) continue;
                useful += (next.Sample.Tick - observed.Frame.Sample.Tick) / (double)observed.Frame.Sample.TickRate;
                ended = next.Sample.Tick;
            }
            if (useful <= 0) continue; // Activation in an empty room has no eligible effectiveness opportunity.
            outcomes.Add(new AEDV02ResolvedOpportunity { Id = "Tool:6:" + activation.Key.HeldToolInstanceId + ":" + activation.Key.StabilizerActivationOrdinal + ":" + activation.Key.StabilizerUntilTick,
                Axis = "Tool", ToolId = 6, UserId = activation.Key.UserId, StartTick = first.Frame.Sample.Tick,
                EndTick = Math.Max(first.Frame.Sample.Tick + 1, ended), Resolution = onset && useful >= AEDV02PilotOpportunities.StabilizerEffectSeconds ? "Success" : "Censored" });
        }
        opportunities = outcomes.ToArray(); reason = string.Empty; return true;
    }

    private static bool VerifiedCoreCarrier(IReadOnlyList<TelemetryEventDocument> accepted, TelemetryEventDocument observation, AEDV02PlayerSource player)
    {
        if (player.ObjectiveState != "OPERATING" || !player.ObjectiveUnitId.StartsWith("core:", StringComparison.Ordinal)) return false;
        string core = player.ObjectiveUnitId[5..];
        var last = accepted.Where(x => x.MatchId == observation.MatchId && x.EventSequence < observation.EventSequence &&
                x.EventType is "CORE_PICKED_UP" or "CORE_DROPPED" or "CORE_PLACED" && x.ValueJson["data"]["coreId"] == core)
            .OrderByDescending(x => x.EventSequence).FirstOrDefault();
        return last?.EventType == "CORE_PICKED_UP" && last.UserId == player.UserId;
    }
}
