using System;
using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.TeamTools
{
    public enum TeamToolSpawnZone { Zone1 = 1, Zone2 = 2, Zone3 = 3 }

    [Flags]
    public enum TeamToolSpawnMask
    {
        None = 0,
        Scanner = 1 << 0,
        NoiseMaker = 1 << 1,
        FirstAid = 1 << 2,
        DoorJammer = 1 << 3,
        CoreStabilizer = 1 << 4,
        All = Scanner | NoiseMaker | FirstAid | DoorJammer | CoreStabilizer,
    }

    [DisallowMultipleComponent]
    public sealed class TeamToolSpawnPoint : MonoBehaviour
    {
        [SerializeField] private TeamToolSpawnZone zone = TeamToolSpawnZone.Zone1;
        [SerializeField, Min(1)] private int roomId = 1;
        [SerializeField] private TeamToolSpawnMask allowedTools = TeamToolSpawnMask.All;
        [SerializeField] private bool requireNavMeshAccess = true;
        [SerializeField, Min(0.1f)] private float navMeshAccessRadius = 1.5f;

        public TeamToolSpawnZone Zone => zone;
        public int RoomId => roomId;

        public bool Allows(int toolId)
        {
            TeamToolSpawnMask mask = toolId switch
            {
                LobbyPlayerState.FieldScannerToolId => TeamToolSpawnMask.Scanner,
                LobbyPlayerState.NoiseMakerToolId => TeamToolSpawnMask.NoiseMaker,
                LobbyPlayerState.FirstAidKitToolId => TeamToolSpawnMask.FirstAid,
                LobbyPlayerState.DoorJammerToolId => TeamToolSpawnMask.DoorJammer,
                LobbyPlayerState.CoreStabilizerToolId => TeamToolSpawnMask.CoreStabilizer,
                _ => TeamToolSpawnMask.None,
            };
            return mask != TeamToolSpawnMask.None && (allowedTools & mask) != 0;
        }

        public bool TryGetSpawnPosition(out Vector3 position)
        {
            position = transform.position;
            return !requireNavMeshAccess || NavMesh.SamplePosition(position, out _, navMeshAccessRadius, NavMesh.AllAreas);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() => Gizmos.DrawWireSphere(transform.position, 0.25f);
#endif
    }
}
