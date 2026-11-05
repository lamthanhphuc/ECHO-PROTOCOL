#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public static class AEDV02ResourcePlacement
    {
        public static bool ValidateProposed(AEDV02SceneManifest manifest, AEDV02WorldSample world,
            AEDV02ProposedReservation[] proposed, int grade)
        {
            if (manifest == null || world?.Supply == null || manifest.MinimumToolSpacing < 0 || grade is < -1 or > 3 ||
                proposed == null || proposed.Length == 0 || proposed.Length != world.Supply.Reservations.Length ||
                proposed.Any(x => x == null || !x.IsValid()) || proposed.Select(x => x.MarkerId).Distinct().Count() != proposed.Length) return false;
            var placements = new List<AEDV02SceneMarker>();
            foreach (var zone in world.Supply.Reservations.GroupBy(x => x.Zone))
            {
                var replacements = proposed.Where(x => x.Zone == zone.Key).ToArray();
                var entry = manifest.Markers.SingleOrDefault(x => x.Kind == "ENTRY" && x.Zone == zone.Key);
                if (entry == null || world.Supply.SpawnedZones.Contains(zone.Key) ||
                    !replacements.Select(x => x.ToolId).OrderBy(x => x).SequenceEqual(zone.Select(x => x.ToolId).OrderBy(x => x))) return false;
                foreach (var replacement in replacements)
                {
                    var marker = manifest.Markers.SingleOrDefault(x => x.Id == replacement.MarkerId && x.Zone == zone.Key &&
                        x.Kind == "TOOL" && x.AllowedToolIds.Contains(replacement.ToolId) && AllowsRole(grade, x.ResourceRole));
                    if (marker == null || !manifest.TryIsInsideSafeRoom(marker.X, marker.Y, marker.Z, out var inside) || inside ||
                        !manifest.TryPathIntersectsSafeRoom(new[] { new[] { entry.X, entry.Y, entry.Z },
                            new[] { marker.X, marker.Y, marker.Z } }, out var routeIntersects) || routeIntersects) return false;
                    if (!manifest.HasReachableGeometry(new[] { entry.X, entry.Y, entry.Z },
                        new[] { marker.X, marker.Y, marker.Z }, world.Doors) || !ClearOfSupply(marker, world, manifest.MinimumToolSpacing) ||
                        placements.Any(other => Distance(marker, other.X, other.Y, other.Z) < manifest.MinimumToolSpacing)) return false;
                    placements.Add(marker);
                }
            }
            return placements.Count == proposed.Length;
        }

        public static bool ClearOfSupply(AEDV02SceneMarker marker, AEDV02WorldSample world, double spacing) =>
            marker != null && world != null && AEDV02PilotPolicy.Finite(spacing) && spacing >= 0 &&
            world.Tools.Where(x => x.Location == "World" && x.CarrierMinionId.Length == 0)
                .All(tool => Distance(marker, tool.X, tool.Y, tool.Z) >= spacing);

        private static double Distance(AEDV02SceneMarker marker, double x, double y, double z) =>
            Math.Sqrt((marker.X - x) * (marker.X - x) + (marker.Y - y) * (marker.Y - y) + (marker.Z - z) * (marker.Z - z));

        public static bool? UsableSupply(AEDV02SceneManifest manifest, AEDV02WorldSample world, AEDV02PlayerSource need, int toolId)
        {
            var candidates = world.Tools.Where(x => x.ToolId == toolId && x.RemainingCharges != 0 && x.CarrierMinionId.Length == 0 && x.Location is "World" or "Player").ToArray();
            if (candidates.Length == 0) return false;
            if (candidates.Any(x => x.Location == "Player" && x.OwnerUserId == need.UserId && need.LifeState == "ALIVE" && !need.IsCaught)) return true;
            bool unknown = false;
            foreach (var helper in world.Players.Where(x => x.LifeState == "ALIVE" && !x.IsCaught))
                foreach (var tool in candidates.Where(x => x.Location == "World" || x.OwnerUserId == helper.UserId))
                {
                    bool knownTool = tool.Location == "Player" || manifest.TryReachableGeometry(new[] { helper.X, helper.Y, helper.Z },
                        new[] { tool.X, tool.Y, tool.Z }, world.Doors, out _);
                    bool toTool = tool.Location == "Player" || manifest.HasReachableGeometry(new[] { helper.X, helper.Y, helper.Z }, new[] { tool.X, tool.Y, tool.Z }, world.Doors);
                    bool knownNeed = manifest.TryReachableGeometry(new[] { tool.X, tool.Y, tool.Z }, new[] { need.X, need.Y, need.Z }, world.Doors, out bool toNeed);
                    if (knownTool && knownNeed && toTool && toNeed) return true;
                    unknown |= !knownTool || !knownNeed;
                }
            return unknown ? (bool?)null : false;
        }
        public static bool AllowsRole(int grade, string role) => grade == 0 || grade == 2 ||
            grade == -1 && role == "SideRoom" || grade is 1 or 3 && role is "MainRoute" or "FirstRoom" or "ObjectiveRoute";

        public static bool TryPlan(int zone, int grade, int[] types, IEnumerable<AEDV02PlacementCandidate> candidates,
            double spacing, out AEDV02SupplyReservation[] reservations)
        {
            reservations = Array.Empty<AEDV02SupplyReservation>();
            if (zone is < 1 or > 3 || grade is < -1 or > 3 || types == null || types.Length is < 1 or > 7 ||
                types.Any(x => x is not (1 or 2 or 3 or 4 or 6)) || candidates == null || !AEDV02PilotPolicy.Finite(spacing) || spacing < 0) return false;
            var input = candidates.Take(513).ToArray();
            if (input.Length > 512 || input.Any(x => x == null || x.Marker == null || x.Marker.AllowedToolIds == null ||
                !AEDV02WorldSample.Hash(x.Marker.Id) || x.Path == null || x.Path.Length > 256 ||
                x.Path.Any(p => p == null || p.Length != 3 || p.Any(v => !AEDV02PilotPolicy.Finite(v))) ||
                !AEDV02PilotPolicy.Finite(x.Marker.X) || !AEDV02PilotPolicy.Finite(x.Marker.Y) || !AEDV02PilotPolicy.Finite(x.Marker.Z))) return false;
            var points = input.Where(x => x.Marker.Zone == zone && x.Marker.Kind == "TOOL" && AllowsRole(grade, x.Marker.ResourceRole) &&
                AEDV02PilotPolicy.Finite(x.PathMeters) && x.PathMeters >= 0 && x.Path.Length != 0).OrderBy(x => x.PathMeters)
                .ThenBy(x => x.Marker.Id, StringComparer.Ordinal).ToArray();
            if (points.Select(x => x.Marker.Id).Distinct(StringComparer.Ordinal).Count() != points.Length) return false;
            var selected = new List<(int Type, AEDV02PlacementCandidate Point)>();
            int attempts = 0;
            var order = types.Select((type, index) => (type, index)).OrderBy(x => points.Count(p => p.Marker.AllowedToolIds.Contains(x.type))).ThenBy(x => x.index).ToArray();
            bool Select(int index)
            {
                if (index == order.Length) return true;
                foreach (var point in points.Where(x => x.Marker.AllowedToolIds.Contains(order[index].type)))
                {
                    // Bound a pathological authored layout; absence of a proven placement remains a Hold.
                    if (++attempts > 4096) return false;
                    if (selected.Any(x => x.Point.Marker.Id == point.Marker.Id ||
                        Math.Pow(x.Point.Marker.X - point.Marker.X, 2) + Math.Pow(x.Point.Marker.Y - point.Marker.Y, 2) +
                        Math.Pow(x.Point.Marker.Z - point.Marker.Z, 2) < spacing * spacing)) continue;
                    selected.Add((order[index].type, point));
                    if (Select(index + 1)) return true;
                    selected.RemoveAt(selected.Count - 1);
                }
                return false;
            }
            if (!Select(0)) return false;
            reservations = selected.Select(x => new AEDV02SupplyReservation { Zone = zone, ToolId = x.Type,
                MarkerId = x.Point.Marker.Id, EntryPath = x.Point.Path }).ToArray();
            return true;
        }
    }

    public sealed class AEDV02PlacementCandidate
    {
        public AEDV02SceneMarker Marker { get; set; } = new AEDV02SceneMarker();
        public double[][] Path { get; set; } = Array.Empty<double[]>();
        public double PathMeters { get; set; }
    }

    public sealed partial class AEDV02SceneManifest
    {
        public bool TryValidatePlacementPath(double[][] path, AEDV02DoorSource[] doors, out double meters)
        {
            meters = 0;
            if (path == null || path.Length is < 1 or > 256 || doors == null || NavCells.Length == 0 ||
                path.Any(p => p == null || p.Length != 3 || p.Any(x => !AEDV02PilotPolicy.Finite(x) || Math.Abs(x) > 10000)) ||
                !TryPathIntersectsSafeRoom(path, out var intersectsSafeRoom) || intersectsSafeRoom) return false;
            var open = doors.Where(x => x.State is "OPEN" or "BROKEN" or "CLOSED_UNLOCKED").Select(x => x.DoorId).ToHashSet();
            if (DoorIds.Any(id => !doors.Any(x => x.DoorId == id))) return false;
            var cells = NavCells.Where(x => x.DoorIds.All(open.Contains)).ToArray();
            if (path.Length == 1) return cells.Any(x => x.Contains(path[0]));
            for (int i = 1; i < path.Length; i++)
            {
                var intervals = cells.Select(c => SegmentInterval(c, path[i - 1], path[i])).Where(x => x.HasValue)
                    .Select(x => x!.Value).OrderBy(x => x.Start).ToArray();
                double covered = 0;
                foreach (var interval in intervals)
                {
                    if (interval.Start > covered + .000001) return false;
                    covered = Math.Max(covered, interval.End);
                    if (covered >= 1 - .000001) break;
                }
                if (covered < 1 - .000001) return false;
                meters += Math.Sqrt(Enumerable.Range(0, 3).Sum(axis => Math.Pow(path[i][axis] - path[i - 1][axis], 2)));
            }
            return true;
        }

        private static (double Start, double End)? SegmentInterval(AEDV02NavCell cell, double[] from, double[] to)
        {
            var u = Enumerable.Range(0, 3).Select(i => cell.B[i] - cell.A[i]).ToArray();
            var v = Enumerable.Range(0, 3).Select(i => cell.C[i] - cell.A[i]).ToArray();
            double Dot(double[] a, double[] b) => Enumerable.Range(0, 3).Sum(i => a[i] * b[i]);
            double uu = Dot(u, u), uv = Dot(u, v), vv = Dot(v, v), denominator = uu * vv - uv * uv;
            var normal = new[] { u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0] };
            double length = Math.Sqrt(Dot(normal, normal));
            if (denominator <= .00000001 || length <= .00000001) return null;
            double[] Coordinates(double[] point)
            {
                var w = Enumerable.Range(0, 3).Select(i => point[i] - cell.A[i]).ToArray();
                return new[] { (vv * Dot(w, u) - uv * Dot(w, v)) / denominator,
                    (uu * Dot(w, v) - uv * Dot(w, u)) / denominator, Dot(w, normal) / length };
            }
            var a = Coordinates(from); var b = Coordinates(to); double start = 0, end = 1;
            bool Clip(double first, double last, double minimum)
            {
                if (first >= minimum && last >= minimum) return true;
                if (first < minimum && last < minimum) return false;
                double crossing = (minimum - first) / (last - first);
                if (first < minimum) start = Math.Max(start, crossing); else end = Math.Min(end, crossing);
                return start <= end;
            }
            return Clip(a[0], b[0], -.000001) && Clip(a[1], b[1], -.000001) &&
                Clip(1 - a[0] - a[1], 1 - b[0] - b[1], -.000001) && Clip(a[2], b[2], -1) && Clip(-a[2], -b[2], -1)
                ? (start, end) : null;
        }
    }
}
