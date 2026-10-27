using System.Collections.Generic;
using EchoProtocol.Networking;
using EchoProtocol.Settings;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    public sealed class LobbyGameplaySettings : MonoBehaviour
    {
        public Button[] Previous, Next;
        public TMP_Text[] Values;
        public static readonly string[] Keys = { "playerCollisions", "proximityVoice", "medkits" };
        private static NetworkBootstrap _bootstrap;
        private float _refreshAt;

        public static bool Enabled(string key, NetworkRunner runner = null)
        {
            if (runner == null) {
                if (_bootstrap == null) _bootstrap = Object.FindAnyObjectByType<NetworkBootstrap>();
                runner = _bootstrap != null ? _bootstrap.Runner : null;
            }
            return runner == null || runner.SessionInfo.Properties == null
                || !runner.SessionInfo.Properties.TryGetValue(key, out var value) || (int)value != 0;
        }

        private void Start()
        {
            for (int i = 0; i < Keys.Length; i++) {
                int index = i;
                Previous[i].onClick.AddListener(() => Toggle(index));
                Next[i].onClick.AddListener(() => Toggle(index));
            }
        }
        private void Toggle(int index)
        {
            if (_bootstrap == null) _bootstrap = Object.FindAnyObjectByType<NetworkBootstrap>();
            if (_bootstrap == null || _bootstrap.State != NetworkSessionState.InLobby
                || _bootstrap.Runner == null || !_bootstrap.Runner.IsServer) return;
            int value = Enabled(Keys[index]) ? 0 : 1;
            if (_bootstrap.UpdateRoomProperties(new Dictionary<string, SessionProperty> {
                [Keys[index]] = value
            })) LobbySelectionMemory.Write(Keys[index], value);
        }
        private void Update()
        {
            if (Time.unscaledTime < _refreshAt) return;
            _refreshAt = Time.unscaledTime + .2f;
            if (_bootstrap == null) _bootstrap = Object.FindAnyObjectByType<NetworkBootstrap>();
            bool canEdit = _bootstrap != null && _bootstrap.State == NetworkSessionState.InLobby
                && _bootstrap.Runner != null && _bootstrap.Runner.IsServer
                && _bootstrap.Runner.SessionInfo.IsValid;
            for (int i = 0; i < Keys.Length; i++) {
                Previous[i].interactable = Next[i].interactable = canEdit;
                Values[i].text = Enabled(Keys[i]) ? GameLanguage.Choose("Bật", "On") : GameLanguage.Choose("Tắt", "Off");
            }
        }
    }

    // Apply only to player collider pairs; world geometry and enemies retain their collisions.
    public sealed class RoomPlayerCollisionSettings : MonoBehaviour
    {
        private float _refreshAt;
        private int _lastSignature;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var root = new GameObject("RoomPlayerCollisionSettings");
            DontDestroyOnLoad(root);
            root.AddComponent<RoomPlayerCollisionSettings>();
        }
        private void Update()
        {
            if (Time.unscaledTime < _refreshAt) return;
            _refreshAt = Time.unscaledTime + .5f;
            var players = Object.FindObjectsByType<LobbyPlayerState>(FindObjectsSortMode.None);
            bool ignore = SceneManager.GetActiveScene().name != "Lobby" && !LobbyGameplaySettings.Enabled("playerCollisions");
            int signature = ignore ? 1 : 2;
            foreach (var player in players) signature ^= player.GetEntityId().GetHashCode();
            if (signature == _lastSignature) return;
            _lastSignature = signature;
            for (int i = 0; i < players.Length; i++)
                for (int j = i + 1; j < players.Length; j++)
                    foreach (var a in players[i].GetComponentsInChildren<Collider>())
                        foreach (var b in players[j].GetComponentsInChildren<Collider>())
                            if (!a.isTrigger && !b.isTrigger && a.enabled && b.enabled)
                                Physics.IgnoreCollision(a, b, ignore);
        }
    }
}
