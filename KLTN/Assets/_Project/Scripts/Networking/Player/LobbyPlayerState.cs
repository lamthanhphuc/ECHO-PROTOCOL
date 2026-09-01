using System;
using System.Collections.Generic;
using Fusion;
using EchoProtocol.Networking.Authority;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public static class RpcRequesterResolver
    {
        public static bool TryResolveEffectiveRequester(
            PlayerRef rpcSource,
            PlayerRef inputAuthority,
            bool hasStateAuthority,
            bool hasInputAuthority,
            out PlayerRef effectiveRequester)
        {
            if (rpcSource.IsRealPlayer)
            {
                effectiveRequester = rpcSource;
                return true;
            }

            // Fusion Host Mode may execute a locally-owned InputAuthority -> StateAuthority
            // RPC with RpcInfo.Source == PlayerRef.None.
            // Only that exact "None" case is eligible for the Host-local fallback.
            if (rpcSource.IsNone &&
                hasStateAuthority &&
                hasInputAuthority &&
                inputAuthority.IsRealPlayer)
            {
                effectiveRequester = inputAuthority;
                return true;
            }

            effectiveRequester = PlayerRef.None;
            return false;
        }
    }

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

        [Networked, OnChangedRender(nameof(HandleSelectionChanged))]
        public NetworkString<_64> BackendUserId { get; private set; }

        [Networked]
        public NetworkId CarriedCoreId { get; private set; }

        public bool HasVerifiedBackendIdentity => BackendUserId.Length > 0;

        public int TeamCount => _teamCount;
        public IReadOnlyList<LobbyToolDefinition> ToolDefinitions => _toolDefinitions;

        public void InitializeAuthoritativeSelection(int teamId, int toolId, bool isGameplayPlayer)
        {
            TeamId = teamId;
            ToolId = toolId;
            IsReady = false;
            IsGameplayPlayer = isGameplayPlayer;
            if (!isGameplayPlayer) CarriedCoreId = default;
        }

        public bool TryBeginCarryingCore(NetworkId coreId)
        {
            if (!Object.HasStateAuthority || !IsGameplayPlayer || !coreId.IsValid
                || CarriedCoreId.IsValid)
            {
                return false;
            }

            CarriedCoreId = coreId;
            return true;
        }

        public bool TryClearCarriedCore(NetworkId expectedCoreId)
        {
            if (!Object.HasStateAuthority || !CarriedCoreId.IsValid
                || CarriedCoreId != expectedCoreId)
            {
                return false;
            }

            CarriedCoreId = default;
            return true;
        }

        public override void Spawned()
        {
            AnyStateChanged?.Invoke();
            if (Object.HasInputAuthority)
            {
                MatchAuthorityRuntime.EnsureExists(NetworkBootstrap.Instance).TrySubmitLocalIdentity(this);
            }
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

        public void SubmitJoinProof(string proof, int actorNumber)
        {
            if (!Object.HasInputAuthority || string.IsNullOrWhiteSpace(proof)) return;
            RpcSubmitJoinProof(proof, actorNumber);
        }

        public void ApplyVerifiedBackendIdentity(string userId)
        {
            if (!Object.HasStateAuthority || !Guid.TryParse(userId, out _)) return;
            BackendUserId = userId;
            AnyStateChanged?.Invoke();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcSubmitJoinProof(string proof, int actorNumber, RpcInfo info = default)
        {
            if (!TryResolveOwnedRequester(info.Source, out var requester)) return;
            var actualActor = Runner.GetPlayerActorId(requester) ?? requester.PlayerId;
            if (actualActor != actorNumber)
            {
                Debug.LogWarning($"[LobbyPlayerState] Join proof actor mismatch for {requester}.");
                return;
            }

            MatchAuthorityRuntime.EnsureExists(NetworkBootstrap.Instance)
                .BindPlayerFromProof(this, actorNumber, proof);
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
            if (!TryResolveOwnedRequester(info.Source, out var requester))
            {
                Debug.LogWarning(
                    $"[LobbyPlayerState] Rejected ready request from {info.Source}; owner is {Object.InputAuthority}.");
                return;
            }

            if (!HasVerifiedBackendIdentity)
            {
                Debug.LogWarning($"[LobbyPlayerState] Rejected ready request from unbound player {requester}.");
                return;
            }

            if (!Runner.TryGetPlayerObject(requester, out var ownedObject) || ownedObject != Object)
            {
                Debug.LogWarning($"[LobbyPlayerState] Rejected ready request: {requester} does not own this player object.");
                return;
            }

            IsReady = isReady;
            Debug.Log($"[LobbyPlayerState] {requester} ready={isReady}.");
            AnyStateChanged?.Invoke();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestTeam(int teamId, RpcInfo info = default)
        {
            if (!TryResolveRequester(info.Source, out var requester))
            {
                Debug.LogWarning(
                    $"[LobbyPlayerState] Rejected team request from {info.Source}; owner is {Object.InputAuthority}.");
                return;
            }

            var error = ValidateOwnedRequest(requester);
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

            SendSelectionResult(requester, LobbySelectionKind.Team, teamId, error);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestTool(int toolId, RpcInfo info = default)
        {
            if (!TryResolveRequester(info.Source, out var requester))
            {
                Debug.LogWarning(
                    $"[LobbyPlayerState] Rejected tool request from {info.Source}; owner is {Object.InputAuthority}.");
                return;
            }

            var error = ValidateOwnedRequest(requester);
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

            SendSelectionResult(requester, LobbySelectionKind.Tool, toolId, error);
        }

        private bool TryResolveRequester(PlayerRef source, out PlayerRef requester)
        {
            return RpcRequesterResolver.TryResolveEffectiveRequester(
                source,
                Object.InputAuthority,
                Object.HasStateAuthority,
                Object.HasInputAuthority,
                out requester);
        }

        private bool TryResolveOwnedRequester(PlayerRef source, out PlayerRef requester)
        {
            if (!RpcRequesterResolver.TryResolveEffectiveRequester(
                    source,
                    Object.InputAuthority,
                    Object.HasStateAuthority,
                    Object.HasInputAuthority,
                    out requester))
            {
                return false;
            }

            return Object.HasStateAuthority && requester == Object.InputAuthority;
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
