using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public sealed partial class NetworkMatchState
    {
        [Networked, Capacity(AlienHuntMission.EntityCount)] public NetworkArray<HuntEntityState> HuntEntities => default;
        [Networked, Capacity(AlienHuntMission.AlienCount)] public NetworkArray<Vector3> HuntNestPositions => default;
        [Networked, Capacity(3)] public NetworkArray<HuntFurnaceState> HuntFurnaces => default;
        [Networked, Capacity(AlienHuntMission.AlienCount)] private NetworkArray<NetworkBool> HuntCaptureCredited => default;
        [Networked, Capacity(3)] private NetworkArray<int> HuntFurnaceCreditMask => default;
        [Networked] public NetworkBool HuntReady { get; private set; }
        [Networked] public int HuntBurned { get; private set; }

        [Networked] public TickTimer LastAlienHintStart { get; private set; }

        private void TickAlienHunt()
        {
            var hunt = AlienHuntMission.Instance;
            if (!hunt) return;
            if (!HuntReady) { hunt.Initialize(); HuntReady = true; }
            hunt.ApplyNetworkNestHomes();
            hunt.Tick(Runner.DeltaTime);
            if (hunt.FindNearestUncaughtAlien(Vector3.zero) < 0)
                LastAlienHintStart = TickTimer.None;
            else if (!LastAlienHintStart.IsRunning)
                LastAlienHintStart = TickTimer.CreateFromSeconds(Runner, 180f);
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
            var before = furnace >= 0 && furnace < 3 ? HuntFurnaces[furnace] : default;
            if (AlienHuntMission.Instance.Act(actor, life.transform, entity, furnace))
            {
                if (entity >= 0 && entity < AlienHuntMission.AlienCount
                    && HuntEntities[entity].Stage == (int)AlienStage.Carried)
                    LastAlienHintStart = TickTimer.None;
                bool contribution = false;
                if (furnace < 0 && entity >= 0 && entity < AlienHuntMission.AlienCount && !HuntCaptureCredited[entity])
                {
                    HuntCaptureCredited.Set(entity, true);
                    contribution = true;
                }
                else if (furnace >= 0 && furnace < 3 && before.BurnedCount < AlienHuntMission.FurnaceCapacity)
                {
                    var after = HuntFurnaces[furnace];
                    int step = before.Fuel == 0 && after.Fuel > 0 ? 0
                        : before.AlienSlot == 0 && after.AlienSlot > 0 ? 1
                        : before.BurnRemaining <= 0 && after.BurnRemaining > 0 ? 2 : -1;
                    if (step >= 0)
                    {
                        int bit = 1 << (before.BurnedCount * 3 + step);
                        if ((HuntFurnaceCreditMask[furnace] & bit) == 0)
                        {
                            HuntFurnaceCreditMask.Set(furnace, HuntFurnaceCreditMask[furnace] | bit);
                            contribution = true;
                        }
                    }
                }
                if (contribution)
                    EchoProtocol.Networking.Authority.MatchAuthorityRuntime.Instance?.RecordObjectiveContribution(actor);
            }
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
        public void RequestDebugCompleteNextFurnace(
            int count = AlienHuntMission.FurnaceCapacity)
        {
            if (!HasValidNetworkObject() ||
                (count != 1 && count != AlienHuntMission.FurnaceCapacity))
                return;

            if (Object.HasStateAuthority)
            {
                if (TryGetLocalRequester(out var player))
                    TryDebugCompleteNextFurnace(player, count);
            }
            else
            {
                RpcDebugCompleteNextFurnace(count);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcDebugCompleteNextFurnace(
            int count,
            RpcInfo info = default)
        {
            TryDebugCompleteNextFurnace(info.Source, count);
        }

        private void TryDebugCompleteNextFurnace(
            PlayerRef player,
            int count)
        {
            if (!Object.HasStateAuthority ||
                IsEnded ||
                !HuntReady ||
                (count != 1 && count != AlienHuntMission.FurnaceCapacity) ||
                !TryResolveActivePlayer(player, out var life) ||
                !life.DebugGodMode)
                return;

            AlienHuntMission.Instance?.DebugCompleteNextFurnace(count);
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
