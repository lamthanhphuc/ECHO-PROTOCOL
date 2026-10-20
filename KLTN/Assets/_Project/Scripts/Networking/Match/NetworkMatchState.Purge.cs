using Fusion;
using UnityEngine;
using UnityEngine.AI;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.Gameplay;
using EchoProtocol.Tools.Scanner;

namespace EchoProtocol.Networking
{
    public static class PurgeFishcatBalance
    {
        private static readonly int[,] NormalCaps = { { 1, 2, 2, 3 }, { 2, 3, 4, 5 }, { 3, 4, 5, 6 }, { 3, 5, 6, 8 } };
        public static int Tier(int burned) => burned >= 8 ? 3 : burned >= 6 ? 2 : burned >= 3 ? 1 : 0;
        public static int Cap(int burned, int partySize, MatchDifficulty difficulty)
        {
            int normal = NormalCaps[Tier(burned), Mathf.Clamp(partySize, 1, 4) - 1];
            float scale = difficulty == MatchDifficulty.Easy ? .75f : difficulty == MatchDifficulty.Hard ? 1.25f : 1f;
            return Mathf.Max(1, Mathf.CeilToInt(normal * scale));
        }
        public static float SpawnCooldown(int burned) => Tier(burned) == 3 ? 16f : Tier(burned) == 2 ? 22f : Tier(burned) == 1 ? 28f : 35f;
        public const int HuntersPerPlayer = 2;
    }

    public sealed class PurgeFishcatRefillSchedule
    {
        private bool _hasSpawned;
        private float _refillStartedAt;
        private int _lastPopulation;
        public bool IsDue(float now, int population, int cap, float cooldown)
        {
            if (population < _lastPopulation || population >= cap) _refillStartedAt = now;
            _lastPopulation = population;
            return population < cap && (!_hasSpawned || now - _refillStartedAt >= cooldown);
        }
        public void Spawned(float now)
        {
            _hasSpawned = true;
            _refillStartedAt = now;
            _lastPopulation++;
        }
        public void Reset() { _hasSpawned = false; _refillStartedAt = 0f; _lastPopulation = 0; }
    }

    public enum PurgeBossMood { Calm, Warning, Hunting, Staggered }
    public struct PurgeAidSlot : INetworkStruct { public NetworkId Id; public TickTimer Refill; }
    public sealed partial class NetworkMatchState
    {
        [Networked, OnChangedRender(nameof(PurgeMoodChanged))] public PurgeBossMood PurgeMood { get; private set; }
        [Networked] public NetworkId PurgeBossId { get; private set; }
        [Networked] public TickTimer PurgeMoodTimer { get; private set; }
        [Networked] public TickTimer PurgeHuntTimer { get; private set; }
        [Networked] public TickTimer PurgeUvImmunity { get; private set; }
        [Networked] public float PurgeUvExposure { get; private set; }
        [Networked] public int PurgeStaggers { get; private set; }
        [Networked] public Vector3 PurgeRitualPosition { get; private set; }
        [Networked] public int PurgeRitualVersion { get; private set; }
        [Networked] public int PurgeAidAvailable { get; private set; }
        [Networked, Capacity(3)] public NetworkArray<PurgeAidSlot> PurgeAid => default;
        private StalkerFusionRuntime _purgeBoss;
        private static AudioClip _purgeWarningSound;
        public int PurgeRageTier => HuntBurned >= 8 ? 3 : HuntBurned / 3;
        [Networked] public int PurgePartySize { get; private set; }
        public int PurgeMinionCap => PurgeFishcatBalance.Cap(HuntBurned,
            Mathf.Max(PurgePartySize, CountPurgeGameplayPlayers()),
            Authority.MatchAuthorityRuntime.Instance ? Authority.MatchAuthorityRuntime.Instance.Difficulty : MatchDifficulty.Normal);
        public float PurgeMinionSpawnCooldown => PurgeFishcatBalance.SpawnCooldown(HuntBurned);
        private int CountPurgeGameplayPlayers()
        {
            int count = 0;
            if (Runner != null)
                foreach (var player in Runner.ActivePlayers)
                    if (Runner.TryGetPlayerObject(player, out var obj) && obj != null && obj.IsValid
                        && obj.TryGetComponent<LobbyPlayerState>(out var lobby) && lobby.IsGameplayPlayer) count++;
            return Mathf.Clamp(count, 1, 4);
        }
        public int PurgeStaggersRequired => PurgeRageTier + 1;
        public float PurgeCalmSeconds => new[] { 20f, 14f, 9f, 5f }[PurgeRageTier];
        public float PurgeHuntSeconds => 90f + Mathf.Max(0, HuntBurned - 1) * 15f;
        public float PurgeChaseSpeed
        {
            get
            {
                var d = Authority.MatchAuthorityRuntime.Instance ? Authority.MatchAuthorityRuntime.Instance.Difficulty : MatchDifficulty.Normal;
                float baseSpeed = d == MatchDifficulty.Easy ? 6f : d == MatchDifficulty.Hard ? 7.2f : 6.6f;
                float speed = baseSpeed + PurgeRageTier * .4f;
                return PurgeMood == PurgeBossMood.Hunting && PurgeUvExposure > 0 && PurgeUvImmunity.ExpiredOrNotRunning(Runner) ? speed * .6f : speed;
            }
        }
        public void BindPurgeBoss(NetworkObject boss)
        {
            if (!Object.HasStateAuthority || !boss || PurgeBossId==boss.Id) return;
            PurgeBossId = boss.Id; _purgeBoss = boss.GetComponent<StalkerFusionRuntime>();
            PurgeMood = PurgeBossMood.Calm; PurgeMoodTimer = TickTimer.None;
        }
        public void ProvokePurgeBoss()
        {
            if (!Object.HasStateAuthority || IsEnded || !AlienHuntMission.Instance || PurgeMood != PurgeBossMood.Calm
                || !PurgeUvImmunity.ExpiredOrNotRunning(Runner)
                || !PurgeMoodTimer.ExpiredOrNotRunning(Runner)) return;
            BeginPurgeHunt();
        }
        private void BeginPurgeHunt()
        {
            if (IsEnded) return;
            PurgeMood = PurgeBossMood.Warning; PurgeMoodTimer = TickTimer.CreateFromSeconds(Runner,3);
            PurgeHuntTimer = TickTimer.CreateFromSeconds(Runner,PurgeHuntSeconds); PurgeStaggers=0; PurgeUvExposure=0;
        }
        private void PurgeBurnCompleted(Vector3 position)
        {
            if (IsEnded) return;
            PurgeRitualPosition=position;PurgeRitualVersion++;
            if (PurgeMood == PurgeBossMood.Calm) BeginPurgeHunt();
            else PurgeHuntTimer = TickTimer.CreateFromSeconds(Runner,PurgeHuntSeconds);
        }
        public void PacifyPurgeBoss()
        {
            if (!Object.HasStateAuthority || IsEnded || !AlienHuntMission.Instance) return;
            PurgeMood = PurgeBossMood.Calm; PurgeMoodTimer=TickTimer.CreateFromSeconds(Runner,PurgeCalmSeconds);
            PurgeUvImmunity=TickTimer.CreateFromSeconds(Runner,Mathf.Min(5,PurgeCalmSeconds));
            PurgeUvExposure=0; PurgeStaggers=0;
        }
        private void TickPurgeDirector()
        {
            PurgePartySize = Mathf.Max(PurgePartySize, CountPurgeGameplayPlayers());
            if (!_purgeBoss && PurgeBossId.IsValid && Runner.TryFindObject(PurgeBossId,out var boss))
                _purgeBoss=boss.GetComponent<StalkerFusionRuntime>();
            if (_purgeBoss)
            {
                float delta=Runner.DeltaTime;
                if (PurgeMood==PurgeBossMood.Warning && PurgeMoodTimer.Expired(Runner)) PurgeMood=PurgeBossMood.Hunting;
                else if(PurgeMood==PurgeBossMood.Staggered && PurgeMoodTimer.Expired(Runner))
                {
                    if(PurgeStaggers>=PurgeStaggersRequired) PacifyPurgeBoss();
                    else { PurgeMood=PurgeBossMood.Hunting; PurgeUvImmunity=TickTimer.CreateFromSeconds(Runner,4); }
                }
                if(PurgeMood==PurgeBossMood.Hunting)
                {
                    if(PurgeHuntTimer.Expired(Runner)) { PacifyPurgeBoss(); }
                    else
                    {
                        int contributors=0;
                        foreach(var player in Runner.ActivePlayers)
                            if(Runner.TryGetPlayerObject(player,out var obj) && obj
                                && obj.TryGetComponent<NetworkPlayerFlashlight>(out var uv)
                                && uv.ResonanceActive && uv.PurgeBossTarget==_purgeBoss) contributors++;
                        if(PurgeUvImmunity.ExpiredOrNotRunning(Runner))
                            PurgeUvExposure=contributors>0 ? PurgeUvExposure+delta*ResonanceRules.ContributionSpeed(contributors)
                                : Mathf.Max(0,PurgeUvExposure-delta);
                        else PurgeUvExposure=0;
                        if(PurgeUvExposure>=2.25f)
                        {
                            PurgeStaggers++; PurgeUvExposure=0; PurgeMood=PurgeBossMood.Staggered;
                            PurgeMoodTimer=TickTimer.CreateFromSeconds(Runner,2.2f);
                        }
                    }
                }
            }
            if(_purgeBoss)
            {
                var controller=_purgeBoss.GetComponent<EchoProtocol.AI.Stalker.StalkerController>();
                controller.ApplyPurgePolicy(PurgeMood==PurgeBossMood.Hunting,
                    PurgeMood==PurgeBossMood.Warning||PurgeMood==PurgeBossMood.Staggered,PurgeChaseSpeed);
            }
            TickPurgeAid();
        }
        private void TickPurgeAid()
        {
            var mission=AlienHuntMission.Instance; if(!mission || !_teamToolPickupCatalog) return;
            var prefab=_teamToolPickupCatalog.GetPrefab(LobbyPlayerState.FirstAidKitToolId); if(!prefab)return;
            int available=0;
            for(int i=0;i<3;i++)
            {
                var slot=PurgeAid[i];
                bool exists=slot.Id.IsValid && Runner.TryFindObject(slot.Id,out var unused);
                NetworkObject pickupObject=null;
                if(exists)Runner.TryFindObject(slot.Id,out pickupObject);
                bool onFloor=pickupObject && ((pickupObject.TryGetComponent<NetworkTeamToolPickup>(out var teamPickup) && teamPickup.IsAvailable)
                    || (pickupObject.TryGetComponent<NetworkToolPickup>(out var pickup) && !pickup.IsPickedUp));
                if(onFloor){available++;continue;}
                if(slot.Id.IsValid && !slot.Refill.IsRunning) slot.Refill=TickTimer.CreateFromSeconds(Runner,45);
                if(slot.Refill.ExpiredOrNotRunning(Runner))
                {
                    var station=mission.Furnaces[i];
                    if(NavMesh.SamplePosition(station.transform.position+station.transform.right*2.5f,out var hit,3,NavMesh.AllAreas))
                    {
                        if(pickupObject)Runner.Despawn(pickupObject);
                        var spawned=Runner.Spawn(prefab,hit.position,Quaternion.identity);
                        slot.Id=spawned.Id; slot.Refill=TickTimer.None; available++;
                    }
                }
                PurgeAid.Set(i,slot);
            }
            PurgeAidAvailable=available;
            if(available==0)for(int i=0;i<3;i++)
            {
                var slot=PurgeAid[i];
                if(slot.Refill.RemainingTime(Runner)>15) {slot.Refill=TickTimer.CreateFromSeconds(Runner,15);PurgeAid.Set(i,slot);}
            }
        }
        private void PurgeMoodChanged()
        {
            if(!AlienHuntMission.Instance || PurgeMood!=PurgeBossMood.Warning) return;
            if(!_purgeWarningSound)
            {
                const int rate=22050;var samples=new float[rate];
                for(int i=0;i<samples.Length;i++){float t=i/(float)rate;samples[i]=Mathf.Sin(t*(220+120*t)*Mathf.PI*2)*Mathf.Sin(t*Mathf.PI)*.25f;}
                _purgeWarningSound=AudioClip.Create("PurgeHuntWarning",rate,1,rate,false);_purgeWarningSound.SetData(samples,0);
            }
            var listener=UnityEngine.Object.FindAnyObjectByType<AudioListener>();
            if(listener)AudioSource.PlayClipAtPoint(_purgeWarningSound,listener.transform.position,.7f);
        }
    }
}
