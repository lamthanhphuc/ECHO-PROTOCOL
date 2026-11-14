using System.Collections;
using System.Collections.Generic;
using EchoProtocol.Networking;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace EchoProtocol.UI
{
    public sealed class GameplayChatController : MonoBehaviour
    {
        [SerializeField] private TMP_Text history;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private GameObject inputPanel;

        private readonly PlayerInteractionControlLock _lock = new();
        private readonly Queue<string> _messages = new();
        private int _openedFrame;
        private int _closedFrame = -1;
        private float _lastMessageTime;
        private string _composition = string.Empty;
        private int _compositionFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
            OnSceneChanged(default, SceneManager.GetActiveScene());
        }

        private static void OnSceneChanged(Scene previous, Scene current)
        {
            if (!LobbyMapCatalog.IsGameplayScene(current.name))
                return;

            if (FindAnyObjectByType<GameplayChatController>() != null)
                return;

            var prefab = Resources.Load<GameplayChatController>("UI/GameplayChat");

            if (prefab == null)
            {
                Debug.LogError("[Chat] Missing Resources/UI/GameplayChat prefab.");
                return;
            }

            Instantiate(prefab);
        }

        private void Awake()
        {
            if (history == null || input == null || inputPanel == null)
            {
                Debug.LogError("[Chat] Missing Inspector references.");
                enabled = false;
                return;
            }

            history.richText = false;
            UiTypographyTheme.Apply(history);
            history.color = IndustrialUiStyle.Text;
            history.fontSize = 16;
            UiTypographyTheme.Apply(input.textComponent);
            input.textComponent.color = IndustrialUiStyle.Text;
            input.textComponent.fontSize = 16;
            if (input.placeholder is TMP_Text placeholder)
            {
                UiTypographyTheme.Apply(placeholder);
                placeholder.color = IndustrialUiStyle.Muted;
                placeholder.fontSize = 16;
            }
            input.characterLimit = 120;
            input.lineType = TMP_InputField.LineType.SingleLine;
            inputPanel.SetActive(false);
            history.transform.parent.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (input == null) return;

            GameplayChatRpc.MessageReceived += Receive;
            input.onSubmit.AddListener(Submit);
            if (Keyboard.current != null)
                Keyboard.current.onIMECompositionChange += OnCompositionChanged;
        }

        private void OnDisable()
        {
            GameplayChatRpc.MessageReceived -= Receive;
            if (input != null)
                input.onSubmit.RemoveListener(Submit);
            if (Keyboard.current != null)
                Keyboard.current.onIMECompositionChange -= OnCompositionChanged;

            Close();
        }

        private void Update()
        {
            if (!_lock.IsLocked && history.transform.parent.gameObject.activeSelf
                && Time.unscaledTime - _lastMessageTime >= 10f)
                history.transform.parent.gameObject.SetActive(false);

            var keyboard = Keyboard.current;
            if (keyboard == null || !Application.isFocused)
                return;

            if (_lock.IsLocked)
            {
                if (_lock.ShouldAutoRelease() || _lock.ConsumeEscape())
                    Close();
                return;
            }

            if (PlayerInteractionControlLock.HasModal
                || PlayerInteractionControlLock.IsGameplayInputBlocked())
                return;

            if (Time.frameCount != _closedFrame
                && (keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame))
                Open();
        }

        private void Open()
        {
            var runner = NetworkBootstrap.Instance?.Runner;

            if (runner == null || !runner.IsRunning
                || NetworkBootstrap.IsHostMigrationReconciliationPending
                || !runner.TryGetPlayerObject(runner.LocalPlayer, out var player)
                || player == null || !player.IsValid || !player.HasInputAuthority)
                return;

            _lock.Acquire(player.gameObject, Close, allowWhileDowned: true);

            if (!_lock.IsLocked)
                return;

            _openedFrame = Time.frameCount;
            inputPanel.SetActive(true);
            history.transform.parent.gameObject.SetActive(true);
            input.SetTextWithoutNotify("");
            StartCoroutine(FocusInput());
        }

        private IEnumerator FocusInput()
        {
            yield return null;

            if (!_lock.IsLocked || !inputPanel.activeInHierarchy)
                yield break;

            EventSystem.current?.SetSelectedGameObject(input.gameObject);
            input.ActivateInputField();
            yield return null;
            if (!input.isFocused)
                Debug.LogWarning($"[Chat] Input did not gain focus; eventSystem={EventSystem.current != null}, font={input.textComponent?.font != null}.");
        }

        private void Submit(string value)
        {
            Debug.Log($"[Chat] Input submit length={value?.Length ?? 0} focused={input.isFocused} locked={_lock.IsLocked} topmost={_lock.IsTopmost}.");
            if (!_lock.IsLocked || !_lock.IsTopmost
                || Time.frameCount == _openedFrame)
                return;

            // Enter may commit a Windows IME composition before its text reaches TMP.
            if (_composition.Length > 0 || _compositionFrame == Time.frameCount)
            {
                StartCoroutine(FocusInput());
                return;
            }

            var runner = NetworkBootstrap.Instance?.Runner;
            string message = GameplayChatRpc.Normalize(value);

            if (runner != null && runner.IsRunning
                && !NetworkBootstrap.IsHostMigrationReconciliationPending
                && message.Length > 0)
            {
                Debug.Log($"[Chat] Submit local={runner.LocalPlayer} server={runner.IsServer} length={message.Length}");
                GameplayChatRpc.RPC_Submit(runner, message);
            }
            else if (message.Length > 0)
                Debug.LogWarning("[Chat] Submit skipped: runner unavailable or migration pending.");

            Close();
        }

        private void OnCompositionChanged(UnityEngine.InputSystem.LowLevel.IMECompositionString composition)
        {
            _composition = composition.ToString();
            _compositionFrame = Time.frameCount;
        }

        private void Close()
        {
            if (_lock.IsLocked)
                _closedFrame = Time.frameCount;
            _composition = string.Empty;
            if (input != null)
            {
                input.DeactivateInputField();

                if (EventSystem.current != null
                    && EventSystem.current.currentSelectedGameObject == input.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
            }

            if (inputPanel != null)
                inputPanel.SetActive(false);

            if (history != null)
                history.transform.parent.gameObject.SetActive(_messages.Count > 0
                    && Time.unscaledTime - _lastMessageTime < 10f);

            _lock.Release();
        }

        private void Receive(PlayerRef sender, string message)
        {
            var runner = NetworkBootstrap.Instance?.Runner;
            if (runner == null)
                return;

            string senderName = sender.ToString();

            if (runner.TryGetPlayerObject(sender, out var player)
                && player != null
                && player.TryGetComponent<LobbyPlayerState>(out var state))
            {
                string name = state.OperatorName.ToString();

                if (!string.IsNullOrWhiteSpace(name))
                    senderName = name;
            }

            _messages.Enqueue(senderName + ": " + message);

            while (_messages.Count > 6)
                _messages.Dequeue();

            history.text = string.Join("\n", _messages);
            _lastMessageTime = Time.unscaledTime;
            history.transform.parent.gameObject.SetActive(true);
        }
    }
}
