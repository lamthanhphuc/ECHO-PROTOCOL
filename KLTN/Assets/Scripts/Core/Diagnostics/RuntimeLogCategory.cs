using System;

namespace EchoProtocol.Diagnostics
{
    [Flags]
    public enum RuntimeLogCategory
    {
        None = 0,

        NetworkSession = 1 << 0,
        MatchAuthority = 1 << 1,
        Lobby = 1 << 2,
        PlayerLifecycle = 1 << 3,
        PlayerSpawner = 1 << 4,
        PlayerMovement = 1 << 5,
        MatchState = 1 << 6,
        Objective = 1 << 7,
        PowerPuzzle = 1 << 8,

        Aed = 1 << 9,

        StalkerLifecycle = 1 << 10,
        StalkerPatrol = 1 << 11,
        StalkerHearing = 1 << 12,
        StalkerAed = 1 << 13,
        StalkerCombat = 1 << 14,
        StalkerChase = 1 << 15,
        StalkerDiagnostics = 1 << 16,

        Interaction = 1 << 17,
        Door = 1 << 18,
        Inventory = 1 << 19,

        All =
            NetworkSession
            | MatchAuthority
            | Lobby
            | PlayerLifecycle
            | PlayerSpawner
            | PlayerMovement
            | MatchState
            | Objective
            | PowerPuzzle
            | Aed
            | StalkerLifecycle
            | StalkerPatrol
            | StalkerHearing
            | StalkerAed
            | StalkerCombat
            | StalkerChase
            | StalkerDiagnostics
            | Interaction
            | Door
            | Inventory
    }
}
