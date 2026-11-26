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
