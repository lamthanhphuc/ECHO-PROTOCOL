using Fusion;

namespace EchoProtocol.Networking
{
    public static class LobbyMapCatalog
    {
        public const string PropertyKey = "map";
        public const string GameModeKey = "gamemode";
        public const int SalvageMode = 2;
        public const string SalvageScene = "Salvage_SciFi";
        public const string SalvagePath = "Assets/Salvage/Scenes/Salvage_SciFi.unity";

        public static int RoomGameMode(NetworkRunner runner)
        {
            if (runner != null && runner.SessionInfo.Properties != null
                && runner.SessionInfo.Properties.TryGetValue(GameModeKey, out var value))
            {
                int mode = (int)value;
                if (mode == 1 || mode == SalvageMode) return mode;
            }
            return 0;
        }
        public static bool IsMapAllowed(int mode, int map) => mode == 1 ? map == 2
            : mode == SalvageMode ? map == 0 : mode == 0 && (map == 0 || map == 1);
        public static bool MapLocked(NetworkRunner runner) => RoomGameMode(runner) != 0;
        public const string ExplorationScene = "Scene_A";
        public const string ExplorationPath = "Assets/Scenes/Scene_A.unity";
        public const int MapCount = 3;
        public static string TelemetryMapId(string scene) => scene == AlienHuntMission.SceneName
            ? "PURGE-SCENE-B" : scene == SalvageScene ? "SALVAGE-SCIFI"
            : scene == ExplorationScene ? "EXPEDITION-SCENE-A" : "M2-MAP-1";
        public static string TelemetryGameMode(string scene) => scene == AlienHuntMission.SceneName ? "PURGE"
            : scene == SalvageScene ? "SALVAGE" : "EXPEDITION";
        private static NetworkRunner _hostRunner;
        private static int _hostSelection;
        public static bool IsGameplayScene(string name) => name == LobbyManager.GameSceneName || name == ExplorationScene
            || name == AlienHuntMission.SceneName || name == SalvageScene;
        public static bool SupportsGameplayHostMigration(string name) => name == LobbyManager.GameSceneName || name == ExplorationScene || name == SalvageScene;
        public static bool IsChatScene(string name) => name == NetworkBootstrap.LobbySceneName || IsGameplayScene(name);
        public static int Selected(NetworkRunner runner)
        {
            int mode = RoomGameMode(runner);
            if (mode == 1) return 2;
            if (mode == SalvageMode) return 0;
            if (runner != null && runner.IsServer && runner == _hostRunner) return _hostSelection;
            if (runner != null && runner.SessionInfo.Properties != null
                && runner.SessionInfo.Properties.TryGetValue(PropertyKey, out var value))
                return UnityEngine.Mathf.Clamp((int)value, 0, 1);
            return 0;
        }
        public static bool TrySelect(NetworkRunner runner, int map)
        {
            if (runner == null || !runner.IsRunning || !runner.IsServer || !runner.SessionInfo.IsValid
                || !IsMapAllowed(RoomGameMode(runner), map)) return false;
            bool accepted = runner.SessionInfo.UpdateCustomProperties(
                new System.Collections.Generic.Dictionary<string, SessionProperty> { [PropertyKey] = map });
            if (!accepted) return false;
            _hostRunner = runner;
            _hostSelection = map;
            return true;
        }
        public static string SceneName(int map) => map == 2 ? AlienHuntMission.SceneName : map == 1 ? ExplorationScene : LobbyManager.GameSceneName;
        // SciFi is the logical map for both modes; Salvage always loads its own copy.
        public static string SceneName(int mode, int map) => mode == SalvageMode ? SalvageScene : SceneName(map);
    }
}

