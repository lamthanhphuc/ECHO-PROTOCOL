using EchoProtocol.Networking;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Salvage
{
    public sealed class SalvageStation : NetworkInteractable
    {
        private EchoProtocol.Visuals.ObjectiveGlowHighlight _highlight;
        public override void Spawned()
        {
            _highlight = GetComponent<EchoProtocol.Visuals.ObjectiveGlowHighlight>()
                ?? gameObject.AddComponent<EchoProtocol.Visuals.ObjectiveGlowHighlight>();
            _highlight.OutlineMode = QuickOutline.Outline.Mode.OutlineVisible;
        }
        public override void Render()
        {
            base.Render();
            var run = SalvageRunState.Instance;
            if (_highlight != null) _highlight.SetHighlightActive(run != null
                && run.Phase != SalvagePhase.Ended && run.Phase != SalvagePhase.Failed);
        }
        public override string InteractionPrompt => EchoProtocol.Settings.GameLanguage.Choose("Máy điều hành thu hồi", "Salvage Terminal");
        public override bool EmitsRuntimeInteractionNoise => false;
        protected override void ExecuteInteraction(in InteractionContext context)
        {
            RPC_Open(context.Player);
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Open([RpcTarget] PlayerRef target) => SalvageHUD.Instance?.OpenTerminal(false);
    }
}
