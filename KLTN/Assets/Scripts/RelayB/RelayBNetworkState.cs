using Fusion;
using UnityEngine;

namespace EchoProtocol.RelayB
{
    public sealed class RelayBNetworkState : NetworkBehaviour
    {
        [SerializeField] private RelayBController controller;

        [Networked, OnChangedRender(nameof(ApplyOnlineState))]
        public NetworkBool RelayBOnline { get; private set; }

        [Networked]
        public int ActivePresetIndex { get; private set; }

        [Networked]
        public PlayerRef CurrentOperator { get; private set; }

        public override void Spawned()
        {
            if (controller == null)
            {
                controller = GetComponent<RelayBController>();
            }

            if (Object.HasStateAuthority)
            {
                // Host picks preset
                if (controller != null && controller.Config != null && controller.Config.Presets.Count > 0)
                {
                    ActivePresetIndex = UnityEngine.Random.Range(0, controller.Config.Presets.Count);
                    controller.SetPresetIndex(ActivePresetIndex);
                }

                if (controller != null)
                {
                    controller.RelayBOnline += MarkOnlineAuthoritatively;
                    controller.ControlsChanged += HandleControlsChangedAuthoritatively;
                    controller.StartSyncRequested += HandleStartSyncAuthoritatively;
                    controller.CancelSyncRequested += HandleCancelSyncAuthoritatively;
                }
            }
            else
            {
                if (controller != null)
                {
                    controller.SetPresetIndex(ActivePresetIndex);
                }
            }

            ApplyOnlineState();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (controller != null)
            {
                controller.RelayBOnline -= MarkOnlineAuthoritatively;
                controller.ControlsChanged -= HandleControlsChangedAuthoritatively;
                controller.StartSyncRequested -= HandleStartSyncAuthoritatively;
                controller.CancelSyncRequested -= HandleCancelSyncAuthoritatively;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcRequestControl(PlayerRef player)
        {
            if (CurrentOperator == PlayerRef.None || CurrentOperator == player)
            {
                CurrentOperator = player;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcReleaseControl(PlayerRef player)
        {
            if (CurrentOperator == player)
            {
                CurrentOperator = PlayerRef.None;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcSubmitControls(PlayerRef sender, int channel, float freq, float phase)
        {
            if (CurrentOperator == PlayerRef.None || CurrentOperator == sender)
            {
                controller?.ApplyAuthoritativeControls(channel, freq, phase);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcStartSync(PlayerRef sender)
        {
            if (CurrentOperator == PlayerRef.None || CurrentOperator == sender)
            {
                controller?.StartSynchronization();
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcCancelSync(PlayerRef sender)
        {
            if (CurrentOperator == PlayerRef.None || CurrentOperator == sender)
            {
                controller?.CancelSynchronization();
            }
        }

        private void MarkOnlineAuthoritatively()
        {
            if (Object != null && Object.HasStateAuthority)
            {
                RelayBOnline = true;
            }
        }

        private void HandleControlsChangedAuthoritatively(int channel, float freq, float phase)
        {
            // Host keeps local state in sync
        }

        private void HandleStartSyncAuthoritatively()
        {
            // Host starts sync locally
        }

        private void HandleCancelSyncAuthoritatively()
        {
            // Host cancels sync locally
        }

        private void ApplyOnlineState()
        {
            if (RelayBOnline && controller != null && !controller.IsOnline)
            {
                controller.ApplyOnlineFromAuthority();
            }
        }
    }
}

