using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public enum LobbySelectionKind { Team = 1, Tool = 2 }

    public enum LobbySelectionError
    {
        None,
        InvalidPlayer,
        InvalidSelection,
        ToolAlreadyClaimed,
        SelectionLockedWhileReady,
        NotInputAuthority,
    }

    public readonly struct LobbySelectionResult
    {
        public LobbySelectionResult(LobbySelectionKind kind, int requestedId, bool accepted, LobbySelectionError error)
        {
            Kind = kind;
            RequestedId = requestedId;
            Accepted = accepted;
            Error = error;
        }

        public LobbySelectionKind Kind { get; }
        public int RequestedId { get; }
        public bool Accepted { get; }
        public LobbySelectionError Error { get; }
    }

    [Serializable]
    public sealed class LobbyToolDefinition
    {
        [SerializeField, Min(1)] private int _id;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private bool _isUnique = true;

        public int Id => _id;
        public string DisplayName => _displayName;
        public bool IsUnique => _isUnique;
    }

    /// <summary>
    /// Authoritative per-player lobby selection. The same owned object can later carry team/tool choices.
    /// </summary>
    public sealed class LobbyPlayerState : NetworkBehaviour
    {
        public static event Action AnyStateChanged;
        public static event Action<LobbySelectionResult> LocalSelectionRequestCompleted;

        [SerializeField, Min(1)] private int _teamCount = 2;
        [SerializeField] private LobbyToolDefinition[] _toolDefinitions = Array.Empty<LobbyToolDefinition>();

        [Networked, OnChangedRender(nameof(HandleSelectionChanged))]
        public NetworkBool IsReady { get; private set; }

        [Networked, OnChangedRender(nameof(HandleSelectionChanged))]
        public int TeamId { get; private set; }

        [Networked, OnChangedRender(nameof(HandleSelectionChanged))]
        public int ToolId { get; private set; }

        [Networked]
        public NetworkBool IsGameplayPlayer { get; private set; }

        public int TeamCount => _teamCount;
        public IReadOnlyList<LobbyToolDefinition> ToolDefinitions => _toolDefinitions;

        public void InitializeAuthoritativeSelection(int teamId, int toolId, bool isGameplayPlayer)
        {
            TeamId = teamId;
            ToolId = toolId;
            IsReady = false;
            IsGameplayPlayer = isGameplayPlayer;
        }

        public override void Spawned()
        {
            AnyStateChanged?.Invoke();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            AnyStateChanged?.Invoke();
        }

        public bool RequestReady(bool isReady)
        {
            if (!Object.HasInputAuthority)
            {
                Debug.LogWarning("[LobbyPlayerState] Rejected local ready request: object has no input authority.");
                return false;
            }

            RpcRequestReady(isReady);
            return true;
        }

        public bool RequestTeam(int teamId)
        {
            if (!CanSendSelectionRequest(LobbySelectionKind.Team, teamId)) return false;
            RpcRequestTeam(teamId);
            return true;
        }

        public bool RequestTool(int toolId)
        {
            if (!CanSendSelectionRequest(LobbySelectionKind.Tool, toolId)) return false;
            RpcRequestTool(toolId);
            return true;
        }

        private bool CanSendSelectionRequest(LobbySelectionKind kind, int requestedId)
        {
            if (Object.HasInputAuthority) return true;
            Debug.LogWarning($"[LobbyPlayerState] Cannot request {kind}={requestedId}: no input authority.");
            LocalSelectionRequestCompleted?.Invoke(
                new LobbySelectionResult(kind, requestedId, false, LobbySelectionError.NotInputAuthority));
            return false;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestReady(NetworkBool isReady, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || info.Source != Object.InputAuthority)
            {
                Debug.LogWarning(
                    $"[LobbyPlayerState] Rejected ready request from {info.Source}; owner is {Object.InputAuthority}.");
                return;
            }

            if (!Runner.TryGetPlayerObject(info.Source, out var ownedObject) || ownedObject != Object)
            {
                Debug.LogWarning($"[LobbyPlayerState] Rejected ready request: {info.Source} does not own this player object.");
                return;
            }

            IsReady = isReady;
            Debug.Log($"[LobbyPlayerState] {info.Source} ready={isReady}.");
            AnyStateChanged?.Invoke();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestTeam(int teamId, RpcInfo info = default)
        {
            var error = ValidateOwnedRequest(info.Source);
            if (error == LobbySelectionError.None && IsReady)
            {
                error = LobbySelectionError.SelectionLockedWhileReady;
            }
            if (error == LobbySelectionError.None && (teamId < 0 || teamId > _teamCount))
            {
                error = LobbySelectionError.InvalidSelection;
            }

            if (error == LobbySelectionError.None)
            {
                TeamId = teamId;
                AnyStateChanged?.Invoke();
            }

            SendSelectionResult(info.Source, LobbySelectionKind.Team, teamId, error);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestTool(int toolId, RpcInfo info = default)
        {
            var error = ValidateOwnedRequest(info.Source);
            if (error == LobbySelectionError.None && IsReady)
            {
                error = LobbySelectionError.SelectionLockedWhileReady;
            }

            LobbyToolDefinition definition = null;
            if (error == LobbySelectionError.None && toolId != 0 && !TryGetTool(toolId, out definition))
            {
                error = LobbySelectionError.InvalidSelection;
            }
            if (error == LobbySelectionError.None && definition != null && definition.IsUnique && IsToolClaimedByAnother(toolId))
            {
                error = LobbySelectionError.ToolAlreadyClaimed;
            }

            if (error == LobbySelectionError.None)
            {
                ToolId = toolId;
                AnyStateChanged?.Invoke();
            }

            SendSelectionResult(info.Source, LobbySelectionKind.Tool, toolId, error);
        }

        private LobbySelectionError ValidateOwnedRequest(PlayerRef source)
        {
            if (!Object.HasStateAuthority || source != Object.InputAuthority)
            {
                return LobbySelectionError.InvalidPlayer;
            }
            return Runner.TryGetPlayerObject(source, out var ownedObject) && ownedObject == Object
                ? LobbySelectionError.None
                : LobbySelectionError.InvalidPlayer;
        }

        private bool TryGetTool(int toolId, out LobbyToolDefinition definition)
        {
            foreach (var tool in _toolDefinitions)
            {
                if (tool != null && tool.Id == toolId)
                {
                    definition = tool;
                    return true;
                }
            }
            definition = null;
            return false;
        }

        private bool IsToolClaimedByAnother(int toolId)
        {
            foreach (var player in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(player, out var playerObject) || playerObject == Object) continue;
                if (playerObject.TryGetComponent<LobbyPlayerState>(out var state) && state.ToolId == toolId) return true;
            }
            return false;
        }

        private void SendSelectionResult(
            PlayerRef target,
            LobbySelectionKind kind,
            int requestedId,
            LobbySelectionError error)
        {
            var accepted = error == LobbySelectionError.None;
            Debug.Log($"[LobbyPlayerState] {target} {kind}={requestedId}, accepted={accepted}, error={error}.");
            RpcSelectionResult(target, (int)kind, requestedId, accepted, (int)error);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RpcSelectionResult(
            [RpcTarget] PlayerRef target,
            int kind,
            int requestedId,
            NetworkBool accepted,
            int error)
        {
            LocalSelectionRequestCompleted?.Invoke(new LobbySelectionResult(
                (LobbySelectionKind)kind,
                requestedId,
                accepted,
                (LobbySelectionError)error));
        }

        private void HandleSelectionChanged()
        {
            AnyStateChanged?.Invoke();
        }
    }
}
