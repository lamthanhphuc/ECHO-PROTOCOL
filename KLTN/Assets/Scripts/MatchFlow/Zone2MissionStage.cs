using System;

namespace EchoProtocol.MatchFlow
{
    public enum Zone2MissionStage
    {
        Zone1CoreObjective = 0,
        FindSecurityTerminal = 1,
        RepairRelays = 2,
        SecurityHoldReady = 3,
        SecurityHold = 4,
        AuthorizationCodeGranted = 5,
        UnlockZoneDoors = 6,
        Zone2Completed = 7,
    }

    public enum RelaySlot
    {
        RelayA_1 = 0,
        RelayA_2 = 1,
        RelayB_1 = 2,
        RelayB_2 = 3,
    }

    public enum Zone2NetworkCommandResult
    {
        Accepted = 0,
        InvalidRequester = 1,
        InvalidTarget = 2,
        OutOfRange = 3,
        InvalidStage = 4,
        AlreadyComplete = 5,
        NotOperator = 6,
        InvalidCode = 7,
        Cooldown = 8,
    }

    public enum Zone2AccessSubmissionDisposition
    {
        Rejected = 0,
        Accepted = 1,
        Pending = 2,
    }

    public readonly struct Zone2AccessCodeResult
    {
        public Zone2AccessCodeResult(
            int panelIndex,
            Zone2NetworkCommandResult result,
            float cooldownSeconds = 0f)
        {
            PanelIndex = panelIndex;
            Result = result;
            CooldownSeconds = cooldownSeconds < 0f
                ? 0f
                : cooldownSeconds;
        }

        public int PanelIndex { get; }
        public Zone2NetworkCommandResult Result { get; }
        public float CooldownSeconds { get; }
        public bool Accepted => Result == Zone2NetworkCommandResult.Accepted;
    }
}

