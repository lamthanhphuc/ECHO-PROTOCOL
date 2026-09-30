using EchoProtocol.Networking;
using Fusion;
using UnityEngine;

namespace EchoProtocol.TeamTools
{
    [CreateAssetMenu(menuName = "Echo Protocol/Team Tools/Pickup Catalog")]
    public sealed class TeamToolPickupCatalog : ScriptableObject
    {
        [SerializeField] private NetworkObject fieldScannerPrefab;
        [SerializeField] private NetworkObject noiseMakerPrefab;
        [SerializeField] private NetworkObject firstAidPrefab;
        [SerializeField] private NetworkObject doorJammerPrefab;
        [SerializeField] private NetworkObject coreStabilizerPrefab;

        public NetworkObject GetPrefab(int toolId) => toolId switch
        {
            LobbyPlayerState.FieldScannerToolId => fieldScannerPrefab,
            LobbyPlayerState.NoiseMakerToolId => noiseMakerPrefab,
            LobbyPlayerState.FirstAidKitToolId => firstAidPrefab,
            LobbyPlayerState.DoorJammerToolId => doorJammerPrefab,
            LobbyPlayerState.CoreStabilizerToolId => coreStabilizerPrefab,
            _ => null,
        };
    }
}
