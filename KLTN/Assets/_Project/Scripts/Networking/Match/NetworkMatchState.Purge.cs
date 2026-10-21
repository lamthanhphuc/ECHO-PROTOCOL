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
        public static float UvDeathSeconds(int partySize, MatchDifficulty difficulty)
        {
            bool solo = partySize <= 1;
            if (difficulty == MatchDifficulty.Easy) return solo ? 1f : 2f;
            if (difficulty == MatchDifficulty.Hard) return solo ? 1.5f : 2.75f;
            return solo ? 1.25f : 2.5f;
        }
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
    public struct PurgeAidSlot : INetworkStruct
    {
        public NetworkId Id;
        public TickTimer Refill;
        public int LastSpawnIndex;
        public Vector3 LastPosition;
    }
    public sealed partial class NetworkMatchState
    {
        [Networked, OnChangedRender(nameof(PurgeMoodChanged))] public PurgeBossMood PurgeMood { get; private set; }
        [Networked] public NetworkId PurgeBossId { get; private set; }
        [Networked] public TickTimer PurgeMoodTimer { get; private set; }
        [Networked] public TickTimer PurgeHuntTimer { get; private set; }
        [Networked] public TickTimer PurgeUvImmunity { get; private set; }
        [Networked] public float PurgeUvExposure { get; private set; }
        [Networked] public int PurgeStaggers { get; private set; }
        [Networked] public int PurgeHuntsStarted { get; private set; }
        [Networked] public int PurgeHuntsEndedByUv { get; private set; }
        [Networked] public int PurgeHuntsEndedByTimeout { get; private set; }
        [Networked] public int PurgeUvStaggersTotal { get; private set; }
        [Networked] public float PurgeLastRitualElapsedSeconds { get; private set; }
        [Networked] public Vector3 PurgeRitualPosition { get; private set; }
        [Networked] public int PurgeRitualVersion { get; private set; }
        [Networked] public int PurgeAidAvailable { get; private set; }
        private const int PurgeAidCount = 9;
        [Networked, Capacity(PurgeAidCount)] public NetworkArray<PurgeAidSlot> PurgeAid => default;
        private StalkerFusionRuntime _purgeBoss;
        private static AudioClip _purgeWarningSound;
        public int PurgeRageTier => HuntBurned >= 8 ? 3 : HuntBurned / 3;
        [Networked] public int PurgePartySize { get; private set; }
        public int PurgeMinionCap => PurgeFishcatBalance.Cap(HuntBurned,
            Mathf.Max(PurgePartySize, CountPurgeGameplayPlayers()),
            Authority.MatchAuthorityRuntime.Instance ? Authority.MatchAuthorityRuntime.Instance.Difficulty : MatchDifficulty.Normal);
        public float PurgeMinionSpawnCooldown => PurgeFishcatBalance.SpawnCooldown(HuntBurned);
        public float PurgeFishcatUvDeathSeconds => PurgeFishcatBalance.UvDeathSeconds(
            Mathf.Max(PurgePartySize, CountPurgeGameplayPlayers()),
            Authority.MatchAuthorityRuntime.Instance ? Authority.MatchAuthorityRuntime.Instance.Difficulty : MatchDifficulty.Normal);
        public bool PurgeCoopAidEnabled => CountPurgeGameplayPlayers() >= 2;
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
            PurgeHuntsStarted++;
            PurgeMood = PurgeBossMood.Warning; PurgeMoodTimer = TickTimer.CreateFromSeconds(Runner,3);
            PurgeHuntTimer = TickTimer.CreateFromSeconds(Runner,PurgeHuntSeconds); PurgeStaggers=0; PurgeUvExposure=0;
        }
        private void PurgeBurnCompleted(Vector3 position)
        {
            if (IsEnded) return;
            PurgeRitualPosition=position;PurgeRitualVersion++;
            PurgeLastRitualElapsedSeconds = Mathf.Clamp(_matchDurationSeconds - Remaining(MatchTimer), 0f, _matchDurationSeconds);
            Debug.Log($"[PURGE_BALANCE] Ritual {HuntBurned}/{AlienHuntMission.AlienCount} at {PurgeLastRitualElapsedSeconds:F1}s; hunts={PurgeHuntsStarted}, uvEnds={PurgeHuntsEndedByUv}, timeouts={PurgeHuntsEndedByTimeout}, uvStaggers={PurgeUvStaggersTotal}");
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
        private void CompletePurgeHunt(bool byUv)
        {
            if (byUv) PurgeHuntsEndedByUv++;
            else PurgeHuntsEndedByTimeout++;
            Debug.Log($"[PURGE_BALANCE] Hunt ended by {(byUv ? "UV" : "timeout")}; hunts={PurgeHuntsStarted}, uvEnds={PurgeHuntsEndedByUv}, timeouts={PurgeHuntsEndedByTimeout}, uvStaggers={PurgeUvStaggersTotal}");
            PacifyPurgeBoss();
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
                    if(PurgeStaggers>=PurgeStaggersRequired) CompletePurgeHunt(true);
                    else { PurgeMood=PurgeBossMood.Hunting; PurgeUvImmunity=TickTimer.CreateFromSeconds(Runner,4); }
                }
                if(PurgeMood==PurgeBossMood.Hunting)
                {
                    if(PurgeHuntTimer.Expired(Runner)) { CompletePurgeHunt(false); }
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
                            PurgeStaggers++; PurgeUvStaggersTotal++; PurgeUvExposure=0; PurgeMood=PurgeBossMood.Staggered;
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
            if (!PurgeCoopAidEnabled)
            {
                // Remove only unclaimed supplies; never take a carried kit away.
                for (int i = 0; i < PurgeAidCount; i++)
                {
                    var slot = PurgeAid[i];
                    if (slot.Id.IsValid && Runner.TryFindObject(slot.Id, out var pickup) && pickup != null)
                    {
                        bool unclaimed = pickup.TryGetComponent<NetworkTeamToolPickup>(out var team) && team.IsAvailable
                            || pickup.TryGetComponent<NetworkToolPickup>(out var tool) && !tool.IsPickedUp;
                        if (unclaimed) Runner.Despawn(pickup);
                    }
                    PurgeAid.Set(i, default);
                }
                PurgeAidAvailable = 0;
                return;
            }
            var prefab = _teamToolPickupCatalog.GetPrefab(
                LobbyPlayerState.FirstAidKitToolId);

            if (!prefab) return;

            int available = 0;

            for (int i = 0; i < PurgeAidCount; i++)
            {
                var slot=PurgeAid[i];
                NetworkObject oldPickup = null;
                if (slot.Id.IsValid)
                    Runner.TryFindObject(slot.Id, out oldPickup);

                bool onFloor = oldPickup &&
                    ((oldPickup.TryGetComponent<NetworkTeamToolPickup>(out var teamTool) && teamTool.IsAvailable)
                    || (oldPickup.TryGetComponent<NetworkToolPickup>(out var pickup) && !pickup.IsPickedUp));

                if (onFloor)
                {
                    available++;
                    continue;
                }

                if (slot.Id.IsValid && !slot.Refill.IsRunning)
                    slot.Refill = TickTimer.CreateFromSeconds(Runner, 45f);

                if (slot.Refill.IsRunning && !slot.Refill.Expired(Runner))
                {
                    PurgeAid.Set(i, slot);
                    continue;
                }

                if (TryFindPurgeAidPosition(i, slot, out var position, out int spawnIndex))
                {
                    var spawned = Runner.Spawn(prefab, position, Quaternion.identity);
                    if (spawned)
                    {
                        if (oldPickup) Runner.Despawn(oldPickup);
                        slot.Id = spawned.Id;
                        slot.LastSpawnIndex = spawnIndex;
                        slot.LastPosition = position;
                        slot.Refill = TickTimer.None;
                        available++;
                    }
                }
                else slot.Refill = TickTimer.CreateFromSeconds(Runner, 3f);

                PurgeAid.Set(i, slot);
            }
            PurgeAidAvailable = available;
        }

        private bool TryFindPurgeAidPosition(int slotIndex, PurgeAidSlot slot, out Vector3 position, out int spawnIndex)
        {
            position = default;
            spawnIndex = -1;
            var mission = AlienHuntMission.Instance;
            if (!mission) return false;
            var path = new NavMeshPath();

            for (int attempt = 0; attempt < 180; attempt++)
            {
                int candidateIndex = Random.Range(0, 18);
                if (slot.Id.IsValid && candidateIndex == slot.LastSpawnIndex) continue;
                Vector3 origin = mission.Actors[9 + candidateIndex].Home;
                Vector2 offset = Random.insideUnitCircle * 5f;
                if (offset.sqrMagnitude < 4f) continue;
                Vector3 candidate = origin + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(candidate, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - origin.y) > 1.5f) continue;
                if (slot.Id.IsValid && Vector3.Distance(hit.position, slot.LastPosition) < 6f) continue;
                if (!NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * .35f, hit.position + Vector3.up * 1.1f, .35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                bool occupied = false;
                for (int j = 0; j < PurgeAidCount; j++)
                {
                    if (j == slotIndex) continue;
                    var other = PurgeAid[j];
                    if (!other.Id.IsValid || !Runner.TryFindObject(other.Id, out var obj)) continue;
                    bool active = obj.TryGetComponent<NetworkTeamToolPickup>(out var tool) && tool.IsAvailable;
                    if (!active && obj.TryGetComponent<NetworkToolPickup>(out var otherPickup)) active = !otherPickup.IsPickedUp;
                    if (active && Vector3.Distance(hit.position, obj.transform.position) < 12f) { occupied = true; break; }
                }
                if (occupied) continue;
                position = hit.position;
                spawnIndex = candidateIndex;
                return true;
            }
            return false;
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
