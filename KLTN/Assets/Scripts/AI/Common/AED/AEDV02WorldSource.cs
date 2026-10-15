#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDV02PlayerSource
    {
        public Guid UserId { get; set; }
        public string LifeState { get; set; } = string.Empty;
        public bool IsCaught { get; set; }
        public bool IsReviving { get; set; }
        public Guid? ReviverUserId { get; set; }
        public string HeldToolInstanceId { get; set; } = string.Empty;
        public string ObjectiveUnitId { get; set; } = string.Empty;
        public string ObjectiveState { get; set; } = "NONE";
        public string ObjectiveCauseEntityId { get; set; } = string.Empty;
        public string ObjectiveCauseEventId { get; set; } = string.Empty;
        public string ResourceCauseEventId { get; set; } = string.Empty;
        public long SlowUntilTick { get; set; }
        public int RequiredToolId { get; set; }
        public string HideSpotId { get; set; } = "0";
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
    public sealed class AEDV02ThreatSource
    {
        public string NetworkId { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string EpisodeId { get; set; } = string.Empty;
        public Guid? TargetUserId { get; set; }
        public double[] ConsumerValues { get; set; } = Array.Empty<double>();
        public bool MatchesStalkerConsumer(AEDV02Plan plan) => plan != null && ConsumerValues != null && ConsumerValues.Length == 6 &&
            ConsumerValues.Select((value, index) => AEDV02PilotPolicy.Finite(value) && Math.Abs(value - plan.Get((AEDV02Key)index)) < .000001).All(matches => matches);
        public int Zone { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
    public sealed class AEDV02ToolSource
    {
        public string InstanceId { get; set; } = string.Empty;
        public int ToolId { get; set; }
        public int Zone { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public Guid? OwnerUserId { get; set; }
        public int? RemainingCharges { get; set; }
        public string CarrierMinionId { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string MarkerId { get; set; } = string.Empty;
        public string[] RouteEdgeIds { get; set; } = Array.Empty<string>();
    }
    public sealed class AEDV02DoorSource
    {
        public string DoorId { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }
    public sealed class AEDV02SupplyReservation
    {
        public int Zone { get; set; }
        public int ToolId { get; set; }
        public string MarkerId { get; set; } = string.Empty;
        public double[][] EntryPath { get; set; } = Array.Empty<double[]>();
    }
    public sealed class AEDV02SupplySource
    {
        public int InitialOptionalBudget { get; set; }
        public int OptionalBudget { get; set; }
        public int OptionalMaterialized { get; set; }
        public int ReservationGrade { get; set; }
        public int[] SpawnedZones { get; set; } = Array.Empty<int>();
        public AEDV02SupplyReservation[] Reservations { get; set; } = Array.Empty<AEDV02SupplyReservation>();
        public bool IsValid() => ReservationGrade is >= -1 and <= 3 && InitialOptionalBudget is >= 0 and <= 2 && OptionalBudget >= OptionalMaterialized &&
            OptionalBudget <= InitialOptionalBudget && OptionalMaterialized >= 0 && SpawnedZones != null &&
            SpawnedZones.All(x => x is >= 1 and <= 3) && SpawnedZones.Distinct().Count() == SpawnedZones.Length &&
            Reservations != null && Reservations.Length <= 17 && Reservations.All(x => x != null && x.EntryPath != null && x.EntryPath.Length <= 256 &&
                x.EntryPath.All(p => p != null && p.Length == 3 && p.All(AEDV02PilotPolicy.Finite)) && x.Zone is >= 1 and <= 3 &&
                !SpawnedZones.Contains(x.Zone) && x.ToolId is 1 or 2 or 3 or 4 or 6 && !string.IsNullOrWhiteSpace(x.MarkerId) && x.MarkerId.Length <= 128) &&
            Reservations.Select(x => x.MarkerId).Distinct(StringComparer.Ordinal).Count() == Reservations.Length;
    }
    public sealed class AEDV02WorldSample
    {
        public const string SourceVersion = "AED_V02_WORLD_SOURCE_REV02_4";
        public string Version { get; set; } = SourceVersion;
        public string OpportunityContractVersion { get; set; } = string.Empty;
        public string ResolutionMode { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public long Tick { get; set; }
        public int TickRate { get; set; }
        public int PhaseOrdinal { get; set; }
        public string PhaseName { get; set; } = string.Empty;
        public int LogicalPhase { get; set; }
        public int Zone { get; set; }
        public int PlanRevision { get; set; }
        public string PlanFingerprint { get; set; } = string.Empty;
        public string RosterIdentity { get; set; } = string.Empty;
        public string SceneManifestFingerprint { get; set; } = string.Empty;
        public string EvidenceExclusion { get; set; } = string.Empty;
        public AEDV02PlayerSource[] Players { get; set; } = Array.Empty<AEDV02PlayerSource>();
        public AEDV02ThreatSource[] Stalkers { get; set; } = Array.Empty<AEDV02ThreatSource>();
        public AEDV02ThreatSource[] Minions { get; set; } = Array.Empty<AEDV02ThreatSource>();
        public AEDV02ToolSource[] Tools { get; set; } = Array.Empty<AEDV02ToolSource>();
        public AEDV02DoorSource[] Doors { get; set; } = Array.Empty<AEDV02DoorSource>();
        public AEDV02SupplySource? Supply { get; set; }

        public bool IsValid()
        {
            if (Version != SourceVersion || ResolutionMode != "Adaptive" || Difficulty != "Normal" || Tick < 0 || TickRate < 1 || TickRate > 240 || PhaseOrdinal < 1 ||
                OpportunityContractVersion == null || OpportunityContractVersion.Length != 0 && OpportunityContractVersion != AEDV02PilotOpportunities.Version ||
                string.IsNullOrWhiteSpace(PhaseName) || PhaseName.Length > 80 || LogicalPhase < 1 || LogicalPhase > 4 || LogicalPhase != AEDV02PhaseMap.LogicalPhase(PhaseName) ||
                Zone != Math.Min(LogicalPhase, 3) || PlanRevision < 0 || !Hash(PlanFingerprint) || !Hash(RosterIdentity) ||
                !Hash(SceneManifestFingerprint) || EvidenceExclusion == null || EvidenceExclusion.Length > 80 ||
                Players == null || Players.Length < 1 || Players.Length > 4 || Stalkers == null || Stalkers.Length > 8 ||
                Minions == null || Minions.Length > 16 || Tools == null || Tools.Length > 128 || Doors == null || Doors.Length > 512 ||
                Supply != null && !Supply.IsValid())
                return false;
            var users = Players.Select(x => x?.UserId ?? Guid.Empty).ToHashSet();
            if (users.Contains(Guid.Empty) || users.Count != Players.Length ||
                Players.Any(x => x.LifeState != "ALIVE" && x.LifeState != "DOWNED" && x.LifeState != "ELIMINATED" ||
                    x.ReviverUserId.HasValue && (!x.IsReviving || x.ReviverUserId == x.UserId || !users.Contains(x.ReviverUserId.Value)) ||
                    OpportunityContractVersion == AEDV02PilotOpportunities.Version && x.IsReviving && !x.ReviverUserId.HasValue ||
                    !ulong.TryParse(x.HideSpotId, out _) || x.HeldToolInstanceId == null || x.ObjectiveUnitId == null || x.ObjectiveCauseEntityId == null ||
                    x.ObjectiveCauseEventId == null || x.ResourceCauseEventId == null || x.SlowUntilTick < 0 ||
                    !Position(x.X, x.Y, x.Z) ||
                    x.ObjectiveState != "NONE" && x.ObjectiveState != "OPERATING" && x.ObjectiveState != "THREAT_INTERRUPTED" ||
                    x.RequiredToolId != 0 && x.RequiredToolId != 1 && x.RequiredToolId != 2 && x.RequiredToolId != 3 && x.RequiredToolId != 4 && x.RequiredToolId != 6 ||
                    (x.ObjectiveState != "NONE" && string.IsNullOrWhiteSpace(x.ObjectiveUnitId)) ||
                    (x.ObjectiveState == "THREAT_INTERRUPTED" && string.IsNullOrWhiteSpace(x.ObjectiveCauseEntityId)))) return false;
            if (!Threats(Stalkers, users, false) || !Threats(Minions, users, true)) return false;
            if (Tools.Any(x => x == null || !Text(x.InstanceId) || !Text(x.Origin) || x.Zone < 0 || x.Zone > 3 ||
                x.ToolId != 1 && x.ToolId != 2 && x.ToolId != 3 && x.ToolId != 4 && x.ToolId != 6 ||
                x.Location != "World" && x.Location != "Player" && x.Location != "Minion" ||
                x.RemainingCharges < 0 || x.CarrierMinionId == null || x.CarrierMinionId.Length > 128 ||
                x.CarrierMinionId.Length != 0 && (x.Location != "World" || x.OwnerUserId.HasValue || !Minions.Any(m => m.NetworkId == x.CarrierMinionId)) ||
                !Position(x.X, x.Y, x.Z) || (x.OwnerUserId.HasValue && !users.Contains(x.OwnerUserId.Value)) ||
                x.RouteEdgeIds == null || x.RouteEdgeIds.Length > 128 || x.RouteEdgeIds.Any(y => !Text(y))) ||
                Tools.Select(x => x.InstanceId).Distinct(StringComparer.Ordinal).Count() != Tools.Length) return false;
            return !Doors.Any(x => x == null || !Text(x.DoorId) ||
                x.State != "OPEN" && x.State != "CLOSED_UNLOCKED" && x.State != "LOCKED" && x.State != "BROKEN") &&
                Doors.Select(x => x.DoorId).Distinct(StringComparer.Ordinal).Count() == Doors.Length;
        }
        private bool Threats(AEDV02ThreatSource[] threats, HashSet<Guid> users, bool minion) =>
            threats.All(x => x != null && Text(x.NetworkId) && Text(x.State) && x.EpisodeId != null && x.EpisodeId.Length <= 128 && x.Zone >= 1 && x.Zone <= 3 &&
                x.ConsumerValues != null && (x.ConsumerValues.Length == 0 || !minion && x.ConsumerValues.Length == 6 &&
                    x.ConsumerValues.Select((value, index) => AEDV02PilotPolicy.Finite(value) &&
                        AEDV02Plan.AllowedValues((AEDV02Key)index).Any(allowed => Math.Abs(allowed - value) < .000001)).All(valid => valid)) &&
                Position(x.X, x.Y, x.Z) &&
                (!x.TargetUserId.HasValue || users.Contains(x.TargetUserId.Value)) &&
                (!x.TargetUserId.HasValue || x.State is not ("CHASE" or "ATTACK" or "TRACK" or "HARASS") ||
                    Players.Single(p => p.UserId == x.TargetUserId.Value).LifeState != "ALIVE" || Text(x.EpisodeId)) &&
                (minion ? x.State == "IDLE" || x.State == "TRACK" || x.State == "HARASS" || x.State == "FLEE" || x.State == "DYING"
                    : x.State == "PATROL" || x.State == "DETECTION" || x.State == "CHASE" || x.State == "ATTACK" || x.State == "RECOVER" || x.State == "SEARCH" || x.State == "SPECIAL")) &&
            threats.Select(x => x.NetworkId).Distinct(StringComparer.Ordinal).Count() == threats.Length;
        private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
        private static bool Position(double x, double y, double z) => AEDV02PilotPolicy.Finite(x) && AEDV02PilotPolicy.Finite(y) && AEDV02PilotPolicy.Finite(z) &&
            Math.Abs(x) <= 10000 && Math.Abs(y) <= 10000 && Math.Abs(z) <= 10000;
        public static bool Hash(string? value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
    }

    public sealed class AEDV02RouteEdge
    {
        public string Id { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public double PathMeters { get; set; }
        public string DoorId { get; set; } = string.Empty;
    }
    public sealed class AEDV02SceneMarker
    {
        public string Id { get; set; } = string.Empty;
        public int Zone { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string HideSpotId { get; set; } = string.Empty;
        public double[] InteractionBounds { get; set; } = Array.Empty<double>();
        public string ResourceRole { get; set; } = "Unclassified";
        public int[] AllowedToolIds { get; set; } = Array.Empty<int>();
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
    public sealed partial class AEDV02SceneManifest
    {
        public string SceneVersion { get; set; } = string.Empty;
        public string ScenePath { get; set; } = string.Empty;
        public double MaximumVisionRange { get; set; }
        public double MaximumCabinetInteractionDistance { get; set; }
        public double MinimumToolSpacing { get; set; } = -1;
        public AEDV02SceneMarker[] Markers { get; set; } = Array.Empty<AEDV02SceneMarker>();
        public AEDV02RouteEdge[] Edges { get; set; } = Array.Empty<AEDV02RouteEdge>();
        public AEDV02NavCell[] NavCells { get; set; } = Array.Empty<AEDV02NavCell>();
        public AEDV02NavLink[] NavLinks { get; set; } = Array.Empty<AEDV02NavLink>();
        public string[] DoorIds { get; set; } = Array.Empty<string>();

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(SceneVersion) || SceneVersion.Length > 128 || Markers == null || Markers.Length is < 1 or > 4096 ||
                Edges == null || Edges.Length > 65536 || NavCells == null || NavCells.Length > 100000 || NavLinks == null || NavLinks.Length > 600000 ||
                !AEDV02PilotPolicy.Finite(MaximumVisionRange) || MaximumVisionRange < 0 || MaximumVisionRange > 10000 ||
                !AEDV02PilotPolicy.Finite(MaximumCabinetInteractionDistance) || MaximumCabinetInteractionDistance < 0 || MaximumCabinetInteractionDistance > 100 ||
                !AEDV02PilotPolicy.Finite(MinimumToolSpacing) || MinimumToolSpacing < -1 || MinimumToolSpacing > 100 ||
                DoorIds == null || DoorIds.Length > 4096 || DoorIds.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 128) ||
                DoorIds.Distinct(StringComparer.Ordinal).Count() != DoorIds.Length ||
                Markers.Any(x => x == null || string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 128) || NavCells.Any(x => x == null || !x.IsValid())) return false;
            var markers = Markers.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var cells = NavCells.Select(x => x.Id).ToHashSet();
            if (markers.Count != Markers.Length || cells.Count != NavCells.Length) return false;
            var cellById = NavCells.ToDictionary(x => x.Id);
            return markers.Count == Markers.Length && cells.Count == NavCells.Length &&
            Markers.All(x => x.ResourceRole is "Unclassified" or "MainRoute" or "SideRoom" or "FirstRoom" or "ObjectiveRoute" &&
                x.Zone >= 1 && x.Zone <= 3 && x.Kind is "ENTRY" or "TOOL" or "OBJECTIVE" or "CABINET" or "AMBUSH_ENTRY" or "ROUTE" &&
                (x.Kind != "CABINET" || ulong.TryParse(x.HideSpotId, out var spot) && spot != 0 && x.InteractionBounds != null &&
                    x.InteractionBounds.Length == 6 && x.InteractionBounds.All(AEDV02PilotPolicy.Finite) &&
                    Enumerable.Range(0, 3).All(i => x.InteractionBounds[i] <= x.InteractionBounds[i + 3])) &&
                x.AllowedToolIds != null && x.AllowedToolIds.All(t => t is 1 or 2 or 3 or 4 or 6) &&
                AEDV02PilotPolicy.Finite(x.X) && AEDV02PilotPolicy.Finite(x.Y) && AEDV02PilotPolicy.Finite(x.Z)) &&
            Edges.All(x => x != null && !string.IsNullOrWhiteSpace(x.Id) &&
                markers.Contains(x.From) && markers.Contains(x.To) && x.From != x.To &&
                AEDV02PilotPolicy.Finite(x.PathMeters) && x.PathMeters > 0) && Edges.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == Edges.Length &&
            NavLinks.All(x => x != null && x.From != x.To && cells.Contains(x.From) && cells.Contains(x.To) &&
                cellById[x.From].SharesPortal(cellById[x.To]) && x.DoorIds != null && x.DoorIds.All(d => !string.IsNullOrWhiteSpace(d) && d.Length <= 128));
        }

        public bool HasReachableGeometry(double[] from, double[] to, AEDV02DoorSource[] doors)
            => TryReachableGeometry(from, to, doors, out var reachable) && reachable;

        public bool TryReachableGeometry(double[] from, double[] to, AEDV02DoorSource[] doors, out bool reachable)
        {
            reachable = false;
            if (from == null || to == null || doors == null || NavCells.Length == 0) return false;
            var starts = NavCells.Where(x => x.Contains(from)).Select(x => x.Id).ToHashSet();
            var ends = NavCells.Where(x => x.Contains(to)).Select(x => x.Id).ToHashSet();
            if (starts.Count == 0 || ends.Count == 0) return false;
            var doorIds = doors.Select(x => x.DoorId).ToHashSet(StringComparer.Ordinal);
            if (DoorIds.Any(x => !doorIds.Contains(x)) || NavCells.Any(x => x.DoorIds.Any(d => !doorIds.Contains(d))) ||
                NavLinks.Any(x => x.DoorIds.Any(d => !doorIds.Contains(d)))) return false;
            var open = doors.Where(x => x.State is "OPEN" or "BROKEN" or "CLOSED_UNLOCKED").Select(x => x.DoorId).ToHashSet(StringComparer.Ordinal);
            var usable = NavCells.Where(x => x.DoorIds.All(open.Contains)).Select(x => x.Id).ToHashSet();
            starts.IntersectWith(usable); ends.IntersectWith(usable);
            var links = NavLinks.Where(x => usable.Contains(x.From) && usable.Contains(x.To) && x.DoorIds.All(open.Contains))
                .GroupBy(x => x.From).ToDictionary(x => x.Key, x => x.Select(l => l.To).ToArray());
            var queue = new Queue<int>(starts);
            while (queue.Count != 0)
            {
                int cell = queue.Dequeue(); if (ends.Contains(cell)) { reachable = true; return true; }
                if (links.TryGetValue(cell, out var next)) foreach (var id in next) if (starts.Add(id)) queue.Enqueue(id);
            }
            return true;
        }

        public bool TryRoute(string from, string to, string[] edgeIds, AEDV02DoorSource[] doors, out double meters)
        {
            meters = 0;
            if (edgeIds == null || edgeIds.Length == 0 || edgeIds.Distinct(StringComparer.Ordinal).Count() != edgeIds.Length ||
                Edges == null || Edges.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() != 1) || doors == null) return false;
            var current = from;
            foreach (var id in edgeIds)
            {
                var edge = Edges.SingleOrDefault(x => x.Id == id);
                if (edge == null || edge.From != current || !AEDV02PilotPolicy.Finite(edge.PathMeters) || edge.PathMeters <= 0 ||
                    (!string.IsNullOrEmpty(edge.DoorId) && !doors.Any(x => x.DoorId == edge.DoorId && (x.State == "OPEN" || x.State == "BROKEN" || x.State == "CLOSED_UNLOCKED")))) return false;
                current = edge.To; meters += edge.PathMeters;
            }
            return current == to;
        }
    }

    public sealed class AEDV02NavLink
    {
        public int From { get; set; }
        public int To { get; set; }
        public string[] DoorIds { get; set; } = Array.Empty<string>();
    }
    public sealed class AEDV02NavCell
    {
        public int Id { get; set; }
        public double[] A { get; set; } = Array.Empty<double>();
        public double[] B { get; set; } = Array.Empty<double>();
        public double[] C { get; set; } = Array.Empty<double>();
        public string[] DoorIds { get; set; } = Array.Empty<string>();
        public bool IsValid() => Id >= 0 && Point(A) && Point(B) && Point(C) && AreaSquared() > .00000001 &&
            DoorIds != null && DoorIds.Length <= 64 && DoorIds.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 128) &&
            DoorIds.Distinct(StringComparer.Ordinal).Count() == DoorIds.Length;
        public bool IntersectsBounds(double[] min, double[] max)
        {
            if (!IsValid() || !Point(min) || !Point(max) || Enumerable.Range(0, 3).Any(i => min[i] > max[i])) return false;
            var polygon = new List<double[]> { A, B, C };
            for (int axis = 0; axis < 3; axis++)
                for (int side = 0; side < 2; side++)
                {
                    double boundary = side == 0 ? min[axis] : max[axis];
                    var clipped = new List<double[]>();
                    if (polygon.Count == 0) return false;
                    var previous = polygon[polygon.Count - 1];
                    foreach (var current in polygon)
                    {
                        bool inside = side == 0 ? current[axis] >= boundary : current[axis] <= boundary;
                        bool previousInside = side == 0 ? previous[axis] >= boundary : previous[axis] <= boundary;
                        if (inside != previousInside)
                        {
                            double t = (boundary - previous[axis]) / (current[axis] - previous[axis]);
                            clipped.Add(Enumerable.Range(0, 3).Select(i => previous[i] + t * (current[i] - previous[i])).ToArray());
                        }
                        if (inside) clipped.Add(current);
                        previous = current;
                    }
                    polygon = clipped;
                }
            return polygon.Count != 0;
        }
        private static bool Point(double[] p) => p != null && p.Length == 3 && p.All(x => AEDV02PilotPolicy.Finite(x) && Math.Abs(x) <= 10000);
        private double AreaSquared()
        {
            var u = Difference(B, A); var v = Difference(C, A);
            return Dot(u, u) * Dot(v, v) - Dot(u, v) * Dot(u, v);
        }
        public bool Contains(double[] point)
        {
            if (!Point(point) || !IsValid()) return false;
            var u = Difference(B, A); var v = Difference(C, A); var w = Difference(point, A);
            double uu = Dot(u, u), uv = Dot(u, v), vv = Dot(v, v), wu = Dot(w, u), wv = Dot(w, v);
            double denominator = uu * vv - uv * uv;
            double b = (vv * wu - uv * wv) / denominator, c = (uu * wv - uv * wu) / denominator;
            if (b < -.000001 || c < -.000001 || b + c > 1.000001) return false;
            // Item origins can be above the baked floor; horizontal projection must remain inside a real triangle.
            double error = 0;
            for (int i = 0; i < 3; i++) { double d = w[i] - b * u[i] - c * v[i]; error += d * d; }
            return error <= 1;
        }
        public bool SharesPortal(AEDV02NavCell other) => new[] { A, B, C }.Count(p => new[] { other.A, other.B, other.C }
            .Any(q => Enumerable.Range(0, 3).Sum(i => (p[i] - q[i]) * (p[i] - q[i])) <= .00000001)) >= 2;
        private static double[] Difference(double[] x, double[] y) => new[] { x[0] - y[0], x[1] - y[1], x[2] - y[2] };
        private static double Dot(double[] x, double[] y) => x[0] * y[0] + x[1] * y[1] + x[2] * y[2];
    }

    public sealed class AEDV02EffectSource
    {
        public Guid DecisionId { get; set; }
        public int PlanRevision { get; set; }
        public string PlanFingerprint { get; set; } = string.Empty;
        public string Consumer { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public double ObservedValue { get; set; }
        public long ObservedTick { get; set; }
        public string[] NetworkObjectIds { get; set; } = Array.Empty<string>();
        public bool IsValid() => DecisionId != Guid.Empty && PlanRevision >= 1 && AEDV02WorldSample.Hash(PlanFingerprint) &&
            (Consumer == "StalkerFusionRuntime" || Consumer == "PlayerSpawner" || Consumer == "TeamToolWorldSpawn" || Consumer == "AEDPolicyEnvelope") &&
            (Key == "SupportTool" && Consumer == "TeamToolWorldSpawn" && ObservedValue is 1 or 2 or 3 or 4 or 6 ||
                Enum.TryParse<AEDV02Key>(Key, out var key) && Enum.IsDefined(typeof(AEDV02Key), key) && key.ToString() == Key) && AEDV02PilotPolicy.Finite(ObservedValue) && ObservedTick >= 0 &&
            NetworkObjectIds != null && NetworkObjectIds.Length <= 64 && NetworkObjectIds.All(x => !string.IsNullOrWhiteSpace(x)) &&
            NetworkObjectIds.Distinct(StringComparer.Ordinal).Count() == NetworkObjectIds.Length;
    }
    public sealed class AEDV02ToolCarrierSource
    {
        public string ToolInstanceId { get; set; } = string.Empty;
        public string MinionNetworkId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public long Tick { get; set; }
        public bool IsValid() => !string.IsNullOrWhiteSpace(ToolInstanceId) && ToolInstanceId.Length <= 128 &&
            !string.IsNullOrWhiteSpace(MinionNetworkId) && MinionNetworkId.Length <= 128 && Action is "STEAL" or "DROP" && Tick >= 0;
    }
    public sealed class AEDV02CabinetClueSource
    {
        public Guid UserId { get; set; }
        public string StalkerNetworkId { get; set; } = string.Empty;
        public string HideSpotId { get; set; } = string.Empty;
        public string SceneMarkerId { get; set; } = string.Empty;
        public string SceneManifestFingerprint { get; set; } = string.Empty;
        public long Tick { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
        public double OriginZ { get; set; }
        public double TargetX { get; set; }
        public double TargetY { get; set; }
        public double TargetZ { get; set; }
        public double ObservedDistance { get; set; }
        public double PlayerX { get; set; }
        public double PlayerY { get; set; }
        public double PlayerZ { get; set; }
        public bool IsValid() => UserId != Guid.Empty && !string.IsNullOrWhiteSpace(StalkerNetworkId) && StalkerNetworkId.Length <= 128 &&
            ulong.TryParse(HideSpotId, out var spot) && spot != 0 && AEDV02WorldSample.Hash(SceneMarkerId) &&
            AEDV02WorldSample.Hash(SceneManifestFingerprint) && Tick >= 0 &&
            new[] { OriginX, OriginY, OriginZ, TargetX, TargetY, TargetZ, PlayerX, PlayerY, PlayerZ }.All(x => AEDV02PilotPolicy.Finite(x) && Math.Abs(x) <= 10000) &&
            AEDV02PilotPolicy.Finite(ObservedDistance) && ObservedDistance > 0 &&
            Math.Abs(ObservedDistance - Math.Sqrt(Math.Pow(OriginX - TargetX, 2) + Math.Pow(OriginY - TargetY, 2) + Math.Pow(OriginZ - TargetZ, 2))) <= .0001;
    }
}
