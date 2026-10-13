using EchoProtocol.Networking;
using EchoProtocol.Voice;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace EchoProtocol.UI
{
    [DefaultExecutionOrder(1000)]
    public sealed class GameplayMenuController : MonoBehaviour
    {
        private readonly PlayerInteractionControlLock _lock = new PlayerInteractionControlLock();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHandler()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
            OnSceneChanged(default, SceneManager.GetActiveScene());
        }

        private static void OnSceneChanged(Scene previous, Scene current)
        {
            if (current.name != LobbyManager.GameSceneName) return;
            if (FindAnyObjectByType<GameplayMenuController>() != null) return;
            var owner = new GameObject("GameplayMenus");
            owner.AddComponent<GameplayMenuController>();
        }

        private void Awake()
        {
            EnsureEventSystem();
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != LobbyManager.GameSceneName)
            {
                if (_lock.IsLocked) Close();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (_lock.IsLocked)
            {
                if (_lock.ShouldAutoRelease()) Close();
                else if (!_lock.IsTopmost) return;
                else if (_lock.ConsumeEscape()) Close();
                return;
            }

            if (PlayerInteractionControlLock.HasModal || PlayerInteractionControlLock.EscapeConsumedThisFrame
                || PlayerInteractionControlLock.IsGameplayInputBlocked()) return;
            if (keyboard.escapeKey.wasPressedThisFrame) Open();
        }

        private void Open()
        {
            var camera = FindAnyObjectByType<PlayerCamera>();
            if (camera == null || camera.Target == null) return;
            var player = camera.Target.gameObject;
            var networkObject = player.GetComponentInParent<Fusion.NetworkObject>();
            if (networkObject != null && networkObject.IsValid && !networkObject.HasInputAuthority) return;
            _lock.Acquire(player, Close, allowWhileDowned: true);
            if (!_lock.IsLocked) return;
            VoiceManager.EnsureExists();
            var settings = VoiceManager.Instance.GetComponent<VoiceSettingsPanel>();
            if (!settings.OpenFromGameplayMenu(_lock, Close)) Close();
        }

        private void Close()
        {
            var voicePanel = VoiceManager.Instance != null
                ? VoiceManager.Instance.GetComponent<VoiceSettingsPanel>() : null;
            voicePanel?.CloseFromGameplayMenu(_lock);
            _lock.Release();
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var owner = new GameObject("GameplayEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            owner.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void OnDisable() => Close();
        private void OnDestroy() => Close();
    }
}
