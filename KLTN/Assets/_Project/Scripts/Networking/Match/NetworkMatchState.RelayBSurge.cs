using EchoProtocol.Gameplay;
using EchoProtocol.MatchFlow;
using EchoProtocol.Networking.Authority;
using EchoProtocol.RelayB;
using Fusion;

namespace EchoProtocol.Networking
{
    public sealed partial class NetworkMatchState
    {
        [Networked] public RelayBSurgeTelemetry RelayB1Surge { get; private set; }
        [Networked] public RelayBSurgeTelemetry RelayB2Surge { get; private set; }

        private void TickRelayBSurgeAuthoritative(RelaySlot slot, RelayBController controller)
        {
            int difficulty=(int)(MatchAuthorityRuntime.Instance!=null ? MatchAuthorityRuntime.Instance.Difficulty : MatchDifficulty.Normal);
            controller.ConfigureSurge(difficulty,slot==RelaySlot.RelayB_2 ? 1 : 0);
            controller.TickAuthoritative(Runner.DeltaTime);
            CaptureRelayBSurge(slot,controller);
        }
        private void CaptureRelayBSurge(RelaySlot slot,RelayBController controller)
        {
            var state=RelayBSurgeTelemetry.From(controller.Surge);
            var snapshot=controller.Snapshot;
            if(slot==RelaySlot.RelayB_1) {
                RelayB1Surge=state;RelayB1Scanned=snapshot.HasScanned;RelayB1Channel=snapshot.SelectedChannelIndex;
                RelayB1Frequency=snapshot.CurrentFrequency;RelayB1Phase=snapshot.CurrentPhase;
            } else {
                RelayB2Surge=state;RelayB2Scanned=snapshot.HasScanned;RelayB2Channel=snapshot.SelectedChannelIndex;
                RelayB2Frequency=snapshot.CurrentFrequency;RelayB2Phase=snapshot.CurrentPhase;
            }
        }
        public bool RequestRelayBSurge(RelaySlot slot,int cell,int attempt,int seed)
        {
            if(!HasValidNetworkObject())return false;
            if(Object.HasStateAuthority)return TryGetLocalRequester(out var player) && TryRelayBSurgeAuthoritative(player,slot,cell,attempt,seed);
            RpcRelayBSurge((int)slot,cell,attempt,seed);return true;
        }
        [Rpc(RpcSources.All,RpcTargets.StateAuthority)]
        private void RpcRelayBSurge(int slot,int cell,int attempt,int seed,RpcInfo info=default)=>
            TryRelayBSurgeAuthoritative(info.Source,(RelaySlot)slot,cell,attempt,seed);

        private bool TryRelayBSurgeAuthoritative(PlayerRef player,RelaySlot slot,int cell,int attempt,int seed)
        {
            if(!IsRelayBSlot(slot) || !TryValidateRelayCommand(player,slot,out var target)
                || GetRelayOperator(slot)!=player)return false;
            var controller=(RelayBController)target;
            if(controller.Surge.Board==null || controller.Surge.Board.Seed!=seed
                || controller.Surge.Attempt!=attempt || controller.Simulation.IsSignalFound)return false;
            bool accepted=cell==-1 ? controller.StartSurge() : controller.PlaceSurgeInsulation(cell,attempt);
            if(!accepted)return false;
            CaptureRelayBSurge(slot,controller);HandleReplicatedStateChanged();return true;
        }
    }
}
