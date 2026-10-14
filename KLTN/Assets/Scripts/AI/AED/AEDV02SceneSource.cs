using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using UnityEngine;

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
            for (var current = item; current != null; current = current.parent) parts.Push(current.name + ":" + current.GetSiblingIndex());
            return AEDV02Plan.Hash(item.gameObject.scene.path + "/" + string.Join("/", parts));
        }
    }

    [Serializable] public sealed class AEDV02SceneManifestData
    {
        public string SceneVersion;
        public string ScenePath;
        public Marker[] Markers;
        public Edge[] Edges = Array.Empty<Edge>();
        public Cell[] NavCells;
        public Link[] NavLinks;
        [Serializable] public sealed class Marker { public string Id; public int Zone; public string Kind; public int[] AllowedToolIds = Array.Empty<int>(); public double X, Y, Z; }
        [Serializable] public sealed class Edge { public string Id, From, To, DoorId; public double PathMeters; }
        [Serializable] public sealed class Cell { public int Id; public double[] A, B, C; }
        [Serializable] public sealed class Link { public int From, To; public string[] DoorIds; }
        public AEDV02SceneManifest ToModel() => new AEDV02SceneManifest
        {
            SceneVersion = SceneVersion,
            ScenePath = ScenePath,
            Markers = Markers?.Select(x => x == null ? null : new AEDV02SceneMarker { Id = x.Id, Zone = x.Zone, Kind = x.Kind, AllowedToolIds = x.AllowedToolIds, X = x.X, Y = x.Y, Z = x.Z }).ToArray(),
            Edges = Edges?.Select(x => x == null ? null : new AEDV02RouteEdge { Id = x.Id, From = x.From, To = x.To, DoorId = x.DoorId, PathMeters = x.PathMeters }).ToArray(),
            NavCells = NavCells?.Select(x => x == null ? null : new AEDV02NavCell { Id = x.Id, A = x.A, B = x.B, C = x.C }).ToArray(),
            NavLinks = NavLinks?.Select(x => x == null ? null : new AEDV02NavLink { From = x.From, To = x.To, DoorIds = x.DoorIds }).ToArray()
        };
    }
}
