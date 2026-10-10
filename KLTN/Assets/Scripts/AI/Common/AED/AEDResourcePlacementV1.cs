using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDResourceSpawnCandidateV1
    {
        public string PointId { get; set; }
        public int Zone { get; set; }
        public int RoomId { get; set; }
        public IReadOnlyList<int> AllowedToolIds { get; set; }
        public bool NavMeshReachable { get; set; }
        public double PathDistanceFromZoneEntry { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public sealed class AEDResourcePlacementReceiptV1
    {
        public int Zone { get; }
        public int ToolId { get; }
        public string PointId { get; }
        public int RoomId { get; }
        public double PathDistance { get; }
        internal AEDResourcePlacementReceiptV1(int zone, int toolId, string pointId,
            int roomId, double distance)
        { Zone = zone; ToolId = toolId; PointId = pointId; RoomId = roomId; PathDistance = distance; }
    }

    public static class AEDResourcePlacementV1
    {
        public const double DistantScannerMeters = 20d;
        public const int MaxSearchNodes = 200000;

        private sealed class Slot { public int Index; public int Zone; public int ToolId; }
        private sealed class Assignment { public Slot Slot; public AEDResourceSpawnCandidateV1 Point; }

        private static bool Finite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        public static bool TryPlan(AEDResourceProposalV1 proposal,
            IEnumerable<AEDResourceSpawnCandidateV1> candidates, double minimumSpacing,
            out IReadOnlyList<AEDResourcePlacementReceiptV1> receipts)
        {
            receipts = Array.Empty<AEDResourcePlacementReceiptV1>();
            if (proposal == null || !AEDResourceDirectorV1.ValidateFairness(proposal) ||
                candidates == null || !Finite(minimumSpacing) || minimumSpacing < 0)
                return false;

            var points = candidates.ToArray();
            if (points.Any(p => p == null || string.IsNullOrWhiteSpace(p.PointId) ||
                p.Zone < 1 || p.Zone > 3 || p.RoomId <= 0 || p.AllowedToolIds == null ||
                !Finite(p.PathDistanceFromZoneEntry) || !Finite(p.X) || !Finite(p.Y) ||
                !Finite(p.Z) || p.AllowedToolIds.Any(id => !AEDResourceToolIdsV1.All.Contains(id))))
                return false;
            if (points.GroupBy(p => p.PointId, StringComparer.Ordinal).Any(g => g.Count() > 1))
                return false;

            var requests = new List<Slot>();
            int sequence = 0;
            var zones = new[] { proposal.Zone1, proposal.Zone2, proposal.Zone3 };
            for (int zone = 1; zone <= 3; zone++)
                foreach (int toolId in zones[zone - 1])
                    requests.Add(new Slot { Index = sequence++, Zone = zone, ToolId = toolId });

            var selected = new List<Assignment>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            int exploredNodes = 0;
            bool FarEnough(AEDResourceSpawnCandidateV1 candidate)
            {
                double squared = minimumSpacing * minimumSpacing;
                return selected.Where(a => a.Slot.Zone == candidate.Zone).All(a =>
                {
                    var dx = a.Point.X - candidate.X; var dy = a.Point.Y - candidate.Y; var dz = a.Point.Z - candidate.Z;
                    return dx * dx + dy * dy + dz * dz >= squared;
                });
            }

            AEDResourceSpawnCandidateV1[] Eligible(Slot slot)
            {
                var query = points.Where(p => p.Zone == slot.Zone && p.NavMeshReachable &&
                    p.PathDistanceFromZoneEntry >= 0 && p.AllowedToolIds.Contains(slot.ToolId) &&
                    !used.Contains(p.PointId) && FarEnough(p));
                if (slot.ToolId == AEDResourceToolIdsV1.Scanner && proposal.PreferDistantScanner)
                    query = query.Where(p => p.PathDistanceFromZoneEntry >= DistantScannerMeters);
                return query.ToArray();
            }

            int RoomUse(int zone, int roomId) => selected.Count(a =>
                a.Slot.Zone == zone && a.Point.RoomId == roomId);

            bool Solve()
            {
                if (selected.Count == requests.Count) return true;
                if (++exploredNodes > MaxSearchNodes) return false;
                var next = requests.Where(slot => !selected.Any(a => a.Slot.Index == slot.Index))
                    .Select(slot => new { Slot = slot, Options = Eligible(slot) })
                    .OrderBy(x => x.Options.Length)
                    .ThenBy(x => x.Slot.ToolId == AEDResourceToolIdsV1.FirstAid ? 0 : 1)
                    .ThenBy(x => x.Slot.Zone).ThenBy(x => x.Slot.Index).First();
                if (next.Options.Length == 0) return false;
                var ordered = next.Options.OrderBy(p => RoomUse(next.Slot.Zone, p.RoomId))
                    .ThenBy(p => next.Slot.ToolId == AEDResourceToolIdsV1.FirstAid ? p.PathDistanceFromZoneEntry : -p.PathDistanceFromZoneEntry)
                    .ThenBy(p => p.PointId, StringComparer.Ordinal);
                foreach (var point in ordered)
                {
                    selected.Add(new Assignment { Slot = next.Slot, Point = point });
                    used.Add(point.PointId);
                    if (Solve()) return true;
                    used.Remove(point.PointId);
                    selected.RemoveAt(selected.Count - 1);
                }
                return false;
            }

            if (!Solve()) return false;
            var result = selected.OrderBy(a => a.Slot.Index)
                .Select(a => new AEDResourcePlacementReceiptV1(a.Slot.Zone, a.Slot.ToolId,
                    a.Point.PointId, a.Point.RoomId, a.Point.PathDistanceFromZoneEntry)).ToArray();
            if (result.Length != requests.Count) return false;
            receipts = new ReadOnlyCollection<AEDResourcePlacementReceiptV1>(result);
            return true;
        }
    }
}
