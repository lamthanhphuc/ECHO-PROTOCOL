using EchoProtocol.Networking;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Salvage
{
    [DisallowMultipleComponent]
    public sealed class SalvageSite : MonoBehaviour
    {
        public Transform CrewSpawn, ReturnPoint, TerminalPoint;
        public Transform[] ScrapSpawns, MonsterSpawns;
        public Transform[] CrewSpawns;
        public NetworkObject RunPrefab, ScrapPrefab, StationPrefab, StalkerPrefab;
        public NetworkObject CorridorDoorPrefab;
        public NetworkObject[] MainEnemyPrefabs, MinionPrefabs;
        public GameObject[] ScrapVisuals;
        public Vector3[] ScrapSizes;
        public Vector3 ScrapSize(int definition)=>ScrapSizes!=null&&definition<ScrapSizes.Length?ScrapSizes[definition]:Vector3.one*.5f;
        private Vector3 TransportSize(int definition)
        {
            var size=ScrapSize(definition);float[] axes={size.x,size.y,size.z};System.Array.Sort(axes);
            // Physics cargo can be rotated: lay tall furniture down instead of scaling it smaller.
            return new Vector3(axes[1],axes[0],axes[2]);
        }
        public bool CanTransport(int definition)
        {
            var size=TransportSize(definition);
            if(SalvageLedger.Types[definition].Pocketable)return true;
            if(RandomMap==null)return true;
            float width=float.MaxValue;
            foreach(var socket in RandomMap.FixedBase.Sockets)width=Mathf.Min(width,socket.Width);
            foreach(var socket in RandomMap.Passage.Sockets)width=Mathf.Min(width,socket.Width);
            // Move lengthwise through doors; turning clearance is checked against the actual route.
            return Mathf.Min(size.x,size.z)+.4f<=width && size.y+.3f<=3.4f;
        }
        public bool CargoRouteClear(Vector3 from,int definition)
        {
            if(SalvageLedger.Types[definition].Pocketable||RandomMap==null)return true;
            var path=new UnityEngine.AI.NavMeshPath();
            var socket=RandomMap.FixedBase.Sockets[0];
            var destination=RandomMap.FixedBase.transform.TransformPoint(socket.Position-SalvageMapLayout.Direction(socket.Direction)*5);
            var size=TransportSize(definition);
            if(!RandomMap.CargoPath(from,destination,size,path))return false;
            var extents=new Vector3(Mathf.Min(size.x,size.z)*.5f+.1f,size.y*.5f,Mathf.Max(size.x,size.z)*.5f+.1f);
            Quaternion previous=Quaternion.Euler(0,size.x>size.z?90:0,0);
            for(int i=1;i<path.corners.Length;i++)
            {
                var direction=path.corners[i]-path.corners[i-1];direction.y=0;if(direction.sqrMagnitude<.001f)continue;
                var rotation=Quaternion.LookRotation(direction);
                float distance=Vector3.Distance(path.corners[i-1],path.corners[i]);int steps=Mathf.CeilToInt(distance/.65f);
                for(int step=0;step<=steps;step++)
                {
                    var point=Vector3.Lerp(path.corners[i-1],path.corners[i],steps==0?0:(float)step/steps)+Vector3.up*(size.y*.5f+.12f);
                    for(int turn=0;turn<(step==0?7:1);turn++)
                    foreach(var hit in Physics.OverlapBox(point,extents,step==0?Quaternion.Slerp(previous,rotation,turn/6f):rotation,~0,QueryTriggerInteraction.Ignore))
                    {
                        if(hit.GetComponentInParent<SalvageBaseDoor>()!=null||hit.GetComponentInParent<NetworkSlidingDoor>()!=null
                            ||hit.GetComponentInParent<SalvageScrap>()!=null||hit.GetComponentInParent<LobbyPlayerState>()!=null)continue;
                        return false;
                    }
                }
                previous=rotation;
            }
            return true;
        }
        public GameObject PreviewCamera, GameplayCamera;
        public SalvageRandomMap RandomMap;
        public float ReturnRadius = 7;
        public static bool IsActive => SceneManager.GetActiveScene().name == LobbyMapCatalog.SalvageScene;
        public bool Contains(Vector3 position)
        {
            if(RandomMap!=null&&RandomMap.FixedBase!=null)
            {var local=RandomMap.FixedBase.transform.InverseTransformPoint(position);var bounds=RandomMap.FixedBase.Footprint;return local.x>bounds.min.x+.3f&&local.x<bounds.max.x-.3f&&local.z>bounds.min.z+.3f&&local.z<bounds.max.z-.3f&&local.y>-.5f&&local.y<4;}
            return ReturnPoint!=null&&Vector3.Distance(ReturnPoint.position,position)<=ReturnRadius;
        }
        public bool ContainsCargo(Bounds bounds)
        {return Contains(bounds.min+Vector3.up*.1f)&&Contains(bounds.max);}
        public void EnterGameplay()
        { if (PreviewCamera != null) PreviewCamera.SetActive(false); if (GameplayCamera != null) GameplayCamera.SetActive(true); }
        public Vector3 CrewPosition(int seat) => CrewSpawns != null && seat >= 0 && seat < CrewSpawns.Length && CrewSpawns[seat] != null
            ? CrewSpawns[seat].position : CrewSpawn.position + Vector3.right * (seat * 1.3f);

        public void Begin(NetworkRunner runner)
        {
            if (!runner.IsServer || SalvageRunState.Instance != null) return;
            if (RunPrefab == null || ScrapPrefab == null || StationPrefab == null || CrewSpawn == null
                || ReturnPoint == null || ScrapSpawns == null || ScrapSpawns.Length < 12)
                throw new System.InvalidOperationException("Salvage site is not completely authored.");
            runner.Spawn(RunPrefab, CrewSpawn.position, Quaternion.identity);
        }
    }
}
