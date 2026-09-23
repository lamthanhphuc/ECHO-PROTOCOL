using Fusion;
using UnityEngine;

namespace EchoProtocol.RelayA
{
    public sealed class RelayANetworkState : NetworkBehaviour
    {
        [SerializeField] private RelayAController controller;

        [Networked, OnChangedRender(nameof(ApplyOnlineState))]
        public NetworkBool RelayAOnline { get; private set; }

        public override void Spawned()
        {
            if (controller == null)
            {
                controller = GetComponent<RelayAController>();
            }

            if (Object.HasStateAuthority && controller != null)
            {
                controller.RelayAOnline += MarkOnlineAuthoritatively;
            }

            ApplyOnlineState();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (controller != null)
            {
                controller.RelayAOnline -= MarkOnlineAuthoritatively;
            }
        }

        private void MarkOnlineAuthoritatively()
        {
            if (Object != null && Object.HasStateAuthority)
            {
                RelayAOnline = true;
            }
        }

        private void ApplyOnlineState()
        {
            if (RelayAOnline && controller != null && !controller.IsOnline)
            {
                controller.ApplyOnlineFromAuthority();
            }
        }
    }
}
