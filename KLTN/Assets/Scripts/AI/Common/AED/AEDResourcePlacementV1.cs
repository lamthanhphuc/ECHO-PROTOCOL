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

        public static bool TryPlan(AEDResourceProposalV1 proposal,
            IEnumerable<AEDResourceSpawnCandidateV1> candidates, double minimumSpacing,
            out IReadOnlyList<AEDResourcePlacementReceiptV1> receipts)
        {
            receipts = Array.Empty<AEDResourcePlacementReceiptV1>();
            if (proposal == null || !AEDResourceDirectorV1.ValidateFairness(proposal) ||
                candidates == null || minimumSpacing < 0 || double.IsNaN(minimumSpacing) ||
                double.IsInfinity(minimumSpacing)) return false;

            var points = candidates.ToArray();
            if (points.Any(p => p == null || string.IsNullOrWhiteSpace(p.PointId) ||
                p.RoomId <= 0 || p.AllowedToolIds == null ||
                double.IsNaN(p.PathDistanceFromZoneEntry) ||
                double.IsInfinity(p.PathDistanceFromZoneEntry))) return false;
            if (points.GroupBy(p => p.PointId).Any(group => group.Count() > 1)) return false;

            var selected = new List<AEDResourcePlacementReceiptV1>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            var occupied = new List<AEDResourceSpawnCandidateV1>();
            bool FarEnough(AEDResourceSpawnCandidateV1 candidate)
            {
                var squared = minimumSpacing * minimumSpacing;
                return occupied.Where(p => p.Zone == candidate.Zone).All(p =>
                {
                    var dx = p.X - candidate.X; var dy = p.Y - candidate.Y; var dz = p.Z - candidate.Z;
                    return dx * dx + dy * dy + dz * dz >= squared;
                });
            }

            var zoneRequests = new[] { proposal.Zone1, proposal.Zone2, proposal.Zone3 };
            for (int zone = 1; zone <= 3; zone++)
            {
                foreach (var toolId in zoneRequests[zone - 1])
                {
                    var eligible = points.Where(p => p.Zone == zone && p.NavMeshReachable &&
                        p.PathDistanceFromZoneEntry >= 0 && p.AllowedToolIds.Contains(toolId) &&
                        !used.Contains(p.PointId) && FarEnough(p)).ToArray();
                    if (toolId == AEDResourceToolIdsV1.Scanner && proposal.PreferDistantScanner)
                        eligible = eligible.Where(p => p.PathDistanceFromZoneEntry >= DistantScannerMeters).ToArray();
                    var roomCounts = selected.Where(x => x.Zone == zone)
                        .GroupBy(x => x.RoomId).ToDictionary(g => g.Key, g => g.Count());
                    var ordered = eligible.OrderBy(p => roomCounts.TryGetValue(p.RoomId, out int n) ? n : 0)
                        .ThenBy(p => toolId == AEDResourceToolIdsV1.FirstAid ? p.PathDistanceFromZoneEntry : -p.PathDistanceFromZoneEntry)
                        .ThenBy(p => p.PointId, StringComparer.Ordinal).ToArray();
                    if (ordered.Length == 0) return false;
                    var chosen = ordered[0]; used.Add(chosen.PointId); occupied.Add(chosen);
                    selected.Add(new AEDResourcePlacementReceiptV1(zone, toolId, chosen.PointId,
                        chosen.RoomId, chosen.PathDistanceFromZoneEntry));
                }
            }
            receipts = new ReadOnlyCollection<AEDResourcePlacementReceiptV1>(selected);
            return true;
        }
    }
}
