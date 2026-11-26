using System;
using EchoProtocol.Networking;
using UnityEngine;

namespace EchoProtocol.Salvage
{
    public static class SalvagePlaytest
    {
        public const string RequireBackendStartArgument = "-salvage-require-backend-start";

        public static bool SkipBackendStart(int gameMode) => AllowsBackendStartBypass(
            Debug.isDebugBuild, gameMode,
            Array.Exists(Environment.GetCommandLineArgs(), argument =>
                string.Equals(argument, RequireBackendStartArgument, StringComparison.OrdinalIgnoreCase)));

        public static bool AllowsBackendStartBypass(bool developmentBuild, int gameMode, bool requireBackendStart)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return developmentBuild && gameMode == LobbyMapCatalog.SalvageMode && !requireBackendStart;
#else
            return false;
#endif
        }
    }
}
