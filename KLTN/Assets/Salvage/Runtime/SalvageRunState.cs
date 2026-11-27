using System;
using System.Collections.Generic;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using EchoProtocol.AI.Listener.Noise;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Salvage
{
    public enum SalvageCommand { Select, Drop, Scan, Sell, Buy, Depart, Return, VoteEnd, Withdraw, SellAll, Store, Grab, Release, Pocket, BuyPower }

    public struct SalvageNetCargo : INetworkStruct
    {
        public int Serial, Definition, BaseValue, Integrity, Owner, Slot, State;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public NetworkBool InReceivingRoom;
        public SalvageCargo Cargo => new SalvageCargo { Serial = Serial, Definition = Definition,
            BaseValue = BaseValue, Integrity = Integrity, Owner = Owner, Slot = Slot, State = (SalvageCargoState)State };
        public static SalvageNetCargo From(SalvageCargo c, Vector3 position) => new SalvageNetCargo
        { Serial = c.Serial, Definition = c.Definition, BaseValue = c.BaseValue, Integrity = c.Integrity,
          Owner = c.Owner, Slot = c.Slot, State = (int)c.State, Position = position };
    }
    public struct SalvageCrew : INetworkStruct
    {
        public NetworkString<_64> User;
        public PlayerRef Actor;
        public int Selected, Tool, Uses, EndVote, PreviousLife;
        public uint LastSequence;
        public TickTimer ActionCooldown;
        public int SpeedRank, HealthRank, StaminaRank, RecoveryRank, StrengthRank, PowerDay, PowerPurchases;
        public float GripDistance;
        public Quaternion GripRotation;
        public Vector3 GripLocalPoint;
    }

    // The single authority for new Run state. Fusion owns the snapshot, including receipts and cargo IDs.
    public sealed partial class SalvageRunState : NetworkBehaviour
    {
        [Networked, Capacity(6)] public NetworkArray<int> ShopBought => default;
        [Networked] public int ShopDay {get;private set;}
        [Networked] public int CartPurchaseLevel {get;private set;}
        [Networked] public NetworkBool AccountRewardsDisabled {get;private set;}
        public void DisableAccountRewards(){if(Object!=null&&Object.IsValid&&Object.HasStateAuthority)AccountRewardsDisabled=true;}
        public int CompletedRewardLevels=>Math.Min(10000,Math.Min(CompletedRewardDays/3,Math.Max(0,SalvageLedger.CompletedLevelsFor(Cycle,Day,DayCompleted,SoldThisCycle,Quota)-RewardStartLevels)));
        public int CompletedRewardDays=>Math.Min(30003,Math.Max(0,SalvageLedger.CompletedDaysFor(Cycle,Day,DayCompleted)-RewardStartDays+RewardStartDays%3));
        public static SalvageRunState Instance { get; private set; }
        [Networked] public int PhaseCode { get; private set; }
        [Networked] public int Cycle { get; private set; }
        [Networked] public int Day { get; private set; }
        [Networked] public int Funds { get; private set; }
        [Networked] public int Quota { get; private set; }
        [Networked] public int SoldThisCycle { get; private set; }
        [Networked] public int TotalSold { get; private set; }
        [Networked] public int Revision { get; private set; }
        [Networked] public int NextSerial { get; private set; }
        [Networked] public int CrewAtCycleStart { get; private set; }
        [Networked] public NetworkBool DayCompleted { get; private set; }
        [Networked] public int LastDayRecovered { get; private set; }
        [Networked] public int LastDayLost { get; private set; }
        [Networked] public NetworkString<_64> RunId { get; private set; }
        [Networked] public TickTimer DayTimer { get; private set; }
        [Networked] public NetworkId StalkerId { get; private set; }
        [Networked] public int RunSeed { get; private set; }
        [Networked] public int MapSeed { get; private set; }
        [Networked] public int MapRevision { get; private set; }
        [Networked] public int MapVersion { get; private set; }
        [Networked] public int MapPlacementCount { get; private set; }
        [Networked] public int MapCycle { get; private set; }
        [Networked] public int MapDay { get; private set; }
        [Networked, Capacity(SalvageMapLayout.Capacity)] public NetworkArray<SalvageNetMapPlacement> MapPlacements => default;
        [Networked, Capacity(4)] public NetworkArray<int> MapAcknowledgements => default;
        [Networked, Capacity(SalvageLedger.Capacity)] public NetworkArray<SalvageNetCargo> Items => default;
        [Networked, Capacity(4)] public NetworkArray<SalvageCrew> Crew => default;
        public SalvagePhase Phase => (SalvagePhase)PhaseCode;
        public float SecondsRemaining => DayTimer.RemainingTime(Runner) ?? 0;
        public SalvageSite Site { get; private set; }
        public readonly Dictionary<int, SalvageScrap> WorldItems = new Dictionary<int, SalvageScrap>();
        // Keep the existing RPC payload stable and localize it on the receiving client.
        private const string CrewAtBaseFeedback = "Cả đội cần ở căn cứ để khởi hành.";
        private const string MapTimeoutFeedback = "Không dựng được map cho cả đội. Chưa bắt đầu chuyến; hãy tạo phòng mới.";
        private const string SavePendingFeedback = "Đang đồng bộ tiến độ trước khi khởi hành. Vui lòng thử lại sau vài giây.";
        private const string MapInvalidFeedback = "Không tạo được khu thu hồi có đường đi hợp lệ. Chưa bắt đầu chuyến; hãy thử lại hoặc tạo phòng mới.";
        private const string PreparingSiteFeedback = "Đang chuẩn bị khu thu hồi…";
        private const string ActionRejectedFeedback = "Chưa thể thực hiện. Kiểm tra vật phẩm và tập hợp cả đội ở căn cứ.";
        private const string NotEnoughCargoFeedback = "Khu thu hồi chưa đủ hàng. Ngày chưa bắt đầu; hãy bán bớt hàng trong căn cứ hoặc khởi hành lại để tạo khu khác.";
        internal const string EmptyStorageFeedback = "Kho chưa có hàng để bán.";
        internal static string FeedbackText(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            var labels = message switch
            {
                CrewAtBaseFeedback => (CrewAtBaseFeedback, "Everyone must be at base before departure."),
                MapTimeoutFeedback => ("Không tải được bản đồ cho cả đội. Hãy tạo phòng mới.", "The map could not load for the crew. Create a new room."),
                SavePendingFeedback => ("Đang đồng bộ tiến độ. Thử lại sau vài giây.", "Syncing progress. Try again in a few seconds."),
                MapInvalidFeedback => ("Không tạo được khu thu hồi. Thử lại hoặc tạo phòng mới.", "Site generation failed. Try again or create a new room."),
                PreparingSiteFeedback => (PreparingSiteFeedback, "Preparing the salvage site…"),
                ActionRejectedFeedback => (ActionRejectedFeedback, "Action unavailable. Check the cargo and gather the crew at base."),
                NotEnoughCargoFeedback => ("Khu thu hồi thiếu hàng. Bán bớt hàng ở căn cứ hoặc khởi hành lại.", "Not enough cargo at the site. Sell cargo at base or depart again."),
                EmptyStorageFeedback => (EmptyStorageFeedback, "There is no cargo to sell."),
                _ => ("Chưa thể thực hiện.", "Action unavailable.")
            };
            return EchoProtocol.Settings.GameLanguage.Choose(labels.Item1, labels.Item2);
        }

        public event Action<string> LocalFeedback;
        private SalvageLedger _ledger;
        private float _nextCrewRefresh;
        private uint _sequence;
        private bool _endSubmitted;
        private float _impactGraceUntil;
        private bool _departurePending;
        private float _mapDeadline;
        private int _mapAckSent;
        private int _failedMapRevision;
        public bool LocalMapReady => Site == null || Site.RandomMap == null
            || MapRevision > 0 && Site.RandomMap.BuiltRevision == MapRevision;

        public override void Spawned()
        {
            Instance = this; Site = FindAnyObjectByType<SalvageSite>();
            if(Object.HasStateAuthority&&Debug.isDebugBuild)AccountRewardsDisabled=true;
            _impactGraceUntil = Time.time + 3;
            if (!Object.HasStateAuthority) return;
            if (NetworkBootstrap.IsHostMigrationResumeActive) return;
            if (Revision > 0) RestoreLedger();
            else InitializeSavedRun();
        }
        private void StartFreshRun()
        {
            int count=0;foreach(var player in Runner.ActivePlayers)count++;
            _ledger=new SalvageLedger(count);RunId=Guid.NewGuid().ToString("N");Publish();RefreshCrew();SpawnStations();
            if(Site.RandomMap!=null)
            {RunSeed=Site.RandomMap.TestSeed!=0?Site.RandomMap.TestSeed:BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0);PrepareMap(Cycle,Day);}
        }
        public override void Despawned(NetworkRunner runner, bool hasState)
        { if (Instance == this) Instance = null; WorldItems.Clear(); }

        public int Seat(PlayerRef actor)
        { for (int i = 0; i < 4; i++) if (Crew[i].User.Length > 0 && Crew[i].Actor == actor) return i; return -1; }
        public NetworkObject Player(int seat)
        { return seat >= 0 && seat < 4 && Runner.TryGetPlayerObject(Crew[seat].Actor, out var obj) ? obj : null; }
        public SalvageCargo Cargo(int index) => Items[index].Cargo;
        public float Weight(int seat)
        { float weight = 0; for (int i = 0; i < Items.Length; i++) { var c = Cargo(i);
            if ((c.State == SalvageCargoState.Held || c.State == SalvageCargoState.PhysicsHeld) && c.Owner == seat) weight += SalvageLedger.Types[c.Definition].Weight; } return weight; }
        public int HeldIndex(int seat, int slot)
        { for (int i = 0; i < Items.Length; i++) { var c = Cargo(i);
            if (c.State == SalvageCargoState.Held && c.Owner == seat && (c.Slot == slot
                || SalvageLedger.Types[c.Definition].Heavy && c.Slot + 1 == slot)) return i; } return -1; }
        public bool HasHeavy(int seat)
        { if(PhysicsHeldIndex(seat)>=0)return true;for (int i = 0; i < Items.Length; i++) { var c = Cargo(i);
            if (c.State == SalvageCargoState.Held && c.Owner == seat && SalvageLedger.Types[c.Definition].Heavy) return true; } return false; }
        public static float MovementMultiplier(GameObject player)
        { var run = Instance; if (!SalvageSite.IsActive || run == null || run.Object == null || !run.Object.IsValid) return 1;
          var state = player.GetComponentInParent<LobbyPlayerState>(); int seat = state != null && state.Object != null && state.Object.IsValid ? run.Seat(state.Object.InputAuthority) : -1;
          return seat < 0 ? 1 : SalvageLedger.CarrySpeed(run.Weight(seat) / PowerMultiplier(player, SalvagePower.Strength)) * PowerMultiplier(player, SalvagePower.Speed); }
        public static bool AllowsSprint(GameObject player)
        { var run = Instance; if (!SalvageSite.IsActive || run == null || run.Object == null || !run.Object.IsValid) return true;
          var state = player.GetComponentInParent<LobbyPlayerState>(); return state == null || state.Object == null || !state.Object.IsValid || !run.HasHeavy(run.Seat(state.Object.InputAuthority)); }
        public static bool HasSelectedCargo(GameObject player)
        {
            var run = Instance; if (!SalvageSite.IsActive || run == null || run.Object == null || !run.Object.IsValid) return false;
            var state = player.GetComponentInParent<LobbyPlayerState>();
            int seat = state != null && state.Object != null && state.Object.IsValid ? run.Seat(state.Object.InputAuthority) : -1;
            return seat >= 0 && (run.PhysicsHeldIndex(seat)>=0 || run.HeldIndex(seat, run.Crew[seat].Selected) >= 0);
        }
        public static SalvageScrap SelectedCargo(GameObject player)
        {
            if (!HasSelectedCargo(player)) return null;
            var state = player.GetComponentInParent<LobbyPlayerState>();
            int seat = Instance.Seat(state.Object.InputAuthority);
            if(Instance.PhysicsHeldIndex(seat)>=0)return null;
            return Instance.WorldItems.TryGetValue(Instance.HeldIndex(seat, Instance.Crew[seat].Selected), out var item) ? item : null;
        }
        public static bool AllowsToolUse(GameObject player) => !SalvageSite.IsActive
            || Instance != null && Instance.Object != null && Instance.Object.IsValid
                && Instance.Phase == SalvagePhase.InDay && AllowsSprint(player);

        public override void FixedUpdateNetwork()
        {
            EnsureLocalMap();
            if(CloudLoading||CloudRecoveryRequired)return;
            if (!Object.HasStateAuthority || NetworkBootstrap.IsHostMigrationReconciliationPending) return;
            if (_ledger == null) RestoreLedger();
            UpdateDeliveries(); TickEnemies(); SaveCheckpoint();
            if (Time.time >= _nextCrewRefresh) { _nextCrewRefresh = Time.time + .25f; RefreshCrew(); TryEndByVotes(); }
            if (_departurePending)
            {
                bool ready=LocalMapReady;
                for(int i=0;i<4;i++) if(IsConnected(i)&&MapAcknowledgements[i]!=MapRevision) ready=false;
                if(ready)
                {
                    _departurePending=false;
                    if(AllCrewAtBase() && _ledger.Depart(ConnectedCrew())) { Publish();StartDay(); }
                    else { Publish();RPC_Feedback(Runner.LocalPlayer,CrewAtBaseFeedback); }
                }
                else if(Time.realtimeSinceStartup>_mapDeadline)
                { _departurePending=false;RPC_Feedback(Runner.LocalPlayer,MapTimeoutFeedback); }
            }
            if (Phase == SalvagePhase.InDay)
            {
                bool anyAlive = false;
                for (int seat = 0; seat < 4; seat++)
                {
                    var player = Player(seat); if (player == null) continue;
                    var life = player.GetComponent<NetworkPlayerLifeState>();
                    var state = player.GetComponent<LobbyPlayerState>(); var crew = Crew[seat];
                    if (life != null && crew.PreviousLife != (int)life.Status)
                    {
                        if (life.IsDowned) DropSeat(seat, false);
                        if (life.IsEliminated || life.Status == NetworkPlayerLifeStatus.Escaped) DropSeat(seat, true);
                        crew.PreviousLife = (int)life.Status; Crew.Set(seat, crew);
                    }
                    anyAlive |= state != null && !state.Disconnected && life != null && life.CanInitiateAction;
                }
                if (DayTimer.Expired(Runner) || !anyAlive) FinishDay();
            }
            if ((Phase == SalvagePhase.Ended || Phase == SalvagePhase.Failed) && !_endSubmitted)
            {
                _endSubmitted = true;
                var authority = MatchAuthorityRuntime.Instance;
                if(authority!=null&&authority.IsHostBinding)
                {
                    if(SalvagePlaytest.SkipBackendStart(LobbyMapCatalog.SalvageMode)) _=authority.EndAsync("SALVAGE_DEVELOPMENT_NO_REWARDS");
                    else authority.QueueMatchResult(Phase==SalvagePhase.Ended&&CompletedRewardLevels>0?"WIN":"LOSE",CompletedRewardLevels>0?1:0);
                }
            }
        }
        public override void Render() => EnsureLocalMap();
        private void PrepareMap(int cycle,int day)
        {
            var map=Site.RandomMap;
            int seed=unchecked(RunSeed ^ cycle*73856093 ^ day*19349663);
            int chosenSeed=seed;
            List<SalvageMapPlacement> placements=null;
            for(int attempt=0;attempt<12;attempt++)
            {
                chosenSeed=unchecked(seed+attempt*104729);
                try { var candidate=map.Plan(chosenSeed);if(map.Build(candidate,MapRevision+1)){placements=candidate;break;} }
                catch(InvalidOperationException) { }
            }
            if(placements==null)throw new InvalidOperationException("No walkable map found. "+map.LastError);
            MapSeed=chosenSeed;
            MapVersion=SalvageMapLayout.Version;
            for(int i=0;i<placements.Count;i++)MapPlacements.Set(i,SalvageNetMapPlacement.Encode(placements[i]));
            MapPlacementCount=placements.Count;MapCycle=cycle;MapDay=day;MapRevision++;
            for(int i=0;i<4;i++)MapAcknowledgements.Set(i,0);
            _mapAckSent=0;
        }
        private void EnsureLocalMap()
        {
            if(Site==null||Site.RandomMap==null||MapRevision==0||MapPlacementCount<2||Object==null||!Object.IsValid)return;
            var map=Site.RandomMap;
            if(map.BuiltRevision!=MapRevision)
            {
                if(MapVersion!=SalvageMapLayout.Version || _failedMapRevision==MapRevision)return;
                var plan=new List<SalvageMapPlacement>();for(int i=0;i<MapPlacementCount;i++)plan.Add(MapPlacements[i].Decode());
                if(!map.Build(plan,MapRevision)) { _failedMapRevision=MapRevision;Debug.LogError("[Salvage] Map rejected: "+map.LastError);return; }
            }
            int seat=Seat(Runner.LocalPlayer);
            if(seat>=0&&_mapAckSent!=MapRevision)
            { _mapAckSent=MapRevision;RPC_MapReady(MapRevision); }
        }
        [Rpc(RpcSources.All,RpcTargets.StateAuthority,HostMode=RpcHostMode.SourceIsHostPlayer)]
        private void RPC_MapReady(int revision,RpcInfo info=default)
        {
            int seat=Seat(info.Source);
            if(seat>=0&&info.Source.IsRealPlayer&&revision==MapRevision)MapAcknowledgements.Set(seat,revision);
        }

        private void RefreshCrew()
        {
            for(int i=0;i<4;i++)if(Crew[i].User.Length>0&&!IsConnected(i))
            {
                var old=Crew[i];_checkpointCrew[old.User.ToString()]=SavedCrew.From(old);
                DropSeat(i,true);Crew.Set(i,default);
            }
            foreach (var actor in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(actor, out var player) || !player.TryGetComponent<LobbyPlayerState>(out var state)) continue;
                string user = state.BackendUserId.ToString(); if (string.IsNullOrEmpty(user)) continue;
                int seat = -1, free = -1;
                for (int i = 0; i < 4; i++) { if (Crew[i].User.ToString() == user) seat = i; if (free < 0 && Crew[i].User.Length == 0) free = i; }
                if (seat < 0)
                {
                    seat = free; if (seat < 0) continue;
                    var saved=ResumeCrew(user);saved.Actor=actor;saved.Selected=0;
                    Crew.Set(seat,saved);
                    state.SetGameplayToolId(saved.Tool,saved.Uses); // Never inherit the account/Lobby loadout.
                }
                var crew = Crew[seat]; if(crew.Actor!=actor&&crew.Tool!=0)state.SetGameplayToolId(crew.Tool,crew.Uses); crew.Actor = actor; crew.Tool = state.ToolId; crew.Uses = state.TeamToolUsesRemaining;
                Crew.Set(seat, crew);
            }
        }
        private void SpawnStations()
        {
            var anchor = Site.TerminalPoint != null ? Site.TerminalPoint : Site.ReturnPoint;
            Runner.Spawn(Site.StationPrefab, anchor.position, anchor.rotation);
        }
        private void RestoreLedger()
        {
            _ledger = new SalvageLedger(CrewAtCycleStart);
            _ledger.Restore(Phase, Cycle, Day, Funds, Quota, SoldThisCycle, TotalSold, NextSerial,
                CrewAtCycleStart, DayCompleted, Revision, LastDayRecovered, LastDayLost);
            for (int i = 0; i < Items.Length; i++) _ledger.Cargo[i] = Cargo(i);
            _impactGraceUntil = Time.time + 3;
        }
        private void Publish()
        {
            PhaseCode = (int)_ledger.Phase; Cycle = _ledger.Cycle; Day = _ledger.Day; Funds = _ledger.Funds;
            Quota = _ledger.Quota; SoldThisCycle = _ledger.SoldThisCycle; TotalSold = _ledger.TotalSold;
            NextSerial = _ledger.NextSerial; CrewAtCycleStart = _ledger.CrewAtCycleStart;
            DayCompleted = _ledger.DayCompleted; Revision = Math.Max(1, _ledger.Revision);
            LastDayRecovered = _ledger.LastDayRecovered; LastDayLost = _ledger.LastDayLost;
            for (int i = 0; i < Items.Length; i++)
            { var pose=Items[i];var data=SalvageNetCargo.From(_ledger.Cargo[i],pose.Position);data.Rotation=pose.Rotation;data.Velocity=pose.Velocity;data.InReceivingRoom=pose.InReceivingRoom;Items.Set(i,data); }
            if(Phase==SalvagePhase.Failed)SaveCheckpoint(true);
        }
        public bool AdoptSnapshot()
        {
            if (!Object.HasStateAuthority || Revision < 1 || Site == null) return false;
            RestoreLedger(); WorldItems.Clear();
            foreach (var item in FindObjectsByType<SalvageScrap>(FindObjectsInactive.Include))
            { if (item.Runner == Runner && item.Object.IsValid && !WorldItems.TryAdd(item.Index, item)) return false; }
            for (int i = 0; i < Items.Length; i++)
                if ((Cargo(i).State == SalvageCargoState.World || Cargo(i).State == SalvageCargoState.Held || Cargo(i).State == SalvageCargoState.Stored || Cargo(i).State == SalvageCargoState.PhysicsHeld)
                    && (!WorldItems.TryGetValue(i, out var item) || item.Serial != Cargo(i).Serial)) return false;
            // Player objects contain the authoritative charge snapshot. The display cache may be
            // one refresh behind a use; writing it back would refund equipment during takeover.
            RefreshCrew();
            return true;
        }
        public void Request(SalvageCommand command, int argument = 0, int serial = 0)
        {
            if (Object == null || !Object.IsValid || !Runner.IsRunning) return;
            int seat = Seat(Runner.LocalPlayer); if (seat < 0) return;
            _sequence = Math.Max(_sequence, Crew[seat].LastSequence) + 1;
            RPC_Command((int)command, argument, serial, _sequence);
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPC_Command(int code, int argument, int serial, uint sequence, RpcInfo info = default)
        {
            PlayerRef actor = info.Source;
            int seat = Seat(actor);
            if (!TryActor(actor, out var player) || seat < 0 || !Enum.IsDefined(typeof(SalvageCommand), code)) return;
            var crew = Crew[seat]; if (sequence == 0 || sequence <= crew.LastSequence) return;
            crew.LastSequence = sequence; Crew.Set(seat, crew);
            var command = (SalvageCommand)code;
            if (command != SalvageCommand.Select && command != SalvageCommand.Release && command != SalvageCommand.Drop && !crew.ActionCooldown.ExpiredOrNotRunning(Runner)) return;
            crew.ActionCooldown = TickTimer.CreateFromSeconds(Runner, .12f); Crew.Set(seat, crew);
            bool accepted = false;
            switch (command)
            {
                case SalvageCommand.BuyPower:
                    accepted = BuyPower(seat, argument, serial);if(accepted)RPC_LocalCue(actor,4,player.transform.position);break;
                case SalvageCommand.Select:
                    if (argument >= 0 && argument < 5 && !HasHeavy(seat)) { crew.Selected = argument; Crew.Set(seat, crew); accepted = true; } break;
                case SalvageCommand.Drop:
                    accepted = ReleaseGrip(seat) || DropSelected(seat); break;
                case SalvageCommand.Release:
                    accepted=ReleaseGrip(seat);break;
                case SalvageCommand.Grab:
                    accepted=BeginGrip(actor,argument,serial);break;
                case SalvageCommand.Pocket:
                    accepted=PocketGrip(actor);if(accepted)RPC_LocalCue(actor,11,player.transform.position);break;
                case SalvageCommand.Store:
                    accepted = Deposit(actor); break;
                case SalvageCommand.Sell:
                    if (AtTerminal(player,true) && PrepareReceivedSale(argument,serial) && _ledger.TrySell(argument, serial)) { Publish(); RetireExhaustedActors(); accepted = true; RPC_Cue(3, player.transform.position, 0); } break;
                case SalvageCommand.SellAll:
                    if (AtTerminal(player,true) && SellAllReceived(argument, serial))
                    { Publish(); RetireExhaustedActors(); accepted = true; RPC_Cue(3, player.transform.position, 0); } break;
                case SalvageCommand.Buy:
                    if (BuyEquipment(player,argument)) { RefreshCrew(); Publish(); accepted=true; RPC_LocalCue(actor,4,player.transform.position); } break;
                case SalvageCommand.Depart:
                    if (Runner.IsServer && actor == Runner.LocalPlayer && AllCrewAtBase())
                    {
                        if(!CheckpointSynced)
                        {SaveCheckpoint(true);RPC_Feedback(actor,SavePendingFeedback);return;}
                        if(Site.RandomMap!=null)
                        {
                            if(_departurePending) { accepted=true;break; }
                            int day=_ledger.DayCompleted?_ledger.Day+1:_ledger.Day;
                            int cycle=_ledger.Cycle;
                            // Let the ledger resolve a Cycle deadline before creating the next collection area.
                            if(day>SalvageLedger.DaysPerCycle)
                            { _ledger.Depart(ConnectedCrew());Publish();accepted=true;break; }
                            if(MapDay!=day||MapCycle!=cycle)
                            {
                                try { PrepareMap(cycle,day); }
                                catch(InvalidOperationException error)
                                {
                                    Debug.LogError("[Salvage] Departure map failed: "+error.Message);
                                    RPC_Feedback(actor,MapInvalidFeedback);
                                    return;
                                }
                            }
                            _departurePending=true;_mapDeadline=Time.realtimeSinceStartup+45;
                            RPC_Feedback(actor,PreparingSiteFeedback);accepted=true;break;
                        }
                        accepted = _ledger.Depart(ConnectedCrew()); Publish();
                        if (accepted) StartDay();
                        else if (Phase == SalvagePhase.CycleSummary || Phase == SalvagePhase.Failed) accepted = true;
                    } break;
                case SalvageCommand.Return:
                    if (Phase == SalvagePhase.InDay && Site.Contains(player.transform.position) && AllCrewAtBase())
                    { FinishDay(); accepted = true; } break;
                case SalvageCommand.VoteEnd:
                    if (Phase != SalvagePhase.InDay && Site.Contains(player.transform.position))
                    { crew = Crew[seat]; crew.EndVote = 1; Crew.Set(seat, crew); accepted = true;
                      TryEndByVotes(); } break;
                case SalvageCommand.Withdraw:
                    if (AtTerminal(player, true) && _ledger.TryWithdraw(argument, serial, seat, out var selected))
                    { crew = Crew[seat]; crew.Selected = selected; Crew.Set(seat, crew); Publish(); accepted = true; } break;
            }
            RPC_Feedback(actor, accepted ? "" : ActionRejectedFeedback);
        }
        private bool TryActor(PlayerRef actor, out NetworkObject player)
        {
            player = null;
            if (!Object.HasStateAuthority || NetworkBootstrap.IsHostMigrationReconciliationPending
                || !actor.IsRealPlayer || !Runner.TryGetPlayerObject(actor, out player) || player.InputAuthority != actor) return false;
            var state = player.GetComponent<LobbyPlayerState>(); var life = player.GetComponent<NetworkPlayerLifeState>();
            return state != null && state.IsGameplayPlayer && !state.Disconnected && life != null && life.CanInitiateAction;
        }
        private bool IsConnected(int seat)
        { var player = Player(seat); var state = player != null ? player.GetComponent<LobbyPlayerState>() : null;
          return state != null && !state.Disconnected; }
        private int ConnectedCrew() { int count = 0; for (int i = 0; i < 4; i++) if (IsConnected(i)) count++; return count; }
        private void TryEndByVotes()
        {
            if (_ledger == null || Phase == SalvagePhase.InDay || Phase == SalvagePhase.Failed || Phase == SalvagePhase.Ended
                || ConnectedCrew() == 0 || !AllCrewAtBase()) return;
            for (int i = 0; i < 4; i++) if (IsConnected(i) && Crew[i].EndVote == 0) return;
            if (_ledger.End()) Publish();
        }
        private bool AllCrewAtBase()
        {
            for (int i = 0; i < 4; i++) if (IsConnected(i))
            { var obj = Player(i); var life = obj.GetComponent<NetworkPlayerLifeState>();
              if (life != null && !life.IsEliminated && (!life.CanInitiateAction || !Site.Contains(obj.transform.position))) return false; }
            return true;
        }
        private bool AtTerminal(NetworkObject player, bool duringDay = false) => Site != null && (duringDay || Phase != SalvagePhase.InDay)
            && Phase != SalvagePhase.Ended && Phase != SalvagePhase.Failed
            && Site.Contains(player.transform.position);

        public bool Pickup(PlayerRef actor, int index, int serial)
        {
            int seat = Seat(actor);
            if (!TryActor(actor, out var player) || seat < 0 || _ledger == null
                || !WorldItems.TryGetValue(index, out var item) || item.Serial != serial
                || Vector3.Distance(player.transform.position, item.transform.position) > 3.5f
                ) return false;
            if(!_ledger.TryPickup(index,serial,seat,out int selected))return BeginGrip(actor,index,serial);
            var crew = Crew[seat]; crew.Selected = selected; Crew.Set(seat, crew);
            item.LastHandler = actor;
            Publish(); RPC_Cue(0, player.transform.position, (int)SalvageLedger.Types[Cargo(index).Definition].Material); return true;
        }
        public bool Deposit(PlayerRef actor)
        {
            if (!TryActor(actor, out var player) || !Site.Contains(player.transform.position) || _ledger == null) return false;
            _receivingReservations.Clear();
            int count=0;
            for(int i=0;i<Items.Length;i++)if(Cargo(i).State==SalvageCargoState.Held&&Cargo(i).Owner==Seat(actor))
            {
                var point=BagDeliveryPosition(i);if(!float.IsFinite(point.x))continue;
                SetCargoPose(i,point,Quaternion.identity,Vector3.zero);if(_ledger.StoreItem(i,Seat(actor)))count++;
            }
            if(count==0)return false;
            Publish(); RPC_Cue(1, Site.ReturnPoint.position, 0); return true;
        }
        private bool DropSelected(int seat)
        {
            if (Phase != SalvagePhase.InDay) return false;
            int index = HeldIndex(seat, Crew[seat].Selected);
            return index >= 0 && Drop(index, seat);
        }
        private void DropSeat(int seat, bool all)
        {
            ReleaseGrip(seat);
            int active = HeldIndex(seat, Crew[seat].Selected);
            for (int i = 0; i < Items.Length; i++)
            { var c = Cargo(i); if (c.State == SalvageCargoState.Held && c.Owner == seat && (all || i == active || SalvageLedger.Types[c.Definition].Heavy)) Drop(i, seat, false); }
            if (all || Crew[seat].Selected == 4)
            { var obj = Player(seat); obj?.GetComponent<NetworkPlayerInteractor>()?.DropTeamToolAuthoritative(Crew[seat].Actor); RefreshCrew(); }
        }
        public void DropForLifeTransition(PlayerRef actor, bool eliminated)
        { int seat = Seat(actor); if (Object != null && Object.HasStateAuthority && seat >= 0 && Phase == SalvagePhase.InDay) DropSeat(seat, eliminated); }
        private void RetireExhaustedActors()
        {
            for (int i = 0; i < Items.Length; i++)
                if ((Cargo(i).State == SalvageCargoState.Sold || Cargo(i).State == SalvageCargoState.Destroyed)
                    && WorldItems.TryGetValue(i, out var actor) && actor != null && actor.Object.IsValid) Runner.Despawn(actor.Object);
        }
        private bool Drop(int index, int seat, bool allowPlacement=true)
        {
            var player = Player(seat); if (player == null) return false;
            Vector3 origin=GripEye(player),direction=allowPlacement?GripLook(player)*Vector3.forward:player.transform.forward;
            Quaternion rotation=Quaternion.Euler(0,player.transform.eulerAngles.y,0);
            Vector3 position=origin+direction*.8f;
            bool placing=allowPlacement&&direction.y<-.25f;
            var hits=Physics.RaycastAll(origin,direction,3.5f,~0,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            bool surface=false;
            foreach(var hit in hits)
            {
                if(hit.collider.GetComponentInParent<LobbyPlayerState>()!=null
                    ||hit.collider.GetComponentInParent<SalvageScrap>() is SalvageScrap own&&own.Index==index)continue;
                if(placing)
                {
                    if(hit.normal.y<.65f)return false;
                    position=hit.point+Vector3.up*.04f;surface=true;
                }
                else if(hit.distance<1)position=hit.point-direction*.3f;
                break;
            }
            if(placing)
            {
                if(!surface)return false;
                var size=Site.ScrapSize(Cargo(index).Definition);
                foreach(var collider in Physics.OverlapBox(position+Vector3.up*(size.y*.5f+.02f),size*.49f,rotation,~0,QueryTriggerInteraction.Ignore))
                    if(!(collider.GetComponentInParent<SalvageScrap>() is SalvageScrap own&&own.Index==index))return false;
            }
            if(!_ledger.TryDrop(index,Cargo(index).Serial,seat))return false;
            Publish();SetCargoPose(index,position,rotation,Vector3.zero);return true;
        }
        public void SetPosition(int index, Vector3 position)
        {
            if (!Object.HasStateAuthority || index < 0 || index >= Items.Length) return;
            var data = Items[index]; data.Position = position; Items.Set(index, data);
        }
        public bool Impact(int index, int serial, float speed, Vector3 position,bool physicsImpact=false)
        {
            if (!Object.HasStateAuthority || Time.time < _impactGraceUntil || _ledger == null
                ||!_ledger.IsItem(index,serial)) return false;
            var cargo=Cargo(index);var type=SalvageLedger.Types[cargo.Definition];
            int amount=SalvageCollisionDamage.IntegrityLoss(speed,type.Weight,type.Fragile,type.Material);
            // The call comes from this host's collision callback, never a client damage RPC.
            if (_ledger.Damage(index, serial, amount,physicsImpact))
            {
                Publish();int value=Cargo(index).Value;
                RPC_CargoValueLoss(index,serial,position,value,cargo.Value-value,(int)type.Material);
                if(WorldItems.TryGetValue(index,out var handled)&&handled.LastHandler.IsValid)
                    Noise(handled.LastHandler,RuntimeNoiseType.CORE_DROP,position,"cargo-contact:"+serial);
                RetireExhaustedActors();return true;
            }
            return false;
        }
        [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
        private void RPC_CargoValueLoss(int index,int serial,Vector3 position,int value,int loss,int material)
        {
            Transform follow=null;
            if(WorldItems.TryGetValue(index,out var item)&&item!=null&&item.Serial==serial)
            {item.ShowCargoValue(value,loss);}
            else SalvageValuePopup.Show(position,value,loss,follow,serial);
            SalvagePresentation.Impact(position,(SalvageCargoMaterial)material,loss,value==0);
        }
        public void Contact(int index, int serial, Vector3 position)
        {
            if (!Object.HasStateAuthority || index < 0 || index >= Items.Length || Cargo(index).Serial != serial
                || (Cargo(index).State != SalvageCargoState.World && Cargo(index).State != SalvageCargoState.PhysicsHeld)) return;
            RPC_Cue(2, position, (int)SalvageLedger.Types[Cargo(index).Definition].Material);
            if (WorldItems.TryGetValue(index, out var item) && item.LastHandler.IsValid)
                Noise(item.LastHandler, RuntimeNoiseType.CORE_DROP, position, "cargo-contact:" + serial);
        }
        public void LoseWorldCargo(int index, int serial)
        {
            if (!Object.HasStateAuthority || _ledger == null || !WorldItems.TryGetValue(index, out var item)
                || item.transform.position.y >= -15 || !_ledger.LoseWorldCargo(index, serial)) return;
            Publish(); RetireExhaustedActors();
        }
        private void StartDay()
        {
            SpawnCorridorDoors();
            DayTimer = TickTimer.CreateFromSeconds(Runner, SalvageLedger.DaySeconds); _impactGraceUntil = Time.time + 2;
            for (int i = 0; i < 4; i++)
            {
                var player = Player(i); if (player == null) continue;
                player.GetComponent<NetworkPlayerLifeState>()?.ResetForMatchAuthoritative();
                player.GetComponent<NetworkCharacterController>()?.Teleport(Site.CrewPosition(i), Site.CrewSpawn.rotation);
                var crew = Crew[i]; crew.EndVote = 0; crew.PreviousLife = 0; Crew.Set(i, crew);
            }
            // Weighted loot on fixed authored anchors. A repair margin keeps the Day's recoverable budget above quota.
            int spawnedValue = 0; int needed=Mathf.Max(0,Quota-SoldThisCycle-_ledger.StoredValue()); int target = Mathf.CeilToInt(needed * 1.5f);
            var lootRandom=new System.Random(unchecked(MapSeed ^ Cycle*83492791 ^ Day*19349663));
            var anchors=(Transform[])Site.ScrapSpawns.Clone();
            for(int i=anchors.Length-1;i>0;i--){int j=lootRandom.Next(i+1);var swap=anchors[i];anchors[i]=anchors[j];anchors[j]=swap;}
            int count = Mathf.Min(Site.ScrapSpawns.Length * 3, 96);
            for (int n = 0; n < count; n++)
            {
                var eligible=new List<int>();
                for(int k=0;k<SalvageLedger.CartDefinition;k++)if(SalvageLedger.MinimumLevel(k)<=Cycle&&Site.CanTransport(k)
                    &&(Cycle>=3||Mathf.Max(Site.ScrapSize(k).x,Site.ScrapSize(k).y,Site.ScrapSize(k).z)<=1.8f))eligible.Add(k);
                if(eligible.Count==0)break;
                int definition=n<48?eligible[lootRandom.Next(eligible.Count)]:2;
                var type = SalvageLedger.Types[definition];
                Vector3 position=default;bool spawnClear=false;
                var size=Site.ScrapSize(definition);
                for(int attempt=0;attempt<anchors.Length;attempt++)
                {
                    var candidate=anchors[(n+attempt)%anchors.Length].position+Vector3.up*.12f;
                    if(Physics.CheckBox(candidate+Vector3.up*(size.y*.5f+.02f),size*.48f,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))continue;
                    bool close=false;foreach(var existing in WorldItems.Values)
                        if(existing!=null&&existing.IsAvailableInWorld&&Vector3.Distance(existing.transform.position,candidate)<Mathf.Max(1.4f,size.magnitude*.6f)){close=true;break;}
                    if(close||!Site.CargoRouteClear(candidate,definition))continue;position=candidate;spawnClear=true;break;
                }
                if(!spawnClear)continue;
                int value = Mathf.RoundToInt(lootRandom.Next(type.MinValue, type.MaxValue + 1) * SalvageLedger.LootScale(Cycle));
                if (!_ledger.TryCreate(definition, value, out int index)) break;
                Publish();SetPosition(index,position);
                int capturedIndex = index, serial = Cargo(index).Serial;
                // Recycle exhausted world actors while keeping every instance serial monotonic.
                if (WorldItems.TryGetValue(index, out var previous) && previous != null) Runner.Despawn(previous.Object);
                Runner.Spawn(Site.ScrapPrefab, position, Quaternion.identity, null, (_, obj) =>
                { var scrap = obj.GetComponent<SalvageScrap>(); scrap.Index = capturedIndex; scrap.Serial = serial; });
                spawnedValue += value;
                if (n >= 11 && spawnedValue >= target) break;
            }
            if(spawnedValue<needed)
            {
                // Never spend a Day on a map whose available cargo cannot cover the remaining objective.
                for(int i=0;i<_ledger.Cargo.Length;i++)if(_ledger.Cargo[i].State==SalvageCargoState.World)
                {var c=_ledger.Cargo[i];c.State=SalvageCargoState.Destroyed;_ledger.Cargo[i]=c;}
                _ledger.Restore(SalvagePhase.Base,Cycle,Day,Funds,Quota,SoldThisCycle,TotalSold,_ledger.NextSerial,CrewAtCycleStart,false,Revision+1,LastDayRecovered,LastDayLost);
                DayTimer=TickTimer.None;MapDay=0;RunSeed=unchecked(RunSeed+104729);
                Publish();RetireExhaustedActors();ClearCorridorDoors();
                RPC_Feedback(Runner.LocalPlayer,NotEnoughCargoFeedback);
                return;
            }
            ClearEnemies(); EnemySpawnTimer=TickTimer.CreateFromSeconds(Runner,35);
            RPC_Cue(6, Site.ReturnPoint.position, 0);
        }
        private void FinishDay()
        {
            if (Phase != SalvagePhase.InDay) return;
            for(int seat=0;seat<4;seat++)ReleaseGrip(seat);
            UpdateDeliveries(true);
            ClearCorridorDoors();
            var survivors = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                var player = Player(i); if (player == null) continue;
                var life = player.GetComponent<NetworkPlayerLifeState>();
                survivors[i] = IsConnected(i) && life != null && life.CanInitiateAction && Site.Contains(player.transform.position);
                if (!survivors[i]) player.GetComponent<LobbyPlayerState>()?.SetGameplayToolId(0);
            }
            _ledger.Return(survivors); DayTimer = TickTimer.None; RefreshCrew(); Publish();
            RetireExhaustedActors();
            ClearEnemies();
            foreach (var pickup in FindObjectsByType<NetworkBehaviour>(FindObjectsInactive.Include))
                if ((pickup is NetworkTeamToolPickup || pickup is EchoProtocol.Tools.Scanner.NetworkToolPickup || pickup is NetworkDoorJammer)
                    && pickup.Runner == Runner && pickup.Object != null && pickup.Object.IsValid) Runner.Despawn(pickup.Object);
            foreach (var beacon in FindObjectsByType<NoiseMakerBeacon>(FindObjectsInactive.Include))
            { var obj = beacon.GetComponent<NetworkObject>(); if (obj != null && obj.IsValid && obj.Runner == Runner) Runner.Despawn(obj); }
            for (int i = 0; i < 4; i++)
            { var player = Player(i); if (player == null) continue;
              player.GetComponent<NetworkPlayerLifeState>()?.ResetForMatchAuthoritative();
              player.GetComponent<NetworkCharacterController>()?.Teleport(Site.CrewPosition(i), Site.CrewSpawn.rotation); }
            RPC_Cue(7, Site.ReturnPoint.position, 0);
        }
        private void ClearCorridorDoors()
        {
            foreach(var door in FindObjectsByType<NetworkSlidingDoor>(FindObjectsInactive.Include))
                if(door.Object!=null&&door.Object.IsValid&&door.Runner==Runner&&door.name.StartsWith("SalvageCorridorDoor"))
                    Runner.Despawn(door.Object);
        }
        private void SpawnCorridorDoors()
        {
            ClearCorridorDoors();
            if(Site.RandomMap==null||Site.CorridorDoorPrefab==null)return;
            // A minority of collection links have the existing interactive door. The base uses its own phase gate.
            for(int i=2;i<MapPlacementCount;i+=3)
            {
                var placement=MapPlacements[i].Decode();var parent=MapPlacements[placement.Parent].Decode();
                var module=placement.Parent==0?Site.RandomMap.FixedBase:Site.RandomMap.Rooms[parent.Module];
                var socket=module.Sockets[placement.ParentSocket];
                var forward=SalvageMapLayout.Direction(SalvageMapLayout.SocketDirection(parent,socket));
                var position=SalvageMapLayout.SocketPoint(parent,socket)+forward*6;
                Runner.Spawn(Site.CorridorDoorPrefab,position,Quaternion.LookRotation(forward));
            }
        }
        public void Noise(PlayerRef actor, RuntimeNoiseType kind, Vector3 position, string occurrence)
        {
            if (!Object.HasStateAuthority || !actor.IsValid) return;
            AlertEnemies(position, kind==RuntimeNoiseType.NOISE_MAKER?25:16);
            HostRuntimeNoiseService.EnsureExists(MatchAuthorityRuntime.Instance).TryAccept(actor, kind,
                RuntimeNoiseSourceOccurrenceKey.ForInteraction("salvage:" + RunId + ":" + occurrence, (uint)Runner.Tick.Raw), position, out _);
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Cue(int kind, Vector3 position, int material) => SalvagePresentation.Cue(kind, position, (SalvageCargoMaterial)material);
        [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
        private void RPC_LocalCue([RpcTarget] PlayerRef actor,int kind,Vector3 position)=>SalvagePresentation.Cue(kind,position,SalvageCargoMaterial.Metal);
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_Feedback([RpcTarget] PlayerRef actor, string message) => LocalFeedback?.Invoke(message);
    }
}

