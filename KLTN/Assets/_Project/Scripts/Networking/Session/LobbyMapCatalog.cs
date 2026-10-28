using Fusion;

namespace EchoProtocol.Networking
{
    public static class LobbyMapCatalog
    {
        public const string PropertyKey = "map";
        public const string GameModeKey = "gamemode";
        public static int RoomGameMode(NetworkRunner runner) => runner != null && runner.SessionInfo.IsValid
            && runner.SessionInfo.Properties.TryGetValue(GameModeKey, out var value) && (int)value == 1 ? 1 : 0;
        public static bool IsMapAllowed(int mode, int map) => mode == 1 ? map == 2 : map == 0 || map == 1;
        public static bool MapLocked(NetworkRunner runner) => RoomGameMode(runner) == 1;
        public const string ExplorationScene = "Scene_A";
        public const string ExplorationPath = "Assets/Scenes/Scene_A.unity";
        public const int MapCount = 3;
        public static string TelemetryMapId(string scene) => scene == AlienHuntMission.SceneName
            ? "PURGE-SCENE-B" : scene == ExplorationScene ? "EXPEDITION-SCENE-A" : "M2-MAP-1";
        public static string TelemetryGameMode(string scene) => scene == AlienHuntMission.SceneName ? "PURGE" : "EXPEDITION";
        private static NetworkRunner _hostRunner;
        private static int _hostSelection;
        public static bool IsGameplayScene(string name) => name == LobbyManager.GameSceneName || name == ExplorationScene || name == AlienHuntMission.SceneName;
        public static int Selected(NetworkRunner runner)
        {
            if (MapLocked(runner)) return 2;
            if (runner != null && runner.IsServer && runner == _hostRunner) return _hostSelection;
            if (runner != null && runner.SessionInfo.IsValid
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
    }
}

