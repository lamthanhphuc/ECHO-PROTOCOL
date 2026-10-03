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
    public sealed class SubmitMatchResultRequestDto
    {
        public string outcome;
        public float objectiveCompletion;
        public SubmitMatchResultPlayerDto[] players = Array.Empty<SubmitMatchResultPlayerDto>();
    }

    [Serializable]
    public sealed class SubmitMatchResultPlayerDto
    {
        public string userId;
        public bool survived;
        public bool disconnected;
        public int detectionCount;
        public int downedCount;
        public int reviveCount;
        public int objectiveContribution;
    }

    [Serializable]
    public sealed class MatchResultResponseDto
    {
        public string matchId;
        public string outcome;
        public int durationSeconds;
        public float objectiveCompletion;
        public int playerCount;
        public string rewardStatus;
        public bool isReplay;
    }

    [Serializable]
    public sealed class RewardMeResponseDto
    {
        public string matchId;
        public string rewardStatus;
        public int currencyAmount;
        public int balanceBefore;
        public int balanceAfter;
        public long experiencePointsAwarded;
        public long currentExperiencePoints;
        public int currentLevel;
        public string policyVersion;
        public string progressionPolicyVersion;
        public string processedAtUtc;
    }

    [Serializable]
    public sealed class EmptyMatchAuthorityRequest { }
}
