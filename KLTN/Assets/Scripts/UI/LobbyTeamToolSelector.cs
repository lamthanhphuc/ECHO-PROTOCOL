using System.Collections.Generic;
using EchoProtocol.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI
{
    [DisallowMultipleComponent]
    public sealed class LobbyTeamToolSelector : MonoBehaviour
    {
        [Header("Controls")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private TMP_Text feedbackText;

        [Header("Networking")]
        [SerializeField] private LobbyManager lobbyManager;

        private RoomInfoViewModel _room =
            new RoomInfoViewModel();

        private float _feedbackClearAt;

        private void OnEnable()
        {
            ResolveLobbyManager();

            if (previousButton != null)
                previousButton.onClick.AddListener(OnPrevious);

            if (nextButton != null)
                nextButton.onClick.AddListener(OnNext);

            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            if (previousButton != null)
                previousButton.onClick.RemoveListener(OnPrevious);

            if (nextButton != null)
                nextButton.onClick.RemoveListener(OnNext);

            Unsubscribe();
        }

        private void Update()
        {
            if (_feedbackClearAt > 0f
                && Time.unscaledTime >= _feedbackClearAt)
            {
                _feedbackClearAt = 0f;

                if (feedbackText != null)
                    feedbackText.text = string.Empty;
            }
        }

        private void ResolveLobbyManager()
        {
            if (lobbyManager == null)
            {
                lobbyManager =
                    FindAnyObjectByType<LobbyManager>();
            }
        }

        private void Subscribe()
        {
            if (lobbyManager == null)
                return;

            lobbyManager.OnRoomUpdated += HandleRoomUpdated;

            lobbyManager.OnSelectionRequestCompleted +=
                HandleSelectionResult;
        }

        private void Unsubscribe()
        {
            if (lobbyManager == null)
                return;

            lobbyManager.OnRoomUpdated -= HandleRoomUpdated;

            lobbyManager.OnSelectionRequestCompleted -=
                HandleSelectionResult;
        }

        private void HandleRoomUpdated(
            RoomInfoViewModel room)
        {
            _room =
                room ?? new RoomInfoViewModel();

            Refresh();
        }

        private void OnPrevious()
        {
            Cycle(-1);
        }

        private void OnNext()
        {
            Cycle(1);
        }

        private void Cycle(int direction)
        {
            ResolveLobbyManager();

            if (lobbyManager == null)
            {
                ShowFeedback(
                    "NETWORK UNAVAILABLE");
                return;
            }

            if (_room == null || _room.IsReady)
            {
                ShowFeedback(
                    "UNREADY TO CHANGE TOOL");
                return;
            }

            if (!lobbyManager.TryGetLocalPlayerState(
                    out var playerState,
                    false))
            {
                ShowFeedback(
                    "PLAYER NOT AVAILABLE");
                return;
            }

            var toolIds =
                BuildToolIdList(playerState);

            if (toolIds.Count <= 1)
            {
                ShowFeedback(
                    "NO TEAM TOOLS");
                return;
            }

            int current =
                GetLocalToolId();

            int index =
                toolIds.IndexOf(current);

            if (index < 0)
                index = 0;

            index =
                (index + direction)
                % toolIds.Count;

            if (index < 0)
                index += toolIds.Count;

            int requested =
                toolIds[index];

            ShowFeedback(
                "REQUESTING...",
                1f);

            if (!lobbyManager.RequestTool(requested))
            {
                ShowFeedback(
                    "REQUEST REJECTED");
            }
        }

        private static List<int> BuildToolIdList(
            LobbyPlayerState playerState)
        {
            var result =
                new List<int> { 0 };

            if (playerState?.ToolDefinitions == null)
                return result;

            foreach (var definition
                     in playerState.ToolDefinitions)
            {
                if (definition == null)
                    continue;

                if (!result.Contains(definition.Id))
                    result.Add(definition.Id);
            }

            return result;
        }

        private int GetLocalToolId()
        {
            if (_room?.Members == null)
                return 0;

            foreach (var member in _room.Members)
            {
                if (member != null && member.IsLocal)
                    return member.ToolId;
            }

            return 0;
        }

        private void Refresh()
        {
            ResolveLobbyManager();

            if (lobbyManager != null)
            {
                _room =
                    lobbyManager.CurrentState
                    ?? new RoomInfoViewModel();
            }

            LobbyPlayerState playerState = null;

            bool hasPlayer =
                lobbyManager != null
                && lobbyManager.TryGetLocalPlayerState(
                    out playerState,
                    false);

            bool hasTools =
                hasPlayer
                && playerState.ToolDefinitions != null
                && playerState.ToolDefinitions.Count > 0;

            bool canChange =
                hasTools
                && !_room.IsReady
                && lobbyManager.IsInRoom;

            if (previousButton != null)
                previousButton.interactable = canChange;

            if (nextButton != null)
                nextButton.interactable = canChange;

            RefreshValue(
                hasPlayer
                    ? playerState
                    : null);
        }

        private void RefreshValue(
            LobbyPlayerState playerState)
        {
            if (valueText == null)
                return;

            int toolId =
                GetLocalToolId();

            if (toolId == 0)
            {
                valueText.text = "NONE";
                return;
            }

            if (playerState?.ToolDefinitions != null)
            {
                foreach (var definition
                         in playerState.ToolDefinitions)
                {
                    if (definition != null
                        && definition.Id == toolId)
                    {
                        valueText.text =
                            definition.DisplayName
                                .ToUpperInvariant();

                        return;
                    }
                }
            }

            valueText.text =
                $"TOOL {toolId}";
        }

        private void HandleSelectionResult(
            LobbySelectionResult result)
        {
            if (result.Kind != LobbySelectionKind.Tool)
                return;

            if (result.Accepted)
            {
                ShowFeedback(
                    "SELECTED",
                    1.5f);
            }
            else
            {
                ShowFeedback(
                    GetErrorMessage(result.Error));
            }

            Refresh();
        }

        private static string GetErrorMessage(
            LobbySelectionError error)
        {
            return error switch
            {
                LobbySelectionError.ToolAlreadyClaimed =>
                    "TOOL ALREADY CLAIMED",

                LobbySelectionError.SelectionLockedWhileReady =>
                    "UNREADY TO CHANGE TOOL",

                LobbySelectionError.InvalidSelection =>
                    "INVALID TOOL",

                LobbySelectionError.NotInputAuthority =>
                    "NO PLAYER AUTHORITY",

                LobbySelectionError.InvalidPlayer =>
                    "PLAYER NOT AVAILABLE",

                _ =>
                    "SELECTION REJECTED"
            };
        }

        private void ShowFeedback(
            string message,
            float duration = 2.5f)
        {
            if (feedbackText == null)
                return;

            feedbackText.text =
                message ?? string.Empty;

            _feedbackClearAt =
                duration > 0f
                    ? Time.unscaledTime + duration
                    : 0f;
        }
    }
}
