using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace EchoProtocol.Salvage
{
    public sealed class SalvageRandomMap : MonoBehaviour
    {
        public SalvageMapModule FixedBase;
        public SalvageMapModule[] Rooms;
        public SalvageMapModule Passage;
        public GameObject EndWall;
        public Transform BaseExit;
        [Range(4,16)] public int RoomCount = 8;
        public int TestSeed;
        public int BuiltRevision { get; private set; }
        public string LastError { get; private set; }
        public EchoProtocol.AI.Stalker.Spatial.RegionGraphAsset EnemyGraph {get;private set;}
        public readonly List<Transform> LootSpawns = new();
        public readonly List<Transform> NavigationSamples = new();
        private GameObject _area;
        private NavMeshSurface _surface;
        private NavMeshData _runtimeData;
        private readonly List<(Vector3 point,Vector3 forward)> _endCaps=new();
        private readonly Dictionary<Vector3,(NavMeshData data,NavMeshDataInstance instance)> _cargoNavigation=new();
        private List<NavMeshBuildSource> _cargoSources;
        private Bounds _cargoBounds;
        public bool CargoPath(Vector3 from,Vector3 to,Vector3 size,NavMeshPath path)
        {
            if(_cargoSources==null)return false;
            if(!_cargoNavigation.TryGetValue(size,out var navigation))
            {
                var settings=NavMesh.GetSettingsByIndex(0);
                settings.agentRadius=Mathf.Min(size.x,size.z)*.5f+.12f;settings.agentHeight=size.y+.2f;
                settings.overrideVoxelSize=true;settings.voxelSize=.1f;
                var sources=new List<NavMeshBuildSource>(_cargoSources.Count);
                foreach(var original in _cargoSources){var source=original;if(source.area!=1)source.area=30;sources.Add(source);}
                var data=NavMeshBuilder.BuildNavMeshData(settings,sources,_cargoBounds,Vector3.zero,Quaternion.identity);
                if(data==null)return false;
                navigation=(data,default);_cargoNavigation.Add(size,navigation);
            }
            var filter=new NavMeshQueryFilter{agentTypeID=NavMesh.GetSettingsByIndex(0).agentTypeID,areaMask=1<<30};
            var instance=NavMesh.AddNavMeshData(navigation.data);
            try {return NavMesh.SamplePosition(from,out var start,.4f,filter)&&NavMesh.SamplePosition(to,out var end,4,filter)
                &&NavMesh.CalculatePath(start.position,end.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete;}
            finally {instance.Remove();}

        }
        public SalvageMapPlacement BasePlacement => new SalvageMapPlacement {
            Module=-1, Parent=-1, Position=FixedBase.transform.position,
            QuarterTurns=Mathf.RoundToInt(FixedBase.transform.eulerAngles.y/90) % 4 };

        public List<SalvageMapPlacement> Plan(int seed)
        {
            Exception failure=null;
            for(int retry=0;retry<24;retry++)
                try {
                    var plan=SalvageMapLayout.Generate(unchecked(seed+retry*7919),Rooms,BasePlacement,FixedBase,RoomCount);
                    if(SalvageMapLayout.PassagesClear(plan,Rooms,FixedBase))return plan;
                }
                catch(InvalidOperationException e) { failure=e; }
            throw new InvalidOperationException("Cannot construct a valid map from the authored modules.",failure);
        }
        public bool Build(IReadOnlyList<SalvageMapPlacement> plan, int revision)
        {
            if (BuiltRevision==revision) return true;
            LastError=null;
            try
            {
                if(plan.Count<2 || plan.Count>SalvageMapLayout.Capacity) throw new InvalidOperationException("Invalid placement manifest.");
                Clear();
                _area=new GameObject("Salvage Collection Area");_area.transform.SetParent(transform,false);
                var used=new HashSet<(int,int)>();
                for(int i=1;i<plan.Count;i++)
                {
                    var p=plan[i];
                    if(p.Module<0||p.Module>=Rooms.Length||p.Parent<0||p.Parent>=i)throw new InvalidOperationException("Invalid room/parent.");
                    var room=Instantiate(Rooms[p.Module],p.Position,Quaternion.Euler(0,p.QuarterTurns*90,0),_area.transform);
                    room.name=$"Room_{i:00}_{Rooms[p.Module].name}";
                    foreach(var point in room.LootPoints) LootSpawns.Add(Marker(room.transform,"Loot",room.transform.TransformPoint(point)));
                    NavigationSamples.Add(Marker(room.transform,"RoomAccess",room.transform.position+Vector3.up*.2f));
                    var parent=plan[p.Parent];var parentModule=p.Parent==0?FixedBase:Rooms[parent.Module];
                    var a=SalvageMapLayout.SocketPoint(parent,parentModule.Sockets[p.ParentSocket]);
                    var b=SalvageMapLayout.SocketPoint(p,Rooms[p.Module].Sockets[p.EntrySocket]);
                    int direction=SalvageMapLayout.SocketDirection(parent,parentModule.Sockets[p.ParentSocket]);
                    if(Vector3.Distance(a+SalvageMapLayout.Direction(direction)*Vector3.Distance(a,b),b)>.05f
                        || (direction+2)%4!=SalvageMapLayout.SocketDirection(p,Rooms[p.Module].Sockets[p.EntrySocket]))
                        throw new InvalidOperationException("Doorways are not aligned.");
                    Connect(a,b,direction,false);
                    used.Add((p.Parent,p.ParentSocket));used.Add((i,p.EntrySocket));
                }
                // Every unused exit leads to a finished corridor alcove, never an opening into the void.
                for(int i=0;i<plan.Count;i++)
                {
                    var p=plan[i];var module=i==0?FixedBase:Rooms[p.Module];
                    for(int s=0;s<module.Sockets.Length;s++)if(!used.Contains((i,s)))
                    {
                        var a=SalvageMapLayout.SocketPoint(p,module.Sockets[s]);int d=SalvageMapLayout.SocketDirection(p,module.Sockets[s]);
                        Connect(a,a+SalvageMapLayout.Direction(d)*6,d,true);
                    }
                }
                var hidingPositions=new List<Vector3>();
                foreach(var spot in _area.GetComponentsInChildren<HidingSpot>(true))
                {
                    bool near=false;foreach(var position in hidingPositions)if(Vector3.Distance(position,spot.transform.position)<16){near=true;break;}
                    if(near)spot.gameObject.SetActive(false);else hidingPositions.Add(spot.transform.position);
                }
                Physics.SyncTransforms();
                foreach(var cap in _endCaps)
                    if(!Physics.Raycast(cap.point-cap.forward*.8f+Vector3.up,cap.forward,1.6f,~0,QueryTriggerInteraction.Ignore))
                        throw new InvalidOperationException("Unsealed corridor end at "+cap.point);
                _surface=gameObject.GetComponent<NavMeshSurface>()??gameObject.AddComponent<NavMeshSurface>();
                _surface.collectObjects=CollectObjects.Children;_surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
                _surface.overrideVoxelSize=true;_surface.voxelSize=.12f;
                // Validate human-sized passage clearance, without changing global AI agent settings.
                // The existing Stalker uses a wider agent and is deliberately not spawned in this prototype.
                _surface.RemoveData();
                var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.4f;settings.agentHeight=1.8f;
                settings.overrideVoxelSize=true;settings.voxelSize=.08f;
                var sources=new List<NavMeshBuildSource>();
                var markups=new List<NavMeshBuildMarkup>();
                foreach(var door in GetComponentsInChildren<SalvageBaseDoor>(true))
                    markups.Add(new NavMeshBuildMarkup {root=door.transform,ignoreFromBuild=true});
                NavMeshBuilder.CollectSources(transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,markups,sources);
                foreach(var source in sources)
                    if(source.shape==NavMeshBuildSourceShape.Mesh && source.sourceObject is Mesh mesh && !mesh.isReadable)
                        throw new InvalidOperationException("Runtime navigation requires a readable Salvage mesh copy: "+mesh.name);
                var geometryBounds=new Bounds(FixedBase.transform.position,Vector3.one*40);
                for(int i=1;i<plan.Count;i++)geometryBounds.Encapsulate(SalvageMapLayout.WorldBounds(plan[i],Rooms[plan[i].Module].Footprint));
                geometryBounds.Expand(new Vector3(60,20,60));
                _cargoSources=sources;_cargoBounds=geometryBounds;
                _runtimeData=NavMeshBuilder.BuildNavMeshData(settings,sources,geometryBounds,Vector3.zero,Quaternion.identity);
                _surface.navMeshData=_runtimeData;
                _surface.AddData();
                var path=new NavMeshPath();var basePoint=GetComponent<SalvageSite>().CrewPosition(0);
                if(!NavMesh.SamplePosition(basePoint,out var start,3,NavMesh.AllAreas))throw new InvalidOperationException("Fixed base is not walkable.");
                var rejected=new List<Transform>();
                var roomReachable=new Dictionary<Transform,int>();
                foreach(var point in LootSpawns)
                {
                    if(!NavMesh.SamplePosition(point.position,out var hit,1.6f,NavMesh.AllAreas)
                        || !NavMesh.CalculatePath(start.position,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                    {rejected.Add(point);continue;}
                    point.position=hit.position+Vector3.up*.12f;
                    roomReachable.TryGetValue(point.parent,out int count);roomReachable[point.parent]=count+1;
                }
                foreach(var point in rejected)LootSpawns.Remove(point);
                if(roomReachable.Count!=plan.Count-1||LootSpawns.Count<12)
                    throw new InvalidOperationException("Disconnected rooms: reachable "+roomReachable.Count+"/"+(plan.Count-1));
                foreach(var locker in _area.GetComponentsInChildren<HidingSpot>(true))
                {
                    if(locker.ExitPoint==null||!NavMesh.SamplePosition(locker.ExitPoint.position,out var exit,1.2f,NavMesh.AllAreas)
                        ||Vector2.Distance(new Vector2(exit.position.x,exit.position.z),new Vector2(locker.ExitPoint.position.x,locker.ExitPoint.position.z))>.5f
                        ||!NavMesh.CalculatePath(start.position,exit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                        throw new InvalidOperationException("Hiding locker exit is blocked: "+locker.name);
                }
                for(int i=0;i<plan.Count;i++)
                {
                    var p=plan[i];var module=i==0?FixedBase:Rooms[p.Module];
                    foreach(var socket in module.Sockets)
                    {
                        var throat=SalvageMapLayout.SocketPoint(p,socket)+Vector3.up*.1f;
                        var direction=SalvageMapLayout.Direction(SalvageMapLayout.SocketDirection(p,socket));
                        foreach(int side in new[]{-1,1})
                        {
                            if(!NavMesh.SamplePosition(throat+direction*side,out var hit,.8f,NavMesh.AllAreas)
                                ||!NavMesh.CalculatePath(start.position,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                                throw new InvalidOperationException("Doorway blocked in "+module.name+" at "+throat);
                        }
                    }
                }
                var site=GetComponent<SalvageSite>();site.ScrapSpawns=LootSpawns.ToArray();
                // The enemy director uses validated navigation samples instead of legacy fixed spawns.
                site.MonsterSpawns=Array.Empty<Transform>();
                EnemyGraph=SalvageEnemyGraph.Build(this);
                BuiltRevision=revision;return true;
            }
            catch(Exception e){LastError=e.Message;return false;}
        }
        private void Connect(Vector3 a,Vector3 b,int direction,bool cap)
        {
            float distance=Vector3.Distance(a,b);int count=Mathf.RoundToInt(distance/6);
            if(count<1||Mathf.Abs(count*6-distance)>.05f)throw new InvalidOperationException("Passage length must match six-metre kit grid.");
            var forward=SalvageMapLayout.Direction(direction);var rotation=Quaternion.Euler(0,direction*90,0);
            for(int i=0;i<count;i++)Instantiate(Passage,a+forward*(i*6+3),rotation,_area.transform);
            if(cap){Instantiate(EndWall,b,rotation,_area.transform);_endCaps.Add((b,forward));}
            NavigationSamples.Add(Marker(_area.transform,"Connection",(a+b)*.5f+Vector3.up*.2f));
        }
        private static Transform Marker(Transform parent,string name,Vector3 position)
        {var go=new GameObject(name);go.transform.SetParent(parent);go.transform.position=position;return go.transform;}
        private void Clear()
        {
            foreach(var navigation in _cargoNavigation.Values)
            {navigation.instance.Remove();if(Application.isPlaying)Destroy(navigation.data);else DestroyImmediate(navigation.data);}
            _cargoNavigation.Clear();_cargoSources=null;
            if(EnemyGraph!=null){if(Application.isPlaying)Destroy(EnemyGraph);else DestroyImmediate(EnemyGraph);EnemyGraph=null;}
            if(_surface!=null)_surface.RemoveData();
            if(_runtimeData!=null){if(Application.isPlaying)Destroy(_runtimeData);else DestroyImmediate(_runtimeData);_runtimeData=null;}
            for(int i=transform.childCount-1;i>=0;i--)
            {
                var child=transform.GetChild(i).gameObject;
                if(child.name!="Salvage Collection Area")continue;
                child.SetActive(false);if(Application.isPlaying)Destroy(child);else DestroyImmediate(child);
            }
            LootSpawns.Clear();NavigationSamples.Clear();_endCaps.Clear();BuiltRevision=0;
        }
        private void OnDestroy()
        {
            Clear();
            if(EnemyGraph!=null){if(Application.isPlaying)Destroy(EnemyGraph);else DestroyImmediate(EnemyGraph);}
            if(_surface!=null)_surface.RemoveData();
            if(_runtimeData!=null){if(Application.isPlaying)Destroy(_runtimeData);else DestroyImmediate(_runtimeData);}
        }
    }
}
