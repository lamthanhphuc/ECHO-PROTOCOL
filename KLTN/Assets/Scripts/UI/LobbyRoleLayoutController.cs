using EchoProtocol.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyRoleLayoutController : MonoBehaviour
    {
        [Header("Role UI")]
        [SerializeField] private TMP_Text roleBadgeText;
        [SerializeField] private TMP_Text difficultyAuthorityText;

        [Header("Controls")]
        [SerializeField] private TMP_Dropdown difficultyDropdown;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startButton;

        [Header("Networking")]
        [SerializeField] private LobbyManager lobbyManager;

        private LobbyManager _subscribedLobby;
        private float _nextResolveAt;

        private static readonly Color HostColor =
            new Color32(138, 168, 143, 255);

        private static readonly Color ClientColor =
            new Color32(151, 155, 153, 255);

        private void OnEnable()
        {
            ResolveLobbyManager();
            RefreshRole();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
            {
                return;
            }

            _nextResolveAt =
                Time.unscaledTime + 0.5f;

            ResolveLobbyManager();
            RefreshRole();
        }

        private void ResolveLobbyManager()
        {
            if (lobbyManager == null)
            {
                lobbyManager =
                    FindAnyObjectByType<LobbyManager>();
            }

            if (_subscribedLobby == lobbyManager)
            {
                return;
            }

            Unsubscribe();

            _subscribedLobby =
                lobbyManager;

            if (_subscribedLobby != null)
            {
                _subscribedLobby.OnRoomUpdated +=
                    HandleRoomUpdated;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedLobby != null)
            {
                _subscribedLobby.OnRoomUpdated -=
                    HandleRoomUpdated;
            }

            _subscribedLobby = null;
        }

        private void HandleRoomUpdated(
            RoomInfoViewModel room)
        {
            ApplyRole(room);
        }

        private void RefreshRole()
        {
            var room =
                lobbyManager != null
                    ? lobbyManager.CurrentState
                    : null;

            ApplyRole(room);
        }

        private void ApplyRole(
            RoomInfoViewModel room)
        {
            bool connected =
                lobbyManager != null
                && lobbyManager.IsInRoom;

            bool isHost =
                connected
                && room != null
                && room.IsHost;

            RefreshBadge(
                connected,
                isHost);

            RefreshDifficultyPresentation(
                connected,
                isHost);

            RefreshActions(
                connected,
                isHost);
        }

        private void RefreshBadge(
            bool connected,
            bool isHost)
        {
            if (roleBadgeText == null)
            {
                return;
            }

            if (!connected)
            {
                roleBadgeText.text =
                    "OFFLINE";

                roleBadgeText.color =
                    ClientColor;

                return;
            }

            roleBadgeText.text =
                isHost
                    ? "HOST"
                    : "CLIENT";

            roleBadgeText.color =
                isHost
                    ? HostColor
                    : ClientColor;
        }

        private void RefreshDifficultyPresentation(
            bool connected,
            bool isHost)
        {
            if (difficultyAuthorityText != null)
            {
                difficultyAuthorityText.text =
                    !connected
                        ? string.Empty
                        : isHost
                            ? "HOST CONTROL"
                            : "HOST CONTROLLED";

                difficultyAuthorityText.color =
                    isHost
                        ? HostColor
                        : ClientColor;
            }

            if (difficultyDropdown == null)
            {
                return;
            }

            var group =
                difficultyDropdown.GetComponent<CanvasGroup>();

            if (group != null)
            {
                group.alpha =
                    !connected || isHost
                        ? 1f
                        : 0.58f;
            }

            // NetworkLobbyUI remains authoritative over
            // interactable state. This controller only
            // changes presentation.
        }

        private void RefreshActions(
            bool connected,
            bool isHost)
        {
            if (startButton != null)
            {
                startButton.gameObject.SetActive(
                    connected && isHost);

                SetTopLeft(
                    startButton.transform as RectTransform,
                    32f,
                    580f,
                    390f,
                    64f);
            }

            if (readyButton == null)
            {
                return;
            }

            // Host has READY + START MISSION.
            if (connected && isHost)
            {
                SetTopLeft(
                    readyButton.transform as RectTransform,
                    32f,
                    510f,
                    390f,
                    52f);
            }
            else
            {
                // For Client, READY becomes the main action
                // in the space normally occupied by START.
                SetTopLeft(
                    readyButton.transform as RectTransform,
                    32f,
                    580f,
                    390f,
                    64f);
            }
        }

        private static void SetTopLeft(
            RectTransform rect,
            float x,
            float y,
            float width,
            float height)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin =
                new Vector2(0f, 1f);

            rect.anchorMax =
                new Vector2(0f, 1f);

            rect.pivot =
                new Vector2(0f, 1f);

            rect.anchoredPosition =
                new Vector2(x, -y);

            rect.sizeDelta =
                new Vector2(width, height);
        }
    }
}
