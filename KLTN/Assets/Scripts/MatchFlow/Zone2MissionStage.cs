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
}

