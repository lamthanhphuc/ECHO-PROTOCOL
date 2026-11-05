using System.Text.Json;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Api.Data.Telemetry;

namespace EchoProtocol.Api.Services;

public sealed partial class AEDV02SourceVerifier
{
    private static bool TryResolveScannerOpportunities(IReadOnlyList<TelemetryEventDocument> accepted,
        List<(TelemetryEventDocument Event, AEDV02WorldSample Sample)> frames, AEDV02SceneManifest manifest,
        out AEDV02ResolvedOpportunity[] opportunities)
    {
        opportunities = [];
        if (frames.Count < 2) return false;
        var last = frames[^1];
        accepted = accepted.Where(x => x.MatchId == last.Event.MatchId && x.EventSequence <= last.Event.EventSequence).ToArray();
        var results = new Dictionary<(Guid, string, string), AEDV02ResolvedOpportunity>();
        foreach (var group in frames.SelectMany(f => f.Sample.Scans.Select(s => (Frame: f, Scan: s)))
            .GroupBy(x => (x.Scan.UserId, x.Scan.Sequence)).OrderBy(x => x.Min(s => s.Scan.Tick)))
        {
            var first = group.First(); var scan = group.Last().Scan;
            if (scan.Tick < frames[0].Sample.Tick) continue; // A retained display is not a new resolved observation window.
            double range = scan.Mode == "Core" ? manifest.ScannerCoreRange : manifest.ScannerMotionRange;
            if (scan.MatchId != last.Event.MatchId || range <= 0 || manifest.ScannerMaxTargets < 1 || manifest.ScannerActiveSeconds <= 0 ||
                manifest.ScannerCycleSeconds < manifest.ScannerActiveSeconds ||
                scan.Presented && scan.PresentedTick - scan.Tick > manifest.ScannerActiveSeconds * last.Sample.TickRate ||
                Math.Abs(scan.Range - range) > .00001 || Math.Abs(scan.MovingThreshold - manifest.ScannerMovingThreshold) > .00001 ||
                Math.Abs(scan.OcclusionMultiplier - manifest.ScannerOcclusionMultiplier) > .00001 ||
                scan.MaxTargets != (scan.Mode == "Core" ? 1 : manifest.ScannerMaxTargets) ||
                group.Where(x => x.Scan.Presented).Select(x => x.Scan.PresentedTick).Distinct().Count() > 1 ||
                group.Any(x => !SameScan(first.Scan, x.Scan)) || group.SkipWhile(x => !x.Scan.Presented).Any(x => !x.Scan.Presented)) return false;
            var actions = accepted.Where(x => x.EventType == "TEAM_TOOL_USED" && x.SchemaVersion == "1.1" && x.UserId == scan.UserId &&
                x.ValueJson["data"]["toolType"] == "FIELD_SCANNER" && x.ValueJson["context"]["phase"] == scan.PhaseName &&
                x.ValueJson["context"]["authorityTick"].ToInt64() == scan.Tick && x.ValueJson["context"]["occurrenceKey"] == scan.Occurrence &&
                x.EventSequence < first.Frame.Event.EventSequence).ToArray();
            if (actions.Length != 1 || !scan.Occurrence.EndsWith(":tool:FIELD_SCANNER:" + scan.Sequence, StringComparison.Ordinal)) return false;
            var action = actions[0];
            if (accepted.Any(x => x.EventType == "TEAM_TOOL_USED" && x.SchemaVersion == "1.1" && x.UserId == scan.UserId &&
                x.ValueJson["data"]["toolType"] == "FIELD_SCANNER" && x.EventSequence < action.EventSequence &&
                scan.Tick - x.ValueJson["context"]["authorityTick"].ToInt64() < manifest.ScannerCycleSeconds * last.Sample.TickRate)) return false;
            if (!AEDPhase8LifecycleReconcilerV1.TryReconstruct(accepted, action.MatchId, action.EventSequence,
                last.Sample.Players.Select(x => x.UserId).ToHashSet(), false, out var tools, out _, out _) ||
                !tools.Any(x => x.Value.ToolId == 1 && x.Value.Location == "Player" && x.Value.Owner == scan.UserId && x.Value.Charges is not 0)) return false;
            if (scan.Mode == "Motion" && scan.Candidates.Any(c => c.Kind != "Stalker" || !frames.Any(f =>
                Math.Abs(f.Sample.Tick - scan.Tick) <= f.Sample.TickRate && f.Sample.Stalkers.Any(s => s.NetworkId == c.Identity)))) return false;
            if (scan.Mode == "Core" && scan.Candidates.Any(c => c.Kind == "Stalker")) return false;
            var nearby = frames.Where(f => Math.Abs(f.Sample.Tick - scan.Tick) <= f.Sample.TickRate).ToArray();
            var identities = scan.Candidates.Select(c => c.Identity).ToHashSet(StringComparer.Ordinal);
            bool witnessed = nearby.Any(f =>
            {
                var census = f.Sample.Scannables.Where(c => (c.Kind == "Stalker") == (scan.Mode == "Motion")).ToArray();
                if (!identities.SetEquals(census.Select(c => c.Identity))) return false;
                return scan.Candidates.All(c => census.Any(q => q.Identity == c.Identity && q.LocalId == c.LocalId && q.Kind == c.Kind && q.Available == c.Available &&
                    (!c.Available || (scan.Mode != "Motion" || (c.Moving || c.Speed >= scan.MovingThreshold) == (q.Moving || q.Speed >= scan.MovingThreshold)) &&
                        Distance(c.X, c.Y, c.Z, q.X, q.Y, q.Z) <= AEDV02PilotOpportunities.ScannerCensusPositionTolerance +
                        Math.Abs(f.Sample.Tick - scan.Tick) / (double)f.Sample.TickRate * nearby.SelectMany(n => n.Sample.Scannables)
                            .Where(n => n.Identity == c.Identity).Select(n => n.Speed).DefaultIfEmpty(0).Max())));
            });
            if (!witnessed) return false;
            var eligible = scan.Candidates.Where(c => c.Available &&
                ScanDistance(c, scan) <= (float)range &&
                (scan.Mode == "Core" || c.Moving || c.Speed >= scan.MovingThreshold)).ToArray();
            int[] expected;
            if (scan.Mode == "Motion") expected = eligible.OrderBy(c => ScanDistance(c, scan))
                .ThenBy(c => c.LocalId).Take(scan.MaxTargets).Select(c => c.LocalId).ToArray();
            else
            {
                AEDV02ScanCandidate? best = null; float bestScore = -1, bestDistance = float.MaxValue;
                foreach (var c in eligible)
                {
                    float distance = ScanDistance(c, scan);
                    float score = Math.Clamp(1f - distance / (float)range, 0f, 1f) * (c.Occluded ? (float)scan.OcclusionMultiplier : 1f);
                    if (score > bestScore + .0001f || Math.Abs(score - bestScore) <= .0001f &&
                        (distance < bestDistance - .001f || Math.Abs(distance - bestDistance) <= .001f && (best == null || c.LocalId < best.LocalId)))
                    { best = c; bestScore = score; bestDistance = distance; }
                }
                expected = best == null ? [] : [best.LocalId];
            }
            if (!scan.ResultIds.SequenceEqual(expected)) return false;
            if (frames.Any(f => f.Sample.OpportunityContractVersion != AEDV02PilotOpportunities.Version)) continue;
            // Fresh information is the Scanner's effect; repeated detection of the same target is not another skill sample.
            foreach (var target in eligible.Where(c => expected.Contains(c.LocalId)))
            {
                var key = (scan.UserId, scan.PhaseName, target.Identity);
                if (results.TryGetValue(key, out var previous) && previous.Resolution == "Success") continue;
                results[key] = new AEDV02ResolvedOpportunity { Id = "Tool:1:" + scan.PhaseName + ":" + target.Identity, Axis = "Tool", ToolId = 1,
                    UserId = scan.UserId, StartTick = scan.Tick, EndTick = Math.Max(scan.Tick + 1, group.Last().Frame.Sample.Tick),
                    Resolution = scan.Presented ? "Success" : "Censored" };
            }
        }
        opportunities = results.Values.ToArray(); return true;
    }

    private static bool SameScan(AEDV02ScanSource a, AEDV02ScanSource b) =>
        a.MatchId == b.MatchId && a.UserId == b.UserId && a.Sequence == b.Sequence && a.Occurrence == b.Occurrence && a.Tick == b.Tick &&
        a.PhaseName == b.PhaseName && a.Mode == b.Mode && a.Range == b.Range && a.MovingThreshold == b.MovingThreshold &&
        a.OcclusionMultiplier == b.OcclusionMultiplier && a.MaxTargets == b.MaxTargets && a.X == b.X && a.Y == b.Y && a.Z == b.Z &&
        a.ResultIds.SequenceEqual(b.ResultIds) && JsonSerializer.Serialize(a.Candidates) == JsonSerializer.Serialize(b.Candidates);

    private static float ScanDistance(AEDV02ScanCandidate c, AEDV02ScanSource s)
    {
        float x = (float)c.X - (float)s.X, y = (float)c.Y - (float)s.Y, z = (float)c.Z - (float)s.Z;
        return (float)Math.Sqrt(x * x + y * y + z * z);
    }
}
