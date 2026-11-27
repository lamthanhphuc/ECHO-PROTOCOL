using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Fusion;
using EchoProtocol.Auth;
using EchoProtocol.Networking;

namespace EchoProtocol.Salvage
{
    public sealed partial class SalvageRunState
    {
        [Serializable] private sealed class SavedCrew
        {
            public string user;
            public int tool,uses,speed,health,stamina,recovery,strength,powerDay,purchases;
            public static SavedCrew From(SalvageCrew c)=>new SavedCrew {user=c.User.ToString(),tool=c.Tool,uses=c.Uses,speed=c.SpeedRank,
                health=c.HealthRank,stamina=c.StaminaRank,recovery=c.RecoveryRank,strength=c.StrengthRank,powerDay=c.PowerDay,purchases=c.PowerPurchases};
            public SalvageCrew Restore()=>new SalvageCrew {User=user??"",Tool=tool,Uses=uses,SpeedRank=speed,HealthRank=health,
                StaminaRank=stamina,RecoveryRank=recovery,StrengthRank=strength,PowerDay=powerDay,PowerPurchases=purchases};
        }
        [Serializable] private sealed class Checkpoint
        {
            public int version=1,cycle,day,funds,quota,sold,total,next,crew,recovered,lost,seed,shopDay,cartLevel;
            public bool completed;
            public string owner;
            public SalvageCargo[] cargo;
            public SavedCrew[] players;
            public Vector3[] positions;
            public Quaternion[] rotations;
            public int[] shop;
        }
        // Keep upgrade ownership by account even when the next session has a different crew.
        private readonly System.Collections.Generic.Dictionary<string,SavedCrew> _checkpointCrew=new();
        private SalvageCrew ResumeCrew(string user)
            =>_checkpointCrew.TryGetValue(user,out var saved)?saved.Restore():new SalvageCrew {User=user};
        public string CheckpointStatus {get;private set;}="";
        public void PrepareSessionExit()
        {
            if(Object==null||!Object.IsValid||!Object.HasStateAuthority)return;
            SaveCheckpoint(true);
            var authority=EchoProtocol.Networking.Authority.MatchAuthorityRuntime.Instance;
            if(!_endSubmitted&&authority!=null&&authority.IsHostBinding)
            {authority.QueueMatchResult(CompletedRewardLevels>0?"WIN":"LOSE",CompletedRewardLevels>0?1:0);_endSubmitted=true;}
        }
        private float _checkpointRetryAt;
        public bool CanStartFreshCloudRun {get;private set;}
        private bool _allowFreshCloudRun;
        public void StartFreshCloudRunConfirmed(){_allowFreshCloudRun=true;InitializeSavedRun();}
        public bool CheckpointSynced=>Debug.isDebugBuild||_cloudSynced&&_savedRevision==Revision;
        public bool CloudLoading {get;private set;}
        public bool CloudRecoveryRequired {get;private set;}
        private string _approvedCloudState;
        private bool _cloudSaveBusy,_cloudSynced;
        [Networked] private long CloudSequence {get;set;}
        public async void InitializeSavedRun()
        {
            if(CloudLoading)return;CloudLoading=true;CloudRecoveryRequired=false;
            try
            {
                var authority=EchoProtocol.Networking.Authority.MatchAuthorityRuntime.Instance;
                _approvedCloudState=Debug.isDebugBuild?null:await authority.LoadSalvageCloudAsync();
                if(Object==null||!Object.IsValid)return;
                if(!TryRestoreCheckpoint())StartFreshRun();
            }
            catch(Exception error){CheckpointStatus=error.Message;CloudRecoveryRequired=true;}
            finally{CloudLoading=false;}
        }
        private async void UploadCheckpoint(string json,bool failed)
        {
            if(Debug.isDebugBuild||_cloudSaveBusy)return;_cloudSaveBusy=true;
            int savedRevision=Revision;
            try
            {
                var authority=EchoProtocol.Networking.Authority.MatchAuthorityRuntime.Instance;
                if(authority==null||!await authority.SaveSalvageCloudAsync(json,++CloudSequence,failed))
                    CheckpointStatus="Đã lưu trên máy; chưa đồng bộ. Giữ game mở và kiểm tra kết nối.";
                else {_cloudSynced=Revision==savedRevision&&failed==(Phase==SalvagePhase.Failed);CheckpointStatus="Đã đồng bộ tiến độ";}
            }
            catch(Exception){CheckpointStatus="Chưa đồng bộ tiến độ. Kiểm tra kết nối.";}
            finally{_cloudSaveBusy=false;}
        }
        private int _savedRevision=-1;
        private float _nextSave;
        [Networked] private int RewardStartDays {get;set;}
        [Networked] private int RewardStartLevels {get;set;}
        private string CheckpointPath
        {
            get
            {
                string user=AuthSession.CurrentUserId;
                if(string.IsNullOrEmpty(user))return null;
                using var hash=SHA256.Create();
                string key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(user))).Replace("-","");
                return Path.Combine(Application.persistentDataPath,"Salvage",key+".json");
            }
        }
        private static SalvageCargo WarehouseSnapshot(SalvageCargo cargo)
        {
            // Called only outside a Day: handling warehouse stock must not erase it on resume.
            if(cargo.State==SalvageCargoState.World||cargo.State==SalvageCargoState.PhysicsHeld||cargo.State==SalvageCargoState.Held)
            {cargo.State=SalvageCargoState.Stored;cargo.Owner=cargo.Slot=-1;}
            return cargo;
        }
        private void SaveCheckpoint(bool force=false)
        {
            if(!Runner.IsServer||NetworkBootstrap.IsHostMigrationReconciliationPending||Time.unscaledTime<_checkpointRetryAt)return;
            if(_ledger==null||Phase==SalvagePhase.InDay||(!force&&((_savedRevision==Revision&&(Debug.isDebugBuild||_cloudSynced))||Time.unscaledTime<_nextSave)))return;
            string path=CheckpointPath;if(path==null)return;
            try
            {
                if(Phase==SalvagePhase.Failed) {_nextSave=Time.unscaledTime+5;UploadCheckpoint(JsonUtility.ToJson(new Checkpoint {owner=AuthSession.CurrentUserId,cycle=Cycle,day=Day,cargo=new SalvageCargo[128]}),true);if(File.Exists(path))File.Delete(path);if(File.Exists(path+".bak"))File.Delete(path+".bak");_savedRevision=Revision;return;}
                for(int i=0;i<4;i++)if(Crew[i].User.Length>0)_checkpointCrew[Crew[i].User.ToString()]=SavedCrew.From(Crew[i]);
                var savedPlayers=new SavedCrew[_checkpointCrew.Count];_checkpointCrew.Values.CopyTo(savedPlayers,0);
                var data=new Checkpoint {owner=AuthSession.CurrentUserId,cycle=Cycle,day=Day,funds=Funds,quota=Quota,sold=SoldThisCycle,total=TotalSold,
                    next=NextSerial,crew=CrewAtCycleStart,recovered=LastDayRecovered,lost=LastDayLost,seed=RunSeed,completed=DayCompleted,
                    shopDay=ShopDay,cartLevel=CartPurchaseLevel,cargo=new SalvageCargo[Items.Length],players=savedPlayers,
                    positions=new Vector3[Items.Length],rotations=new Quaternion[Items.Length],shop=new int[6]};
                for(int i=0;i<Items.Length;i++){data.cargo[i]=WarehouseSnapshot(Cargo(i));data.positions[i]=Items[i].Position;data.rotations[i]=Items[i].Rotation;}
                for(int i=0;i<6;i++)data.shop[i]=ShopBought[i];
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if(_savedRevision!=Revision)_cloudSynced=false;
                string json=JsonUtility.ToJson(data);File.WriteAllText(path+".tmp",json);
                UploadCheckpoint(json,false);
                if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);
                _savedRevision=Revision;_nextSave=Time.unscaledTime+5;CheckpointStatus="Đã lưu tiến độ";
            }
            catch(Exception e){CheckpointStatus="Không lưu được tiến độ. Kiểm tra dung lượng và quyền ghi thư mục game.";Debug.LogWarning("[Salvage] Checkpoint write failed: "+e.Message);_checkpointRetryAt=Time.unscaledTime+5;}
        }
        private bool TryRestoreCheckpoint()
        {
            string path=CheckpointPath;if(path==null)return false;
            if(_approvedCloudState!=null)
            {
                // The server response replaces local input, including edited or corrupt files.
                if(string.IsNullOrEmpty(_approvedCloudState))
                {
                    if(File.Exists(path)&&!_allowFreshCloudRun){CanStartFreshCloudRun=true;throw new InvalidDataException("Có bản lưu cũ chưa được server xác nhận. Bạn có thể giữ file để đối soát hoặc xác nhận tạo chuyến mới.");}
                    // Retain unverified legacy data for manual recovery, never upload it as trusted progress.
                    if(File.Exists(path))File.Copy(path,path+".unverified-local",true);
                    return false;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if(File.Exists(path))File.Copy(path,path+".local-recovery",true);
                File.WriteAllText(path,_approvedCloudState);
            }
            bool found=false;
            foreach(string file in _approvedCloudState!=null?new[]{path}:new[]{path,path+".bak"})
            {
                if(!File.Exists(file))continue;
                found=true;
                try
                {
                    var d=JsonUtility.FromJson<Checkpoint>(_approvedCloudState??File.ReadAllText(file));
                    if(d==null||d.version!=1||d.owner!=AuthSession.CurrentUserId||d.cargo?.Length!=SalvageLedger.Capacity
                        ||d.players==null||d.positions?.Length!=SalvageLedger.Capacity||d.rotations?.Length!=SalvageLedger.Capacity||d.shop?.Length!=6)
                        throw new InvalidDataException("Invalid checkpoint format");
                    var ledger=new SalvageLedger(d.crew);
                    ledger.Restore(SalvagePhase.Base,d.cycle,d.day,d.funds,d.quota,d.sold,d.total,d.next,d.crew,d.completed,1,d.recovered,d.lost);
                    foreach(var c in d.cargo)if(c.Definition<0||c.Definition>=SalvageLedger.Types.Length||c.Integrity<0||c.Integrity>100)
                        throw new InvalidDataException("Invalid cargo");
                    var ids=new System.Collections.Generic.HashSet<int>();
                    for(int i=0;i<d.cargo.Length;i++)
                    {
                        var c=d.cargo[i];var point=d.positions[i];var q=d.rotations[i];
                        if(!Enum.IsDefined(typeof(SalvageCargoState),c.State)||c.BaseValue<0||c.BaseValue>10000000
                            ||!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(point.z)
                            ||!float.IsFinite(q.x)||!float.IsFinite(q.y)||!float.IsFinite(q.z)||!float.IsFinite(q.w)
                            ||c.State==SalvageCargoState.Stored&&(c.Serial<=0||c.Serial>=d.next||!ids.Add(c.Serial)))throw new InvalidDataException("Invalid cargo snapshot");
                    }
                    var users=new System.Collections.Generic.HashSet<string>();
                    foreach(var player in d.players)if(player==null||string.IsNullOrEmpty(player.user)||!users.Add(player.user)
                        ||player.speed<0||player.speed>3||player.recovery<0||player.recovery>3||player.strength<0||player.strength>3
                        ||player.health<0||player.stamina<0||player.tool<0||player.tool>4||player.uses<0||player.uses>3
                        ||player.purchases<0||player.purchases>2)throw new InvalidDataException("Invalid crew snapshot");
                    foreach(int stock in d.shop)if(stock<0||stock>4)throw new InvalidDataException("Invalid stock snapshot");
                    _ledger=ledger;RunSeed=d.seed;RunId=Guid.NewGuid().ToString("N");
                    for(int i=0;i<d.cargo.Length;i++)
                    {
                        var c=d.cargo[i];
                        // Only warehouse stock survives a session boundary. Day loot is replayed from its seed.
                        if(c.State!=SalvageCargoState.Stored)c.State=SalvageCargoState.Empty;
                        _ledger.Cargo[i]=c;
                    }
                    Publish();
                    _checkpointCrew.Clear();
                    foreach(var saved in d.players)if(saved!=null&&!string.IsNullOrEmpty(saved.user))_checkpointCrew[saved.user]=saved;
                    for(int i=0;i<4;i++)Crew.Set(i,default);
                    ShopDay=d.shopDay;CartPurchaseLevel=d.cartLevel;for(int i=0;i<6;i++)ShopBought.Set(i,d.shop[i]);
                    for(int i=0;i<d.cargo.Length;i++)if(Cargo(i).State==SalvageCargoState.Stored)
                    {
                        int index=i,serial=Cargo(i).Serial;SetCargoPose(i,d.positions[i],d.rotations[i],Vector3.zero);
                        Runner.Spawn(Site.ScrapPrefab,d.positions[i],d.rotations[i],null,(_,obj)=>
                        {var scrap=obj.GetComponent<SalvageScrap>();scrap.Index=index;scrap.Serial=serial;});
                    }
                    RewardStartDays=SalvageLedger.CompletedDaysFor(Cycle,Day,DayCompleted);
                    RewardStartLevels=SalvageLedger.CompletedLevelsFor(Cycle,Day,DayCompleted,SoldThisCycle,Quota);
                    RefreshCrew();SpawnStations();if(Site.RandomMap!=null)PrepareMap(Cycle,Day);
                    return true;
                }
                catch(Exception e){if(_ledger!=null)throw new InvalidOperationException("Không thể khôi phục chuyến đã lưu. Dữ liệu vẫn được giữ nguyên.",e);Debug.LogWarning("[Salvage] Checkpoint read failed: "+e.Message);}
            }
            if(found)throw new InvalidDataException("Không đọc được chuyến đã lưu. Dữ liệu gốc vẫn được giữ để khôi phục.");
            return false;
        }
        private void OnApplicationQuit()=>PrepareSessionExit();
    }
}
