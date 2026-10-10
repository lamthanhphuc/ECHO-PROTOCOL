using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDMinionSpawnCandidateV1
    {
        public string PointId { get; set; } public int Zone { get; set; } public int RoomId { get; set; }
        public bool HostVerified { get; set; } public bool NavMeshSampled { get; set; } public bool CompletePathFromStalker { get; set; }
        public bool SafeRoomClassificationVerified { get; set; } public bool InsideSafeRoom { get; set; }
        public double DistanceFromStalker { get; set; } public double DistanceFromNearestAlivePlayer { get; set; }
        public double DistanceFromZoneEntry { get; set; } public double? DistanceFromNearestExistingMinion { get; set; }
        public double X { get; set; } public double Y { get; set; } public double Z { get; set; }
    }

    public sealed class AEDMinionPlacementReceiptV1
    {
        public string PointId { get; } public int Zone { get; } public int RoomId { get; }
        public double DistanceFromStalker { get; } public double DistanceFromNearestPlayer { get; }
        public bool ResearchOnly => true; public bool CanApplyGameplay => false;
        internal AEDMinionPlacementReceiptV1(AEDMinionSpawnCandidateV1 point)
        { PointId = point.PointId; Zone = point.Zone; RoomId = point.RoomId; DistanceFromStalker = point.DistanceFromStalker; DistanceFromNearestPlayer = point.DistanceFromNearestAlivePlayer; }
    }

    public static class AEDMinionPlacementV1
    {
        public const double MinimumPlayerDistance = 18d, MinimumStalkerDistance = 4d, PreferredStalkerDistance = 10d, MaximumStalkerDistance = 28d, MinimumZone1EntryDistance = 20d, MinimumOtherMinionDistance = 8d;
        public const int MaximumCandidates = 256;
        private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        private static bool Spaced(AEDMinionSpawnCandidateV1 a, AEDMinionSpawnCandidateV1 b)
        { var dx = a.X - b.X; var dy = a.Y - b.Y; var dz = a.Z - b.Z; return dx * dx + dy * dy + dz * dz >= MinimumOtherMinionDistance * MinimumOtherMinionDistance; }

        public static bool TryPlan(AEDMinionProposalV1 proposal, IEnumerable<AEDMinionSpawnCandidateV1> candidates, out IReadOnlyList<AEDMinionPlacementReceiptV1> receipts)
        {
            receipts = Array.Empty<AEDMinionPlacementReceiptV1>();
            if (!AEDMinionDirectorV1.Validate(proposal) || candidates == null || proposal.AdditionalSpawnSlots <= 0 || proposal.AdditionalSpawnSlots > AEDMinionDirectorV1.MaxResearchCap) return false;
            var points = candidates.ToArray();
            if (points.Length == 0 || points.Length > MaximumCandidates || points.Any(p => p == null || string.IsNullOrWhiteSpace(p.PointId) || p.RoomId <= 0 || !Finite(p.X) || !Finite(p.Y) || !Finite(p.Z) || !Finite(p.DistanceFromStalker) || !Finite(p.DistanceFromNearestAlivePlayer) || !Finite(p.DistanceFromZoneEntry) || (p.DistanceFromNearestExistingMinion.HasValue && !Finite(p.DistanceFromNearestExistingMinion.Value)))) return false;
            if (points.GroupBy(x => x.PointId, StringComparer.Ordinal).Any(group => group.Count() > 1)) return false;
            var eligible = points.Where(p => p.Zone == proposal.Zone && p.HostVerified && p.NavMeshSampled && p.CompletePathFromStalker && p.SafeRoomClassificationVerified && !p.InsideSafeRoom && p.DistanceFromNearestAlivePlayer >= MinimumPlayerDistance && p.DistanceFromStalker >= MinimumStalkerDistance && p.DistanceFromStalker <= MaximumStalkerDistance && (p.Zone != 1 || p.DistanceFromZoneEntry >= MinimumZone1EntryDistance) && (proposal.CurrentPopulation == 0 || (p.DistanceFromNearestExistingMinion.HasValue && p.DistanceFromNearestExistingMinion.Value >= MinimumOtherMinionDistance))).OrderBy(p => p.DistanceFromStalker <= PreferredStalkerDistance ? 0 : 1).ThenByDescending(p => p.DistanceFromNearestAlivePlayer).ThenBy(p => p.RoomId).ThenBy(p => p.PointId, StringComparer.Ordinal).ToArray();
            var selected = new List<AEDMinionSpawnCandidateV1>();
            bool Solve(int startIndex)
            {
                if (selected.Count == proposal.AdditionalSpawnSlots) return true;
                for (int i = startIndex; i < eligible.Length; i++)
                { var candidate = eligible[i]; if (!selected.All(other => Spaced(candidate, other))) continue; selected.Add(candidate); if (Solve(i + 1)) return true; selected.RemoveAt(selected.Count - 1); }
                return false;
            }
            if (!Solve(0)) return false;
            receipts = new ReadOnlyCollection<AEDMinionPlacementReceiptV1>(selected.Select(p => new AEDMinionPlacementReceiptV1(p)).ToArray());
            return true;
        }
    }
}
