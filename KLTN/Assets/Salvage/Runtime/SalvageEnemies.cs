using System;
using System.Linq;
using EchoProtocol.Networking;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.AI.Minions;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.Salvage
{
    public sealed class SalvageEnemy : MonoBehaviour
    {
        public bool Main;
        private Vector3 _lastOutside;
        private float _patrolAt;
        private NavMeshAgent _agent;
        private StalkerController _controller;
        private NetworkObject _object;
        private EchoProtocol.AI.Stalker.Spatial.RegionGraphAsset _graph;
        private void Awake()
        {
            var run=SalvageRunState.Instance;
            if(Main&&run!=null)
            {
                _graph=run.Site.RandomMap.EnemyGraph;
                GetComponent<StalkerController>()?.ConfigureSalvageGraph(_graph);
            }
        }
        private void Start()
        {
            _agent=GetComponent<NavMeshAgent>();_object=GetComponent<NetworkObject>();
            _controller=GetComponent<StalkerController>();_lastOutside=transform.position;
            if(_agent!=null)_agent.radius=.4f;
            if (_controller!=null) _controller.SetNavigationPathSafetyValidator(corners=>corners.All(p=>!SalvageRunState.EnemySafe(p)));
        }
        private void Update()
        {
            var run=SalvageRunState.Instance;
            if(run==null||_object==null||!_object.IsValid||!_object.HasStateAuthority||_agent==null||!_agent.enabled)return;
            if(_controller!=null&&_graph==null&&run.Site.RandomMap.EnemyGraph!=null)
            { _graph=run.Site.RandomMap.EnemyGraph;_controller.ConfigureSalvageGraph(_graph); }
            if(SalvageRunState.EnemySafe(transform.position))
            {
                // Stalker authoritative locomotion disables agent.updatePosition.
                if(_agent.Warp(_lastOutside))
                {
                    transform.position=_agent.nextPosition;
                    GetComponent<NetworkTransform>()?.Teleport(transform.position);
                }
                _agent.ResetPath();
            }
            else _lastOutside=transform.position;
            if(_controller!=null && Time.time>=_patrolAt && (_controller.CurrentState==StalkerState.PATROL||_controller.CurrentState==StalkerState.SEARCH))
            {
                _patrolAt=Time.time+12;
                var points=run.Site.RandomMap.NavigationSamples.Where(t=>!SalvageRunState.EnemySafe(t.position)).ToArray();
                if(points.Length>0)_controller.InvestigateSalvageNoise(points[UnityEngine.Random.Range(0,points.Length)].position);
            }
        }
    }

    public sealed partial class SalvageRunState
    {
        [Networked,Capacity(9)] public NetworkArray<NetworkId> EnemyIds=>default;
        [Networked] public TickTimer EnemySpawnTimer {get;private set;}
        [Networked,Capacity(4)] public NetworkArray<TickTimer> EnemyHitTimers=>default;
        [Networked,Capacity(4)] public NetworkArray<TickTimer> MinionHitTimers=>default;
        private readonly Vector3[] _enemyLastCrewPositions=new Vector3[4];
        private float _enemyNoiseAt;
        public static bool EnemySafe(Vector3 point)
        { var r=Instance;return SalvageSite.IsActive&&r!=null&&r.Site!=null&&r.Site.Contains(point); }
        public static bool ProtectedFromEnemies(GameObject player)=>SalvageSite.IsActive&&Instance!=null
            && (Instance.Phase!=SalvagePhase.InDay||EnemySafe(player.transform.position));
        public static int MainEnemyLimit(int level,int crew,int rooms)=>Math.Min(rooms>=8?3:2,
            Math.Min(crew<=2?2:3,level<5?1:level<8?2:3));
        public static int MinionLimit(int level,int crew)=>Math.Min(crew<=2?4:6,level<3?2:level<5?3:level<8?4:6);
        public bool TryEnemyHit(NetworkPlayerLifeState victim,Vector3 origin)
        {
            if(!Object.HasStateAuthority||Phase!=SalvagePhase.InDay||victim==null||victim.Runner!=Runner
                ||ProtectedFromEnemies(victim.gameObject))return false;
            int seat=Seat(victim.Object.InputAuthority);
            if(seat<0||!EnemyHitTimers[seat].ExpiredOrNotRunning(Runner)||Vector3.Distance(origin,victim.transform.position)>3.2f)return false;
            var eye=origin+Vector3.up;var end=victim.transform.position+Vector3.up;
            if(Physics.Linecast(eye,end,out var hit,~0,QueryTriggerInteraction.Ignore)
                &&hit.collider.GetComponentInParent<NetworkPlayerLifeState>()!=victim&&hit.distance>.25f)return false;
            if(!victim.TryApplyAuthoritativeDamage(Cycle<5?25:Cycle<8?30:35,"SALVAGE_MONSTER",origin))return false;
            EnemyHitTimers.Set(seat,TickTimer.CreateFromSeconds(Runner,3));return true;
        }
        public bool ReserveMinionHit(NetworkPlayerLifeState victim)
        {
            if(!Object.HasStateAuthority||victim.Runner!=Runner||ProtectedFromEnemies(victim.gameObject))return false;
            int seat=Seat(victim.Object.InputAuthority);
            if(seat<0||!MinionHitTimers[seat].ExpiredOrNotRunning(Runner))return false;
            MinionHitTimers.Set(seat,TickTimer.CreateFromSeconds(Runner,1.5f));return true;
        }
        private void ClearEnemies()
        {
            foreach(var enemy in FindObjectsByType<SalvageEnemy>())
            {var obj=enemy.GetComponent<NetworkObject>();if(obj!=null&&obj.IsValid&&obj.Runner==Runner)Runner.Despawn(obj);}
            for(int i=0;i<EnemyIds.Length;i++)EnemyIds.Set(i,default);
            EnemySpawnTimer=TickTimer.None;StalkerId=default;
        }
        private void TickEnemies()
        {
            if(Phase==SalvagePhase.InDay&&Time.time>=_enemyNoiseAt)
            {
                _enemyNoiseAt=Time.time+.5f;
                for(int seat=0;seat<4;seat++)
                {
                    var player=Player(seat);if(player==null)continue;
                    var point=player.transform.position;float distance=Vector3.Distance(point,_enemyLastCrewPositions[seat]);
                    _enemyLastCrewPositions[seat]=point;
                    if(distance>.1f&&distance<5&&!ProtectedFromEnemies(player.gameObject))
                    {var movement=player.GetComponent<NetworkPlayerMovement>();AlertEnemies(point,movement!=null&&movement.IsCrouching?3:distance>2.2f?14:7);}
                }
            }
            if(Phase!=SalvagePhase.InDay||!LocalMapReady||!EnemySpawnTimer.Expired(Runner))return;
            EnemySpawnTimer=TickTimer.CreateFromSeconds(Runner,20);
            int mains=MainEnemyLimit(Cycle,CrewAtCycleStart,MapPlacementCount-1),minions=MinionLimit(Cycle,CrewAtCycleStart);
            for(int slot=0;slot<mains+minions;slot++)
            {
                if(EnemyIds[slot].IsValid)continue;
                bool main=slot<mains;var prefabs=main?Site.MainEnemyPrefabs:Site.MinionPrefabs;
                if(prefabs==null||prefabs.Length==0)return;
                var random=new System.Random(unchecked(MapSeed+slot*7919));
                var anchors=Site.RandomMap.NavigationSamples.Where(t=>!EnemySafe(t.position)).OrderBy(_=>random.Next()).ToArray();
                foreach(var anchor in anchors)
                {
                    if(!NavMesh.SamplePosition(anchor.position,out var nav,2,NavMesh.AllAreas)||EnemySafe(nav.position))continue;
                    bool safe=true;
                    for(int s=0;s<4;s++) {var p=Player(s);if(p==null)continue;var path=new NavMeshPath();
                        if(!NavMesh.CalculatePath(p.transform.position,nav.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete){safe=false;break;}
                        float distance=0;for(int k=1;k<path.corners.Length;k++)distance+=Vector3.Distance(path.corners[k-1],path.corners[k]);
                        if(distance<18||Vector3.Distance(p.transform.position,nav.position)<12
                            ||!Physics.Linecast(p.transform.position+Vector3.up*1.5f,nav.position+Vector3.up,~0,QueryTriggerInteraction.Ignore)){safe=false;break;}}
                    if(!safe)continue;
                    int variant=main?Math.Abs((MapSeed%3+slot)%3):slot-mains;
                    var obj=Runner.Spawn(prefabs[variant%prefabs.Length],nav.position,Quaternion.identity,null);
                    EnemyIds.Set(slot,obj.Id);if(main&&!StalkerId.IsValid)StalkerId=obj.Id;return;
                }
                return;
            }
        }
        public void AlertEnemies(Vector3 position,float radius)
        {
            if(!Object.HasStateAuthority||Phase!=SalvagePhase.InDay||EnemySafe(position))return;
            foreach(var enemy in FindObjectsByType<SalvageEnemy>())
                if(enemy.Main&&Vector3.Distance(enemy.transform.position,position)<=radius)
                {var control=enemy.GetComponent<StalkerController>();control?.InvestigateSalvageNoise(position);}
        }
    }
}
