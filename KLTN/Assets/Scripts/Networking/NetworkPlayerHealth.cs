using System;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>Minimal authoritative health state used by host-owned gameplay systems.</summary>
    public sealed class NetworkPlayerHealth : NetworkBehaviour
    {
        public static event Action<NetworkPlayerHealth> StateChanged;

        [SerializeField, Min(1)] private int maxHealth = 100;

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public int CurrentHealth { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkBool IsDowned { get; private set; }

        public int MaxHealth => maxHealth;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                CurrentHealth = Mathf.Max(1, maxHealth);
                IsDowned = false;
            }

            HandleReplicatedStateChanged();
        }

        public bool TryApplyAuthoritativeDamage(NetworkObject source, int amount)
        {
            if (!Object.HasStateAuthority
                || source == null
                || !source.HasStateAuthority
                || source.Runner != Runner
                || amount <= 0
                || IsDowned)
            {
                return false;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            IsDowned = CurrentHealth == 0;
            Debug.Log(
                $"[NetworkHealth] Player={Object.InputAuthority}, source={source.Id}, " +
                $"damage={amount}, health={CurrentHealth}, downed={IsDowned}.");
            HandleReplicatedStateChanged();
            return true;
        }

        private void HandleReplicatedStateChanged()
        {
            StateChanged?.Invoke(this);
        }
    }
}
