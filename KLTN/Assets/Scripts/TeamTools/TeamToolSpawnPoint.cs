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
            if (!requireNavMeshAccess) return true;
            if (!NavMesh.SamplePosition(position, out var hit, navMeshAccessRadius, NavMesh.AllAreas)) return false;

            var horizontalOffset = Vector3.ProjectOnPlane(hit.position - position, Vector3.up);
            return horizontalOffset.sqrMagnitude <= 0.5f * 0.5f;
        }

        public bool TryGetReachablePosition(
            Vector3 zoneEntry,
            out Vector3 position,
            out float pathDistance)
        {
            position = default;
            pathDistance = 0f;

            if (!TryGetSpawnPosition(out position))
                return false;

            const float maxVerticalOffset = 1f;
            const float maxHorizontalOffset = 0.75f;

            if (!NavMesh.SamplePosition(zoneEntry, out var start, 2f, NavMesh.AllAreas))
                return false;
            if (Mathf.Abs(start.position.y - zoneEntry.y) > maxVerticalOffset)
                return false;
            if (!NavMesh.SamplePosition(position, out var destination, 1.5f, NavMesh.AllAreas))
                return false;
            if (Mathf.Abs(destination.position.y - position.y) > maxVerticalOffset)
                return false;

            var horizontal = Vector3.ProjectOnPlane(destination.position - position, Vector3.up);
            if (horizontal.magnitude > maxHorizontalOffset)
                return false;

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, destination.position,
                NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;

            var corners = path.corners;
            if (corners == null || corners.Length == 0)
                return false;
            for (int i = 1; i < corners.Length; i++)
                pathDistance += Vector3.Distance(corners[i - 1], corners[i]);

            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() => Gizmos.DrawWireSphere(transform.position, 0.25f);
#endif
    }
}
