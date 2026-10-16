using Fusion;

namespace EchoProtocol.Networking
{
    public static class LobbyMapCatalog
    {
        public const string PropertyKey = "map";
        public const string ExplorationScene = "Scene_A";
        public const string ExplorationPath = "Assets/Scenes/Scene_A.unity";
        private static NetworkRunner _hostRunner;
        private static int _hostSelection;
        public static bool IsGameplayScene(string name) => name == LobbyManager.GameSceneName || name == ExplorationScene;
        public static int Selected(NetworkRunner runner)
        {
            if (runner != null && runner.IsServer && runner == _hostRunner) return _hostSelection;
            if (runner != null && runner.SessionInfo.IsValid
                && runner.SessionInfo.Properties.TryGetValue(PropertyKey, out var value))
                return (int)value == 1 ? 1 : 0;
            return 0;
        }
        public static bool TrySelect(NetworkRunner runner, int map)
        {
            if (runner == null || !runner.IsRunning || !runner.IsServer || !runner.SessionInfo.IsValid
                || map < 0 || map > 1) return false;
            bool accepted = runner.SessionInfo.UpdateCustomProperties(
                new System.Collections.Generic.Dictionary<string, SessionProperty> { [PropertyKey] = map });
            if (!accepted) return false;
            _hostRunner = runner;
            _hostSelection = map;
            return true;
        }
        public static string SceneName(int map) => map == 1 ? ExplorationScene : LobbyManager.GameSceneName;
    }
}

