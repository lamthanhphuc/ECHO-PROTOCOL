using System;

namespace EchoProtocol.Networking
{
    [Serializable]
    public class RoomInfoViewModel
    {
        public string RoomName;
        public int MaxPlayers;
        public int CurrentPlayers;
        public bool IsHost;
        public bool IsReady;
    }
}
