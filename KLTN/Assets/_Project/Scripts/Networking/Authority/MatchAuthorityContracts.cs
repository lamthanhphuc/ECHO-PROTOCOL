using System;

namespace EchoProtocol.Networking.Authority
{
    [Serializable]
    public sealed class CreateMatchAuthorityRequest
    {
        public string fusionSessionName;
        public int maxPlayers;
    }

    [Serializable]
    public sealed class MatchAuthorityDto
    {
        public string matchId;
        public string fusionSessionName;
        public string hostUserId;
        public int maxPlayers;
        public string status;
        public string leaseExpiresAtUtc;
    }

    [Serializable]
    public sealed class IssueJoinProofRequest
    {
        public int fusionActorNumber;
        public string fusionSessionName;
    }

    [Serializable]
    public sealed class JoinProofDto
    {
        public string proof;
        public string expiresAtUtc;
    }

    [Serializable]
    public sealed class BindMatchPlayerRequest
    {
        public int fusionActorNumber;
        public string joinProof;
    }

    [Serializable]
    public sealed class MatchPlayerBindingDto
    {
        public string userId;
        public int fusionActorNumber;
        public string boundAtUtc;
    }

    [Serializable]
    public sealed class EndMatchAuthorityRequest
    {
        public string reason;
    }

    [Serializable]
    public sealed class EmptyMatchAuthorityRequest { }
}
