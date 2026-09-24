using System.Collections.Generic;
using EchoProtocol.AI.Common;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Special
{
    public sealed class StalkerJumpEntryRegistry : MonoBehaviour
    {
        [SerializeField] private List<StalkerJumpEntryPoint> entries = new List<StalkerJumpEntryPoint>();

        private readonly Dictionary<string, AiSimulationTime> _lastUseByStableId =
            new Dictionary<string, AiSimulationTime>();

        public IReadOnlyList<StalkerJumpEntryPoint> Entries => entries;

        private void Awake()
        {
            RefreshCache();
        }

        public void RefreshCache()
        {
            entries.RemoveAll(entry => entry == null);
            var discovered = GetComponentsInChildren<StalkerJumpEntryPoint>(true);
            for (var i = 0; i < discovered.Length; i++)
            {
                if (discovered[i] != null && !entries.Contains(discovered[i]))
                {
                    entries.Add(discovered[i]);
                }
            }
        }

        public bool IsReuseEligible(StalkerJumpEntryPoint entry, AiSimulationTime now, float fallbackCooldown)
        {
            if (entry == null || !now.IsValid)
            {
                return false;
            }

            if (!_lastUseByStableId.TryGetValue(entry.StableId, out var lastUse) || !lastUse.IsValid)
            {
                return true;
            }

            var cooldown = Mathf.Max(entry.ReuseCooldownSeconds, fallbackCooldown);
            return now.Seconds - lastUse.Seconds >= cooldown;
        }

        public void MarkUsed(StalkerJumpEntryPoint entry, AiSimulationTime now)
        {
            if (entry != null && now.IsValid)
            {
                _lastUseByStableId[entry.StableId] = now;
            }
        }

        public void ResetForMatch() => _lastUseByStableId.Clear();
    }
}
