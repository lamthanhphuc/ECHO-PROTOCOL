using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.Networking;
using UnityEngine;
namespace EchoProtocol.Audio
{
    // The registry follows enable/disable, including pooled network objects.
    public sealed class GameAudioEnvironmentSource : MonoBehaviour
    {
        public string RoomKey { get; private set; }
        public StalkerFusionRuntime Stalker { get; private set; }
        private void Awake()
        {
            if (GetComponent<EchoProtocol.RelayA.RelayAController>()!=null) RoomKey="map_ambience/electrical_room_loop";
            else if (GetComponent<EchoProtocol.RelayB.RelayBController>()!=null) RoomKey="map_ambience/server_room_loop";
            else if (GetComponent<SecurityTerminalDownload>()!=null) RoomKey="map_ambience/hvac_loop";
            Stalker=GetComponent<StalkerFusionRuntime>();
            if (GetComponent<EchoProtocol.AI.Stalker.Presentation.StalkerAudioController>()!=null) Stalker=null;
        }
        private void OnEnable() => GameAudioRuntime.RegisterEnvironment(this);
        private void OnDisable() => GameAudioRuntime.UnregisterEnvironment(this);
    }
}
