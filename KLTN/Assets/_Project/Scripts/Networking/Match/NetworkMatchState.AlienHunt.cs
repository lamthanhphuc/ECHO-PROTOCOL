using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public sealed partial class NetworkMatchState
    {
        [Networked, Capacity(AlienHuntMission.EntityCount)] public NetworkArray<HuntEntityState> HuntEntities => default;
        [Networked, Capacity(AlienHuntMission.AlienCount)] public NetworkArray<Vector3> HuntNestPositions => default;
        [Networked, Capacity(3)] public NetworkArray<HuntFurnaceState> HuntFurnaces => default;
        [Networked] public NetworkBool HuntReady { get; private set; }
        [Networked] public int HuntBurned { get; private set; }

        private void TickAlienHunt()
        {
            var hunt = AlienHuntMission.Instance;
            if (!hunt) return;
            if (!HuntReady) { hunt.Initialize(); HuntReady = true; }
            hunt.ApplyNetworkNestHomes();
            hunt.Tick(Runner.DeltaTime);
            if (!IsEnded) TickPurgeDirector();
        }
        public void RequestHuntAction(int entity, int furnace)
        {
            if (!HasValidNetworkObject()) return;
            if (Object.HasStateAuthority)
            { if (TryGetLocalRequester(out var actor)) TryHuntAction(actor, entity, furnace); }
            else RpcHuntAction(entity, furnace);
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcHuntAction(int entity, int furnace, RpcInfo info = default) => TryHuntAction(info.Source, entity, furnace);
        private void TryHuntAction(PlayerRef actor, int entity, int furnace)
        {
            if (!Object.HasStateAuthority || IsEnded || !HuntReady || !AlienHuntMission.Instance
                || !TryResolveActivePlayer(actor, out var life)) return;
            if (AlienHuntMission.Instance.Act(actor, life.transform, entity, furnace))
                EchoProtocol.Networking.Authority.MatchAuthorityRuntime.Instance?.RecordObjectiveContribution(actor);
        }
        public void RequestDebugHuntItem(HuntKind kind)
        {
            if (!HasValidNetworkObject() || (kind != HuntKind.Meat && kind != HuntKind.Jerrycan)) return;
            if (Object.HasStateAuthority)
            { if (TryGetLocalRequester(out var player)) TryDebugHuntItem(player, kind); }
            else RpcDebugHuntItem(kind);
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcDebugHuntItem(HuntKind kind, RpcInfo info = default) => TryDebugHuntItem(info.Source, kind);
        private void TryDebugHuntItem(PlayerRef player, HuntKind kind)
        {
            if (!Object.HasStateAuthority || IsEnded || !HuntReady ||
                (kind != HuntKind.Meat && kind != HuntKind.Jerrycan) ||
                !TryResolveActivePlayer(player, out var life) || !life.DebugGodMode) return;
            AlienHuntMission.Instance?.DebugGive(player, kind);
        }
        public void RecordHuntBurn(Vector3? ritualPosition = null)
        {
            if (!Object.HasStateAuthority || IsEnded || !AlienHuntMission.Instance) return;
            HuntBurned++;
            if (HuntBurned < AlienHuntMission.AlienCount) PurgeBurnCompleted(ritualPosition ?? AlienHuntMission.Instance.Furnaces[0].transform.position);
            if (HuntBurned >= AlienHuntMission.AlienCount)
                TryEndMatch(NetworkMatchResult.Win, NetworkMatchEndReason.ObjectiveCompleted, PlayerRef.None);
        }
    }
}
