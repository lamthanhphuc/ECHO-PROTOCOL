using System;
using EchoProtocol.Networking;
using UnityEngine;

namespace EchoProtocol.MatchFlow
{
    /// <summary>Host selects a bounded random subset; each scene NetworkObject replicates its selection.</summary>
    public sealed class Zone3FuelSupply : MonoBehaviour
    {
        private bool _initialized;
        private bool _initializedOnline;
        public bool EnsureInitialized(bool online)
        {
            if (_initialized && _initializedOnline == online) return true;
            var cells = GetComponentsInChildren<Zone3FuelCell>(true);
            if (cells.Length == 0) return false;
            if (online)
                foreach (var cell in cells)
                    if (cell.Object == null || !cell.Object.IsValid || !cell.Object.HasStateAuthority) return false;
            Array.Sort(cells, (a, b) => string.CompareOrdinal(a.name, b.name));
            int required = Zone3FuelRules.RequiredCells(Zone3FuelRules.LongestSimpleRouteSegments());
            if (cells.Length < required)
            {
                Debug.LogError($"[Zone3Fuel] Need {required} candidates; scene has {cells.Length}.");
                return false;
            }
            for (int i = cells.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var swap = cells[i]; cells[i] = cells[j]; cells[j] = swap;
            }
            for (int i = 0; i < cells.Length; i++) cells[i].SetSelectedAuthoritative(i < required);
            _initializedOnline = online;
            _initialized = true;
            return true;
        }
        private void Update()
        {
            var match = NetworkMatchState.Instance;
            bool online = match != null && match.Object != null && match.Object.IsValid;
            if (!online && Application.isPlaying) EnsureInitialized(false);
        }
    }
}
