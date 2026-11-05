using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using UnityEngine;
using UnityEngine.SceneManagement;
using EchoProtocol.Networking;
using Fusion;

namespace EchoProtocol.AI.AED
{
    public static class AEDV02SceneSource
    {
        private static AEDV02SceneManifest _manifest;
        private static string _fingerprint;
        public static bool TryLoad(out AEDV02SceneManifest manifest, out string fingerprint)
        {
            manifest = _manifest; fingerprint = _fingerprint;
            if (_manifest != null) return true;
            var asset = Resources.Load<TextAsset>("AED/Rev02SceneManifest");
            if (asset == null) return false;
            try
            {
                manifest = JsonUtility.FromJson<AEDV02SceneManifestData>(asset.text)?.ToModel();
                if (manifest == null || !manifest.IsValid() || manifest.NavCells.Length == 0 || string.IsNullOrWhiteSpace(manifest.ScenePath)) return false;
                _manifest = manifest; _fingerprint = fingerprint = AEDV02Plan.Hash(asset.text); return true;
            }
            catch (ArgumentException) { return false; }
        }
        public static string MarkerId(Transform item)
        {
            var parts = new Stack<string>();
            for (var current = item; current != null; current = current.parent)
            {
                int index = current.parent == null
                    ? current.gameObject.scene.GetRootGameObjects().TakeWhile(x => x != current.gameObject).Count(x => x.name == current.name)
                    : current.GetSiblingIndex();
                parts.Push(current.name + ":" + index);
            }
            return AEDV02Plan.Hash(item.gameObject.scene.path + "/" + string.Join("/", parts));
        }
        public static Transform[] Zone2ExitGates(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return Array.Empty<Transform>();
            var roots = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Where(x => x.name == "DoorZone").ToArray();
            return roots.Length == 1 ? roots[0].GetComponentsInChildren<Transform>(true).Where(x => x.name == "LP_Bay_Door_snaps").Take(2).ToArray() : Array.Empty<Transform>();
        }
        public static bool TryGetDoorSources(NetworkRunner runner, NetworkMatchState matchState, out AEDV02DoorSource[] sources)
        {
            sources = Array.Empty<AEDV02DoorSource>();
            if (runner == null || !runner.IsServer || matchState == null || matchState.Runner != runner ||
                matchState.Object == null || !matchState.Object.IsValid || !matchState.Object.HasStateAuthority || !TryLoad(out var manifest, out _)) return false;
            var doors = new List<AEDV02DoorSource>();
            foreach (var door in UnityEngine.Object.FindObjectsByType<NetworkBehaviour>(FindObjectsInactive.Exclude).Where(x => x is INetworkDoorStateProvider && x.Runner == runner))
            {
                if (door.Object == null || !door.Object.IsValid || !door.Object.HasStateAuthority) return false;
                bool broken = door is NetworkSlidingDoor sliding && sliding.IsBroken || door is NetworkDoor normal && normal.IsBroken;
                bool jammed = door is NetworkSlidingDoor slidingDoor && slidingDoor.HasActiveJammer;
                var state = ((INetworkDoorStateProvider)door).State;
                doors.Add(new AEDV02DoorSource { DoorId = MarkerId(door.transform), State = jammed ? "LOCKED" : broken ? "BROKEN" :
                    state == NetworkDoorState.Open ? "OPEN" : state == NetworkDoorState.Locked ? "LOCKED" : "CLOSED_UNLOCKED" });
            }
            var zone2 = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance;
            var gates = Zone2ExitGates(SceneManager.GetSceneByPath(manifest.ScenePath));
            if (zone2 == null || gates.Length != 2 || gates[0].gameObject != zone2.DoorBlocker1 || gates[1].gameObject != zone2.DoorBlocker2) return false;
            foreach (var gate in gates) doors.Add(new AEDV02DoorSource { DoorId = MarkerId(gate), State = matchState.ZoneDoorsUnlocked ? "OPEN" : "LOCKED" });
            if (manifest.DoorIds.Any(x => doors.All(d => d.DoorId != x))) return false;
            sources = doors.ToArray(); return true;
        }
    }

    [Serializable] public sealed class AEDV02SceneManifestData
    {
        public string SceneVersion;
        public string ScenePath;
        public double MaximumVisionRange;
        public double MaximumCabinetInteractionDistance;
        public double MinimumToolSpacing = -1;
        public double CoreStabilizerRadius;
        public double CoreStabilizerDurationSeconds;
        public double JammerBreakSeconds;
        public double ScannerCoreRange;
        public double ScannerMotionRange;
        public double ScannerMovingThreshold;
        public double ScannerOcclusionMultiplier;
        public int ScannerMaxTargets;
        public double ScannerActiveSeconds;
        public double ScannerCycleSeconds;
        public Marker[] Markers;
        public Edge[] Edges = Array.Empty<Edge>();
        public Cell[] NavCells;
        public Link[] NavLinks;
        public string[] DoorIds = Array.Empty<string>();
        [Serializable] public sealed class Marker { public string Id; public int Zone; public string Kind; public string HideSpotId = string.Empty; public string ResourceRole = "Unclassified";
            public double[] InteractionBounds = Array.Empty<double>(); public int[] AllowedToolIds = Array.Empty<int>(); public double X, Y, Z; }
        [Serializable] public sealed class Edge { public string Id, From, To, DoorId; public double PathMeters; }
        [Serializable] public sealed class Cell { public int Id; public double[] A, B, C; public string[] DoorIds = Array.Empty<string>(); }
        [Serializable] public sealed class Link { public int From, To; public string[] DoorIds; }
        public AEDV02SceneManifest ToModel() => new AEDV02SceneManifest
        {
            SceneVersion = SceneVersion,
            ScenePath = ScenePath,
            MaximumVisionRange = MaximumVisionRange,
            MaximumCabinetInteractionDistance = MaximumCabinetInteractionDistance,
            MinimumToolSpacing = MinimumToolSpacing,
            CoreStabilizerRadius = CoreStabilizerRadius, CoreStabilizerDurationSeconds = CoreStabilizerDurationSeconds,
            JammerBreakSeconds = JammerBreakSeconds, ScannerCoreRange = ScannerCoreRange, ScannerMotionRange = ScannerMotionRange,
            ScannerMovingThreshold = ScannerMovingThreshold, ScannerOcclusionMultiplier = ScannerOcclusionMultiplier, ScannerMaxTargets = ScannerMaxTargets,
            ScannerActiveSeconds = ScannerActiveSeconds, ScannerCycleSeconds = ScannerCycleSeconds,
            DoorIds = DoorIds,
            Markers = Markers?.Select(x => x == null ? null : new AEDV02SceneMarker { Id = x.Id, Zone = x.Zone, Kind = x.Kind, HideSpotId = x.HideSpotId,
                InteractionBounds = x.InteractionBounds, ResourceRole = x.ResourceRole, AllowedToolIds = x.AllowedToolIds, X = x.X, Y = x.Y, Z = x.Z }).ToArray(),
            Edges = Edges?.Select(x => x == null ? null : new AEDV02RouteEdge { Id = x.Id, From = x.From, To = x.To, DoorId = x.DoorId, PathMeters = x.PathMeters }).ToArray(),
            NavCells = NavCells?.Select(x => x == null ? null : new AEDV02NavCell { Id = x.Id, A = x.A, B = x.B, C = x.C, DoorIds = x.DoorIds }).ToArray(),
            NavLinks = NavLinks?.Select(x => x == null ? null : new AEDV02NavLink { From = x.From, To = x.To, DoorIds = x.DoorIds }).ToArray()
        };
    }
}
