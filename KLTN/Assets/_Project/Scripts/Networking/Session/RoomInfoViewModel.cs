using System;
using System.Collections.Generic;
using Fusion;

namespace EchoProtocol.Networking
{
    [Serializable]
    public class RoomInfoViewModel
    {
        public string RoomName = string.Empty;
        public int MaxPlayers;
        public int CurrentPlayers;
        public bool IsHost;
        public bool IsReady;
        public bool CanStartMatch;
        public List<LobbyMemberViewModel> Members = new List<LobbyMemberViewModel>();
    }

    [Serializable]
    public class LobbyMemberViewModel
    {
        public PlayerRef PlayerRef;
        public int ActorId;
        public bool IsLocal;
        public bool IsReady;
        public int TeamId;
        public int ToolId;

        public string DisplayName => $"Player {ActorId}";
    }
}
