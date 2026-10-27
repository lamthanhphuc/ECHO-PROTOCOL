using System;
using EchoProtocol.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Auth;
using EchoProtocol.Gameplay;
using Fusion;
using EchoProtocol.Telemetry;
using EchoProtocol.Telemetry.Unity;
using UnityEngine;

namespace EchoProtocol.Networking.Authority
{
    public enum ProductionTelemetryPublishResult
    {
        RetryableFailure,
        InvalidOccurrence,
        Accepted,
        Suppressed
    }

    /// <summary>
    /// Bridges the authenticated backend identity to Fusion's Host authority.
    /// Proofs stay off replicated state; only the verified backend user id is replicated.
    /// </summary>
    public sealed partial class MatchAuthorityRuntime : MonoBehaviour,
        IUnityTelemetryAuthorityProvider,
        IUnityTelemetryProvenanceProvider
    {
        public const string MatchIdSessionProperty = "matchId";
        public const string ScenarioResolutionModeSessionProperty = "scenarioResolutionMode";
        public const string MatchDifficultySessionProperty = "difficulty";
        private const float LeaseRenewIntervalSeconds = 15f;
        private const float LocalRewardRetryIntervalSeconds = 1f;
        private const int LocalRewardMaxAttempts = 8;
        private const int PlayerBindRetryAttempts = 8;
        private const int PlayerBindRetryDelayMs = 250;
        private const string HostTakeoverNotReadyErrorCode =
            "MATCH_AUTHORITY_TAKEOVER_NOT_READY";
        private const int HostTakeoverRetryAttempts = 60;
        private const int HostTakeoverRetryDelayMs = 1000;
        private const string PlayerBindingConflictErrorCode =
            "MATCH_PLAYER_BINDING_CONFLICT";

        private static MatchAuthorityRuntime _instance;
        private MatchAuthorityApiService _api;
        private NetworkBootstrap _bootstrap;
        private float _nextLeaseRenewal;
        private bool _leaseRequestInProgress;
        private bool _identityRequestInProgress;
        private int _bindingGeneration;

        // Only one backend bind may run for the same player in the
        // same match. Multiple local identity triggers may otherwise
        // submit proofs concurrently before BackendUserId replicates.
        private readonly HashSet<(Guid MatchId, int ActorNumber)>
            _playerBindsInProgress = new();
        private bool _backendEndRequestInProgress;
        private bool _pendingBackendMatchResult;
        private float _nextBackendMatchResultRetryAt;
        private const float MinimumBackendMatchResultAgeSeconds = 61f;

        private float _backendMatchStartedAtRealtime = -1f;
        private bool _localRewardRequestInProgress;
        private bool _localRewardFetchQueued;
        private int _localRewardFetchAttempts;
        private float _nextLocalRewardFetchAt;
        private Guid _localRewardMatchId;
        private string _pendingBackendOutcome;
        private float _pendingObjectiveCompletion;
        private readonly Dictionary<int, Guid> _boundPlayers = new();
        private readonly HashSet<int> _disconnectedActors = new();
        private readonly HashSet<int> _objectiveContributors = new();
        private bool _eventsSubscribed;
        private TelemetryRuntimeBehaviour _telemetry;
        private DateTime _matchStartedAtUtc;
        private bool _telemetryMatchActive;
        private bool _matchEndEmitted;
        private bool _pendingMatchEnd;
        private string _pendingMatchEndOccurrenceKey;
        private string _pendingMatchEndOutcome;
        private int _pendingMatchEndSurvivorCount;
        private string _pendingMatchEndReasonCode;
        private float _nextMatchEndRetryAt;
        private bool _matchEndRetryWarningLogged;
        private bool _pendingAuthoritativeTelemetryMatchStart;
        private string _currentTelemetryPhase = "CORE_COLLECTION";
        private HostRuntimeNoiseService _runtimeNoise;
        private bool _runtimeNoiseTelemetryCapacityWarningLogged;
        private bool _runtimeNoiseTelemetryInactiveWarningLogged;
        [SerializeField] private bool _researchCaptureEnabled;
        [SerializeField] private ScenarioResolutionMode requestedScenarioResolutionMode = ScenarioResolutionMode.Fixed;
        [SerializeField] private MatchDifficulty requestedDifficulty = MatchDifficulty.Normal;
        [SerializeField] private string experimentCondition;
        [SerializeField]
        private string experimentProtocolVersion;
        [SerializeField]
        private string telemetryMapContentVersion =
            "M2-MAP-1";
        public static MatchAuthorityRuntime Instance => _instance;
        public Guid MatchId { get; private set; }
        public RewardMeResponseDto LastLocalReward { get; private set; }
        public bool HasLocalReward => LastLocalReward != null;
        public event Action<RewardMeResponseDto> LocalRewardUpdated;
        public ScenarioResolutionMode RequestedScenarioResolutionMode => requestedScenarioResolutionMode;
        public MatchDifficulty Difficulty => requestedDifficulty;
        public string ExperimentCondition => experimentCondition ?? string.Empty;
        public string ExperimentProtocolVersion =>
            experimentProtocolVersion ?? string.Empty;
        public bool IsHostBinding { get; private set; }
        public bool HasBinding => MatchId != Guid.Empty;
        public int BoundPlayerCount =>
            _boundPlayers.Count;

        public bool IsPlayerBound(
            int actorNumber,
            string backendUserId)
        {
            return actorNumber > 0
                && Guid.TryParse(
                    backendUserId,
                    out var userId)
                && _boundPlayers.TryGetValue(
                    actorNumber,
                    out var boundUserId)
                && boundUserId == userId;
        }

        public bool RebuildMigratedPlayerBindings(
            NetworkRunner runner)
        {
            if (!IsHostBinding
                || !HasBinding
                || runner == null
                || !runner.IsRunning
                || !runner.IsServer)
            {
                return false;
            }

            var rebuilt =
                new Dictionary<int, Guid>();

            var pendingIdentityCount = 0;
            var localBindingRebuilt = false;

            bool hasLocalBackendUser =
                Guid.TryParse(
                    AuthSession.CurrentUserId,
                    out var localBackendUserId);

            foreach (var player in
                     runner.ActivePlayers)
            {
                var actorNumber =
                    runner.GetPlayerActorId(player)
                    ?? (player == runner.LocalPlayer
                        ? player.PlayerId
                        : 0);

                // Backend takeover has already authenticated the promoted
                // Host. Its replicated LobbyPlayerState can still be one
                // frame behind while Fusion finishes adding the local
                // player, so use the authenticated local identity here.
                if (player == runner.LocalPlayer
                    && actorNumber > 0
                    && hasLocalBackendUser)
                {
                    rebuilt[actorNumber] =
                        localBackendUserId;

                    localBindingRebuilt = true;

                    continue;
                }

                if (!runner.TryGetPlayerObject(
                        player,
                        out var playerObject)
                    || playerObject == null
                    || !playerObject.IsValid
                    || !playerObject.TryGetComponent<LobbyPlayerState>(
                        out var state)
                    || !state.HasVerifiedBackendIdentity
                    || !Guid.TryParse(
                        state.BackendUserId.ToString(),
                        out var userId))
                {
                    // Remote clients issue a fresh proof after migration.
                    // Do not tear down the restored session merely because
                    // their Networked identity has not arrived in this frame.
                    pendingIdentityCount++;
                    continue;
                }

                if (actorNumber < 1
                    || rebuilt.ContainsKey(actorNumber)
                    || rebuilt.ContainsValue(userId))
                {
                    pendingIdentityCount++;
                    continue;
                }

                rebuilt[actorNumber] =
                    userId;
            }

            if (!localBindingRebuilt)
            {
                return false;
            }

            _boundPlayers.Clear();

            foreach (var binding in
                     rebuilt)
            {
                _boundPlayers[binding.Key] =
                    binding.Value;

                _disconnectedActors.Remove(
                    binding.Key);
            }

            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,
                $"MA|MIGRATION_BINDINGS_REBUILT|players={rebuilt.Count}|pending={pendingIdentityCount}");

            return true;
        }

        public bool RequiresFreshHostBinding =>
            !HasBinding
            || !IsHostBinding
            || HasQueuedResult(MatchId);
        public bool IsCompletingMatch =>
            _backendEndRequestInProgress
            || _pendingBackendMatchResult;
        public bool HasStateAuthority => IsHostBinding && _bootstrap?.Runner != null
            && _bootstrap.Runner.IsRunning && _bootstrap.Runner.IsServer;
        public long? AuthorityTick => HasStateAuthority ? _bootstrap.Runner.Tick.Raw : (long?)null;

        public static MatchAuthorityRuntime EnsureExists(NetworkBootstrap bootstrap = null)
        {
            if (_instance == null)
            {
                var existing = FindAnyObjectByType<MatchAuthorityRuntime>();
                if (existing != null) _instance = existing;
            }

            if (_instance == null)
            {
                _instance = new GameObject("MatchAuthorityRuntime").AddComponent<MatchAuthorityRuntime>();
            }

            _instance.Initialize(bootstrap);
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            LoadResultOutbox();
            DontDestroyOnLoad(gameObject);
        }

        private void Initialize(NetworkBootstrap bootstrap)
        {
            if (bootstrap != null) _bootstrap = bootstrap;
            if (_api == null)
            {
                var auth = AuthRuntime.EnsureExists();
                _api = new MatchAuthorityApiService(auth.Client);
            }
            if (!_eventsSubscribed && _bootstrap != null)
            {
                _bootstrap.SessionStateChanged += HandleSessionStateChanged;
                NetworkPickupItem.AuthoritativeStateCommitted += HandlePickupStateCommitted;
                _eventsSubscribed = true;
            }
            _telemetry ??= TelemetryRuntimeBehaviour.EnsureExists();
            _telemetry.BindProviders(this, this);
            _runtimeNoise ??= HostRuntimeNoiseService.EnsureExists(this);
        }

        private void OnDestroy()
        {
            if (_eventsSubscribed && _bootstrap != null)
            {
                _bootstrap.SessionStateChanged -= HandleSessionStateChanged;
                NetworkPickupItem.AuthoritativeStateCommitted -= HandlePickupStateCommitted;
            }
            if (_instance == this) _instance = null;
        }

        private async void Update()
        {
            TickResultOutbox();
            var matchState = NetworkMatchState.Instance;
            if (HasBinding
                && matchState != null
                && matchState.IsEnded
                && _localRewardMatchId != MatchId)
            {
                QueueLocalRewardFetch(MatchId);
            }

            if (_localRewardFetchQueued
                && !_localRewardRequestInProgress
                && Time.unscaledTime >= _nextLocalRewardFetchAt)
            {
                TryFetchLocalReward();
            }

            if (_pendingMatchEnd
                && Time.unscaledTime >= _nextMatchEndRetryAt)
            {
                TryEmitPendingMatchEnd();
            }

            if (_pendingBackendMatchResult
                && Time.unscaledTime >= _nextBackendMatchResultRetryAt)
            {
                TrySubmitPendingMatchResult();
            }

            if (!IsHostBinding || !HasBinding || _leaseRequestInProgress
                || Time.unscaledTime < _nextLeaseRenewal)
            {
                return;
            }

            _leaseRequestInProgress = true;
            _nextLeaseRenewal = Time.unscaledTime + LeaseRenewIntervalSeconds;
            var result = await _api.RenewLeaseAsync(MatchId);
            _leaseRequestInProgress = false;
            if (!IsSuccessful(result))
            {
                Debug.LogError($"[MatchAuthority] Lease renewal failed: {Describe(result)}");
            }
        }

        public async Task<bool> PrepareHostAsync(string sessionName, int maxPlayers)
        {
            if (!AuthSession.IsAuthenticated || !Guid.TryParse(AuthSession.CurrentUserId, out _))
            {
                Debug.LogError("[MatchAuthority] An authenticated backend user is required to host.");
                return false;
            }

            var result = await _api.CreateAsync(sessionName, maxPlayers);
            if (!IsSuccessful(result)
                || !Guid.TryParse(result.Data.data.matchId, out var matchId))
            {
                Debug.LogError($"[MatchAuthority] Create failed: {Describe(result)}");
                return false;
            }

            ResetLocalRewardTracking();
            MatchId = matchId;
            IsHostBinding = true;
            _nextLeaseRenewal = Time.unscaledTime + LeaseRenewIntervalSeconds;
            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,
                $"[MatchAuthority] Host binding created. Match={MatchId:D}, Session='{sessionName}'.");
            return true;
        }

        public async Task<bool> TakeoverHostAuthorityAsync(
            NetworkRunner runner)
        {
            if (!AuthSession.IsAuthenticated
                || !Guid.TryParse(
                    AuthSession.CurrentUserId,
                    out var localUserId))
            {
                Debug.LogError("[MatchAuthority] An authenticated backend user is required for Host takeover.");
                return false;
            }

            if (runner == null
                || !runner.IsRunning
                || !runner.IsServer
                || !runner.SessionInfo.IsValid)
            {
                Debug.LogError("[MatchAuthority] Host takeover requires a running authoritative Fusion runner.");
                return false;
            }

            if (runner.SessionInfo.Properties == null
                || !runner.SessionInfo.Properties.TryGetValue(
                    MatchIdSessionProperty,
                    out var property)
                || !Guid.TryParse(
                    (string)property,
                    out var sessionMatchId))
            {
                Debug.LogError("[MatchAuthority] Migrated Fusion session has no valid backend match binding.");
                return false;
            }

            if (HasBinding && MatchId != sessionMatchId)
            {
                Debug.LogError($"[MatchAuthority] Host takeover rejected: local match {MatchId:D} does not match session {sessionMatchId:D}.");
                return false;
            }

            var sessionName = runner.SessionInfo.Name;
            var actorNumber =
                runner.GetPlayerActorId(runner.LocalPlayer)
                ?? runner.LocalPlayer.PlayerId;

            if (actorNumber < 1)
            {
                Debug.LogError("[MatchAuthority] Host takeover could not resolve the local Fusion actor number.");
                return false;
            }

            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,
                $"MA|TAKEOVER_REQUEST|match={sessionMatchId:D}|actor={actorNumber}|session={sessionName}");

            MatchId = sessionMatchId;
            IsHostBinding = false;
            requestedScenarioResolutionMode =
                ReadScenarioResolutionMode(runner);
            requestedDifficulty =
                ReadMatchDifficulty(runner);

            for (var attempt = 1;
                 attempt <= HostTakeoverRetryAttempts;
                 attempt++)
            {
                if (runner == null
                    || !runner.IsRunning
                    || !runner.IsServer
                    || !runner.SessionInfo.IsValid)
                {
                    Debug.LogWarning("[MatchAuthority] Host takeover retry stopped because this Fusion runner is no longer authoritative.");
                    return false;
                }

                var result =
                    await _api.TakeoverAsync(
                        sessionMatchId,
                        sessionName,
                        actorNumber);

                if (IsSuccessful(result))
                {
                    break;
                }

                var takeoverErrorCode =
                    result != null
                    && !string.IsNullOrWhiteSpace(result.ErrorCode)
                        ? result.ErrorCode
                        : result?.Data?.errorCode;

                bool retryable =
                    string.Equals(
                        takeoverErrorCode,
                        HostTakeoverNotReadyErrorCode,
                        StringComparison.Ordinal);

                if (!retryable)
                {
                    Debug.LogError($"[MatchAuthority] Host takeover failed: {Describe(result)}");
                    return false;
                }

                if (attempt >= HostTakeoverRetryAttempts)
                {
                    Debug.LogError($"[MatchAuthority] Host takeover timed out waiting for the previous Host lease: {Describe(result)}");
                    return false;
                }

                if (attempt == 1
                    || attempt % 5 == 0)
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.MatchAuthority,
                        $"[MatchAuthority] Waiting for previous Host lease to expire. Attempt={attempt}/{HostTakeoverRetryAttempts}, Match={sessionMatchId:D}.");
                }

                await Task.Delay(
                    HostTakeoverRetryDelayMs);
            }

            // The HTTP request may finish after this runner has already
            // been replaced or disconnected. The backend operation is
            // idempotent, so a later migration retry may reconcile it.
            if (runner == null
                || !runner.IsRunning
                || !runner.IsServer
                || !runner.SessionInfo.IsValid)
            {
                Debug.LogWarning("[MatchAuthority] Backend Host takeover succeeded, but the Fusion runner is no longer authoritative.");
                return false;
            }

            IsHostBinding = true;
            _nextLeaseRenewal =
                Time.unscaledTime + LeaseRenewIntervalSeconds;

            _boundPlayers[actorNumber] =
                localUserId;
            _disconnectedActors.Remove(actorNumber);

            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,
                $"[MatchAuthority] Host takeover confirmed. Match={MatchId:D}, Actor={actorNumber}, User={localUserId:D}, Session='{sessionName}'.");

            return true;
        }

        public async Task<bool> PrepareNextMatchAsync(
            string sessionName,
            int maxPlayers)
        {
            if (_backendEndRequestInProgress)
            {
                return false;
            }

            ResetBinding();

            return await PrepareHostAsync(
                sessionName,
                maxPlayers);
        }

        public Dictionary<string, SessionProperty> BuildHostSessionProperties() =>
            new Dictionary<string, SessionProperty>
            {
                [MatchIdSessionProperty] = MatchId.ToString("D"),
                [ScenarioResolutionModeSessionProperty] =
                    requestedScenarioResolutionMode == ScenarioResolutionMode.Adaptive
                        ? "ADAPTIVE"
                        : "FIXED",
                [MatchDifficultySessionProperty] = requestedDifficulty.ToString().ToUpperInvariant(),
                ["playerCollisions"] = EchoProtocol.UI.LobbySelectionMemory.Read("playerCollisions", 1),
                ["proximityVoice"] = EchoProtocol.UI.LobbySelectionMemory.Read("proximityVoice", 1),
                ["medkits"] = EchoProtocol.UI.LobbySelectionMemory.Read("medkits", 1),
                [LobbyMapCatalog.PropertyKey] = Mathf.Clamp(EchoProtocol.UI.LobbySelectionMemory.Read("map", 0), 0, 1)
            };

        public bool AttachJoinedSession(NetworkRunner runner)
        {
            if (runner == null || !runner.SessionInfo.IsValid)
            {
                Debug.LogError("[MatchAuthority] Fusion session is invalid.");
                return false;
            }

            Guid matchId = Guid.Empty;
            if (runner.SessionInfo.Properties != null
                && runner.SessionInfo.Properties.TryGetValue(MatchIdSessionProperty, out var property)
                && Guid.TryParse((string)property, out var parsedId))
            {
                matchId = parsedId;
            }
            else
            {
                Debug.LogError("[MatchAuthority] Session has no backend match binding.");
                return false;
            }

            ResetLocalRewardTracking();
            MatchId = matchId;
            IsHostBinding = runner.IsServer;
            requestedScenarioResolutionMode = ReadScenarioResolutionMode(runner);
            requestedDifficulty = ReadMatchDifficulty(runner);
            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,

                $"[MatchAuthority] Fusion session attached. Match={MatchId:D}, " +
                $"Host={IsHostBinding}, Mode={requestedScenarioResolutionMode}, Session='{runner.SessionInfo.Name}'.");
            TrySubmitLocalIdentity();
            return true;
        }

        public bool RefreshJoinedSessionBindingIfChanged(
            NetworkRunner runner,
            LobbyPlayerState localPlayerState)
        {
            if (runner == null
                || !runner.IsRunning
                || !runner.SessionInfo.IsValid
                || runner.SessionInfo.Properties == null
                || !runner.SessionInfo.Properties.TryGetValue(
                    MatchIdSessionProperty,
                    out var property)
                || !Guid.TryParse(
                    (string)property,
                    out var sessionMatchId))
            {
                return false;
            }

            if (sessionMatchId == MatchId)
            {
                return false;
            }

            if (!AttachJoinedSession(runner))
            {
                return false;
            }

            TrySubmitLocalIdentity(
                localPlayerState,
                force: true);

            return true;
        }

        public void RequestScenarioResolutionMode(
            ScenarioResolutionMode mode,
            string condition = null,
            string protocolVersion = null)
        {
            requestedScenarioResolutionMode =
                mode;

            experimentCondition =
                condition ?? string.Empty;

            experimentProtocolVersion =
                protocolVersion ?? string.Empty;
        }

        public void RequestDifficulty(MatchDifficulty difficulty)
        {
            requestedDifficulty = Enum.IsDefined(typeof(MatchDifficulty), difficulty)
                ? difficulty : MatchDifficulty.Normal;
        }

        public async Task<bool> RelinquishHostAuthorityAsync()
        {
            if (!HasBinding)
            {
                ResetBinding();
                return true;
            }

            if (!IsHostBinding)
            {
                ResetBinding();
                return true;
            }

            var matchId = MatchId;

            // Stop scheduling additional renewals before relinquishing.
            IsHostBinding = false;

            // Do not race a lease renewal already in flight. If that request
            // completed after relinquish it could otherwise extend the old
            // Host lease again and delay takeover.
            while (_leaseRequestInProgress)
            {
                await Task.Yield();
            }

            var result =
                await _api.RelinquishAsync(matchId);

            var success =
                IsSuccessful(result);

            if (!success)
            {
                Debug.LogWarning(
                    $"[MatchAuthority] Host relinquish failed: {Describe(result)}. " +
                    "Lease renewal remains stopped so authority can still expire naturally.");
            }
            else
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.MatchAuthority,
                    $"[MatchAuthority] Host authority relinquished. Match={matchId:D}.");
            }

            ResetBinding();
            return success;
        }

        public async Task<bool> EndAsync(string reason)
        {
            if (HasQueuedResult(MatchId))
            {
                // Sending /end first would abort the backend match before its result is saved.
                ResetBinding();
                return true;
            }
            if (!HasBinding || !IsHostBinding)
            {
                ResetBinding();
                return true;
            }

            EmitAbortedMatchEndIfActive();
            var result = await _api.EndAsync(MatchId, reason);
            var success = IsSuccessful(result);
            if (!success) Debug.LogWarning($"[MatchAuthority] End failed: {Describe(result)}");
            ResetBinding();
            return success;
        }

        public async void TrySubmitLocalIdentity(
            LobbyPlayerState knownState = null,
            bool force = false)
        {
            if (!HasBinding
                || _identityRequestInProgress
                || _bootstrap?.Runner == null
                || string.IsNullOrWhiteSpace(
                    _bootstrap.CurrentSessionName))
            {
                return;
            }

            var runner =
                _bootstrap.Runner;

            var state =
                knownState;

            if (state == null
                && runner.TryGetPlayerObject(
                    runner.LocalPlayer,
                    out var playerObject))
            {
                playerObject.TryGetComponent(
                    out state);
            }

            if (state == null
                || !state.Object.HasInputAuthority
                || (state.HasVerifiedBackendIdentity
                    && !force))
            {
                return;
            }

            var directActor =
                runner.GetPlayerActorId(
                    runner.LocalPlayer);

            var actorNumber = 0;

            if (directActor.HasValue
                && directActor.Value > 0)
            {
                actorNumber =
                    directActor.Value;
            }
            else if (runner.IsServer
                && runner.LocalPlayer.IsRealPlayer
                && runner.LocalPlayer.PlayerId > 0)
            {
                // Fusion Host local PlayerRef can have no transport
                // ActorNumber mapping. PlayerId is used only for this
                // authoritative Host-local case.
                actorNumber =
                    runner.LocalPlayer.PlayerId;
            }
            else if (state.FusionActorNumber > 0)
            {
                // Regular clients use the actor replicated by
                // State Authority.
                actorNumber =
                    state.FusionActorNumber;
            }

            if (actorNumber < 1)
            {
                return;
            }

            // A retained BackendUserId is NOT proof of a connected backend
            // roster row after a deliberate Leave / rejoin. Only a fresh
            // backend bind acknowledgment can complete this identity cycle.
            if (force && state.HasVerifiedBackendIdentity)
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.MatchAuthority,
                    $"MA|REJOIN_PROOF_REQUEST|actor={actorNumber}|match={MatchId:D}");
            }

            _identityRequestInProgress =
                true;

            var bindingGeneration =
                _bindingGeneration;

            var bindingMatchId =
                MatchId;

            var bindingSessionName =
                _bootstrap.CurrentSessionName;

            EchoProtocol.Api.ApiResult<EchoProtocol.Api.ApiResponse<JoinProofDto>> result;

            try
            {
                result =
                    await _api.IssueJoinProofAsync(
                        bindingMatchId,
                        bindingSessionName,
                        actorNumber);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MatchAuthority] Join proof request failed: {exception.Message}");

                return;
            }
            finally
            {
                if (_bindingGeneration == bindingGeneration)
                {
                    _identityRequestInProgress =
                        false;
                }
            }

            if (_bindingGeneration != bindingGeneration
                || MatchId != bindingMatchId
                || _bootstrap?.Runner != runner
                || !runner.IsRunning
                || state == null
                || state.Object == null
                || !state.Object.IsValid
                || !state.Object.HasInputAuthority)
            {
                return;
            }

            if (!IsSuccessful(result))
            {
                Debug.LogWarning(
                    $"[MatchAuthority] Join proof failed: {Describe(result)}.");

                return;
            }

            state.SubmitJoinProof(
                result.Data.data.proof,
                actorNumber);

            if (force && state.HasVerifiedBackendIdentity)
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.MatchAuthority,
                    $"MA|REJOIN_PROOF_SENT|actor={actorNumber}|match={bindingMatchId:D}");
            }
        }

        public async Task<bool> VerifyPlayerEntitlementsAsync(
            LobbyPlayerState playerState, string[] requiredItemIds)
        {
            if (!HasStateAuthority || !HasBinding || _api == null
                || playerState == null || playerState.Object == null
                || !playerState.Object.IsValid || !playerState.Object.HasStateAuthority
                || requiredItemIds == null)
                return false;
            if (requiredItemIds.Length == 0) return true;

            var matchId = MatchId;
            var player = playerState.Object.InputAuthority;
            var actorNumber = _bootstrap.Runner.GetPlayerActorId(player) ?? player.PlayerId;
            if (!_boundPlayers.TryGetValue(actorNumber, out var userId)
                || !Guid.TryParse(playerState.BackendUserId.ToString(), out var replicatedUserId)
                || replicatedUserId != userId)
                return false;

            try
            {
                var response = await _api.GetPlayerEntitlementsAsync(matchId, actorNumber);
                if (!IsSuccessful(response) || MatchId != matchId || !HasStateAuthority
                    || playerState == null || playerState.Object == null
                    || !playerState.Object.IsValid || playerState.Object.InputAuthority != player
                    || !_boundPlayers.TryGetValue(actorNumber, out var currentUserId)
                    || currentUserId != userId
                    || !Guid.TryParse(response.Data.data.userId, out var verifiedUserId)
                    || verifiedUserId != userId)
                    return false;

                var owned = new HashSet<string>(
                    response.Data.data.shopItemIds ?? Array.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);
                foreach (var itemId in requiredItemIds)
                    if (!Guid.TryParse(itemId, out _) || !owned.Contains(itemId))
                        return false;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MatchAuthority] Entitlement check failed: {exception.Message}");
                return false;
            }
        }

        public async void BindPlayerFromProof(
            LobbyPlayerState playerState,
            int actorNumber,
            string proof)
        {
            if (!IsHostBinding
                || !HasBinding
                || actorNumber < 1
                || playerState == null
                || playerState.Object == null
                || !playerState.Object.IsValid
                || !playerState.Object.HasStateAuthority)
            {
                return;
            }

            Guid bindingMatchId =
                MatchId;

            PlayerRef inputAuthority =
                playerState.Object.InputAuthority;

            var bindKey =
                (bindingMatchId, actorNumber);

            // Identity can be triggered from Spawned, actor replication
            // and the bounded retry path. While one backend bind is still
            // in flight, later proofs for the exact same match/actor are
            // redundant and must not create concurrent DB inserts.
            if (!_playerBindsInProgress.Add(bindKey))
            {
                return;
            }

            try
            {
                for (int attempt = 1;
                     attempt <= PlayerBindRetryAttempts;
                     attempt++)
                {
                    if (!IsHostBinding
                        || !HasBinding
                        || MatchId != bindingMatchId
                        || playerState == null
                        || playerState.Object == null
                        || !playerState.Object.IsValid
                        || !playerState.Object.HasStateAuthority
                        || playerState.Object.InputAuthority
                            != inputAuthority)
                    {
                        return;
                    }

                    var result =
                        await _api.BindPlayerAsync(
                            bindingMatchId,
                            actorNumber,
                            proof);

                    if (IsSuccessful(result))
                    {
                        if (playerState == null
                            || playerState.Object == null
                            || !playerState.Object.IsValid
                            || playerState.Object.InputAuthority
                                != inputAuthority)
                        {
                            return;
                        }

                        playerState.ApplyVerifiedBackendIdentity(
                            result.Data.data.userId);

                        if (Guid.TryParse(
                                result.Data.data.userId,
                                out var boundUserId))
                        {
                            _boundPlayers[actorNumber] =
                                boundUserId;

                            _disconnectedActors.Remove(
                                actorNumber);
                        }

                        RuntimeLog.Log(
                            RuntimeLogCategory.MatchAuthority,
                            $"[MatchAuthority] Player verified. " +
                            $"Actor={actorNumber}, " +
                            $"User={result.Data.data.userId}, " +
                            $"Match={bindingMatchId:D}.");
                        RuntimeLog.Log(
                            RuntimeLogCategory.MatchAuthority,
                            $"MA|ROSTER_BIND_ACK|actor={actorNumber}|match={bindingMatchId:D}");

                        return;
                    }

                    // A different identity attempt may already have
                    // completed successfully while this request was in
                    // flight. Never disconnect a player that is now verified.
                    if (playerState != null
                        && playerState.Object != null
                        && playerState.Object.IsValid
                        && playerState.HasVerifiedBackendIdentity)
                    {
                        return;
                    }

                    bool retryable =
                        result != null
                        && string.Equals(
                            result.ErrorCode,
                            PlayerBindingConflictErrorCode,
                            StringComparison.Ordinal);

                    if (!retryable
                        || attempt >= PlayerBindRetryAttempts)
                    {
                        Debug.LogWarning(
                            $"[MatchAuthority] Player bind rejected: " +
                            $"{Describe(result)}.");

                        if (_bootstrap?.Runner != null
                            && _bootstrap.Runner.IsRunning
                            && playerState != null
                            && playerState.Object != null
                            && playerState.Object.IsValid
                            && !playerState.HasVerifiedBackendIdentity
                            && playerState.Object.InputAuthority
                                == inputAuthority)
                        {
                            _bootstrap.Runner.Disconnect(
                                inputAuthority);
                        }

                        return;
                    }

                    await Task.Delay(
                        PlayerBindRetryDelayMs);
                }
            }
            finally
            {
                _playerBindsInProgress.Remove(
                    bindKey);
            }
        }

        public async Task<bool> ReconcileMigratedDisconnectedActorsAsync(
            IEnumerable<KeyValuePair<int, Guid>> actorBindings)
        {
            if (!IsHostBinding
                || !HasBinding
                || actorBindings == null)
            {
                return false;
            }

            // Pin cleanup to the exact backend match and authoritative Runner.
            var reconciliationGeneration = _bindingGeneration;
            var reconciliationMatchId = MatchId;
            var reconciliationRunner = _bootstrap?.Runner;

            if (reconciliationRunner == null
                || !reconciliationRunner.IsRunning
                || !reconciliationRunner.IsServer)
            {
                return false;
            }

            var actors =
                new Dictionary<int, Guid>();

            foreach (var binding in actorBindings)
            {
                if (binding.Key < 1 || binding.Value == Guid.Empty)
                {
                    continue;
                }

                if (actors.TryGetValue(binding.Key, out var priorUserId)
                    && priorUserId != binding.Value)
                {
                    Debug.LogWarning(
                        $"[MatchAuthority] Conflicting retained actor identity. Actor={binding.Key}.");
                    return false;
                }

                actors[binding.Key] = binding.Value;
            }

            var reconciled = 0;
            var allReconciled = true;

            foreach (var expectedBinding in actors)
            {
                var actorNumber = expectedBinding.Key;
                var expectedUserId = expectedBinding.Value;

                if (_bindingGeneration != reconciliationGeneration
                    || MatchId != reconciliationMatchId
                    || !IsHostBinding
                    || _bootstrap?.Runner != reconciliationRunner
                    || !reconciliationRunner.IsRunning
                    || !reconciliationRunner.IsServer)
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.MatchAuthority,
                        $"MA|MIGRATION_ROSTER_RECONCILE_ABORT_STALE|match={reconciliationMatchId:D}");
                    return false;
                }
                // Fusion may reuse the departed Host's transport actor for
                // an active player during migration. The active roster was
                // rebuilt before this cleanup, so never disconnect a number
                // that is currently owned by a verified survivor.
                if (_boundPlayers.ContainsKey(
                        actorNumber)
                    || IsActiveFusionActor(
                        actorNumber))
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.MatchAuthority,
                        $"MA|MIGRATION_ROSTER_RECONCILE_SKIP_ACTIVE|actor={actorNumber}");

                    continue;
                }

                var actorReconciled = false;

                for (var attempt = 1;
                     attempt <= 3;
                     attempt++)
                {
                    if (_bindingGeneration != reconciliationGeneration
                        || MatchId != reconciliationMatchId
                        || !IsHostBinding
                        || !HasBinding
                        || _bootstrap?.Runner != reconciliationRunner
                        || !reconciliationRunner.IsRunning
                        || !reconciliationRunner.IsServer)
                    {
                        RuntimeLog.Log(
                            RuntimeLogCategory.MatchAuthority,
                            $"MA|MIGRATION_ROSTER_RECONCILE_ABORT_STALE|match={reconciliationMatchId:D}");
                        return false;
                    }

                    var result =
                        await _api.DisconnectPlayerAsync(
                            reconciliationMatchId,
                            actorNumber,
                            expectedUserId);

                    if (_bindingGeneration != reconciliationGeneration
                        || MatchId != reconciliationMatchId
                        || !IsHostBinding
                        || _bootstrap?.Runner != reconciliationRunner
                        || !reconciliationRunner.IsRunning
                        || !reconciliationRunner.IsServer)
                    {
                        RuntimeLog.Log(
                            RuntimeLogCategory.MatchAuthority,
                            $"MA|MIGRATION_ROSTER_RECONCILE_ABORT_STALE|match={reconciliationMatchId:D}");
                        return false;
                    }

                    if (IsSuccessful(result))
                    {
                        actorReconciled = true;
                        break;
                    }

                    var errorCode =
                        !string.IsNullOrWhiteSpace(
                            result?.ErrorCode)
                            ? result.ErrorCode
                            : result?.Data?.errorCode;

                    // Takeover already disconnects the previous Host.
                    // Repeating that disconnect is therefore idempotent.
                    if (string.Equals(
                            errorCode,
                            "NOT_FOUND",
                            StringComparison.Ordinal))
                    {
                        actorReconciled = true;
                        break;
                    }

                    if (attempt < 3)
                    {
                        await Task.Delay(250);
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[MatchAuthority] Migration roster disconnect failed. Actor={actorNumber}, Error={Describe(result)}");
                    }
                }

                if (actorReconciled)
                {
                    _disconnectedActors.Add(
                        actorNumber);

                    reconciled++;
                }
                else
                {
                    allReconciled = false;
                }
            }

            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,
                $"MA|MIGRATION_ROSTER_RECONCILE|requested={actors.Count}|reconciled={reconciled}|success={allReconciled}");

            return allReconciled;
        }

        private bool IsActiveFusionActor(
            int actorNumber)
        {
            var runner =
                _bootstrap?.Runner;

            if (runner == null
                || !runner.IsRunning
                || actorNumber < 1)
            {
                return false;
            }

            foreach (var player in
                     runner.ActivePlayers)
            {
                var activeActor =
                    runner.GetPlayerActorId(player)
                    ?? (player == runner.LocalPlayer
                        ? player.PlayerId
                        : 0);

                if (activeActor == actorNumber)
                {
                    return true;
                }
            }

            return false;
        }

        public async void MarkPlayerDisconnected(int actorNumber)
        {
            if (!IsHostBinding || !HasBinding || actorNumber < 1)
            {
                return;
            }

            var runner = _bootstrap?.Runner;

            if (runner == null
                || !runner.IsRunning
                || !runner.IsServer)
            {
                return;
            }

            var bindingGeneration = _bindingGeneration;
            var bindingMatchId = MatchId;

            if (!_boundPlayers.TryGetValue(
                    actorNumber,
                    out var expectedUserId)
                || _disconnectedActors.Contains(actorNumber))
            {
                RuntimeLog.Log(
                    RuntimeLogCategory.MatchAuthority,
                    $"MA|DISCONNECT_SKIP_UNBOUND|match={bindingMatchId:D}|actor={actorNumber}");
                return;
            }

            try
            {
                // Allow Fusion to finish updating ActivePlayers.
                await Task.Yield();

                if (_bindingGeneration != bindingGeneration
                    || MatchId != bindingMatchId
                    || !IsHostBinding
                    || _bootstrap?.Runner != runner
                    || !runner.IsRunning
                    || !runner.IsServer)
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.MatchAuthority,
                        $"MA|DISCONNECT_SKIP_STALE|match={bindingMatchId:D}|actor={actorNumber}");
                    return;
                }

                if (!_boundPlayers.TryGetValue(
                        actorNumber,
                        out var currentUserId)
                    || currentUserId != expectedUserId
                    || _disconnectedActors.Contains(actorNumber))
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.MatchAuthority,
                        $"MA|DISCONNECT_SKIP_REMAPPED|match={bindingMatchId:D}|actor={actorNumber}");
                    return;
                }

                if (IsActiveFusionActor(actorNumber))
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.MatchAuthority,
                        $"MA|DISCONNECT_SKIP_ACTIVE|match={bindingMatchId:D}|actor={actorNumber}");
                    return;
                }

                RuntimeLog.Log(
                    RuntimeLogCategory.MatchAuthority,
                    $"MA|DISCONNECT_REQUEST|match={bindingMatchId:D}|actor={actorNumber}|generation={bindingGeneration}");

                var result =
                    await _api.DisconnectPlayerAsync(
                        bindingMatchId,
                        actorNumber,
                        expectedUserId);

                if (_bindingGeneration != bindingGeneration
                    || MatchId != bindingMatchId
                    || !IsHostBinding
                    || _bootstrap?.Runner != runner
                    || !runner.IsRunning
                    || !runner.IsServer)
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.MatchAuthority,
                        $"MA|DISCONNECT_RESPONSE_STALE|match={bindingMatchId:D}|actor={actorNumber}");
                    return;
                }

                if (!_boundPlayers.TryGetValue(
                        actorNumber,
                        out currentUserId)
                    || currentUserId != expectedUserId
                    || IsActiveFusionActor(actorNumber))
                {
                    RuntimeLog.Log(
                        RuntimeLogCategory.MatchAuthority,
                        $"MA|DISCONNECT_RESPONSE_REMAPPED|match={bindingMatchId:D}|actor={actorNumber}");
                    return;
                }

                if (!IsSuccessful(result))
                {
                    Debug.LogWarning(
                        $"[MatchAuthority] Disconnect binding failed: {Describe(result)}");
                    return;
                }

                _disconnectedActors.Add(actorNumber);

                RuntimeLog.Log(
                    RuntimeLogCategory.MatchAuthority,
                    $"MA|DISCONNECT_COMMIT|match={bindingMatchId:D}|actor={actorNumber}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MatchAuthority] Disconnect processing failed. Actor={actorNumber}, Error={exception.Message}");
            }
        }

        public async Task<(bool Accepted, string Error)> StartMatchAsync()
        {
            if (_backendEndRequestInProgress)
            {
                return (false, "Previous match is still being finalized.");
            }

            if (HasBinding && !IsHostBinding)
            {
                return (false, "Backend match has already ended; prepare a new host binding.");
            }

            if (!IsHostBinding || !HasBinding)
            {
                const string error = "Backend Host binding is not available.";
                Debug.LogWarning($"[MatchAuthority] {error}");
                return (false, error);
            }

            var runner = _bootstrap?.Runner;
            if (runner == null || !runner.IsRunning || !runner.IsServer
                || !runner.SessionInfo.IsValid)
            {
                return (false, "Authoritative room is unavailable at match start.");
            }

            var mapId = LobbyMapCatalog.SceneName(LobbyMapCatalog.Selected(runner));
            var difficulty = requestedDifficulty.ToString().ToUpperInvariant();
            var result = await _api.StartAsync(MatchId, mapId, difficulty);
            var success = IsSuccessful(result);
            if (success)
            {
                _backendMatchStartedAtRealtime =
                    Time.realtimeSinceStartup;

                RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,
                $"[MatchAuthority] Backend confirmed match start. Match={MatchId:D}.");
                return (true, string.Empty);
            }
            var failure = Describe(result);
            Debug.LogWarning($"[MatchAuthority] Backend rejected match start: {failure}.");
            return (false, failure);
        }

        public void RecordObjectiveContribution(PlayerRef actor)
        {
            var runner = _bootstrap?.Runner;
            if (runner == null || !actor.IsRealPlayer) return;

            var actorNumber = runner.GetPlayerActorId(actor) ?? actor.PlayerId;
            if (_boundPlayers.ContainsKey(actorNumber))
            {
                _objectiveContributors.Add(actorNumber);
            }
        }

        public void ResetBinding()
        {
            _bindingGeneration++;
            var oldMatchId = MatchId;
            MatchId = Guid.Empty;
            IsHostBinding = false;
            requestedScenarioResolutionMode = ScenarioResolutionMode.Fixed;
            requestedDifficulty = MatchDifficulty.Normal;
            experimentCondition = string.Empty;
            experimentProtocolVersion =
                string.Empty;
            _leaseRequestInProgress = false;
            _identityRequestInProgress = false;
            _playerBindsInProgress.Clear();
            _backendEndRequestInProgress = false;
            _pendingBackendMatchResult = false;
            _nextBackendMatchResultRetryAt = 0f;
            _pendingBackendOutcome = null;
            _pendingObjectiveCompletion = 0f;
            _backendMatchStartedAtRealtime = -1f;
            _boundPlayers.Clear();
            _disconnectedActors.Clear();
            _objectiveContributors.Clear();
            _matchStartedAtUtc = default;
            _telemetryMatchActive = false;
            _matchEndEmitted = false;
            _pendingMatchEnd = false;
            _pendingMatchEndOccurrenceKey = null;
            _pendingMatchEndOutcome = null;
            _pendingMatchEndSurvivorCount = 0;
            _pendingMatchEndReasonCode = null;
            _nextMatchEndRetryAt = 0f;
            _matchEndRetryWarningLogged = false;
            _pendingAuthoritativeTelemetryMatchStart = false;
            _currentTelemetryPhase = "CORE_COLLECTION";
            _runtimeNoiseTelemetryCapacityWarningLogged = false;
            _runtimeNoiseTelemetryInactiveWarningLogged = false;
            _runtimeNoise?.ResetForMatch();
            ScenarioConfigRuntimeRegistry.Clear(oldMatchId);
            ScenarioConfigAuthorityRuntime.Instance?.ResetForMatch(oldMatchId);
        }

        public bool TryGetMatchId(out Guid matchId)
        {
            matchId = MatchId;
            return matchId != Guid.Empty;
        }

        public TelemetryProvenanceSnapshot Capture()
        {
            if (MatchId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Telemetry provenance is unavailable: " +
                    "no authoritative match is bound.");
            }

            if (!ScenarioConfigRuntimeRegistry
                    .TryGetAppliedConfig(
                        MatchId,
                        out var applied)
                || applied == null)
            {
                throw new InvalidOperationException(
                    "Telemetry provenance is unavailable: " +
                    "no AppliedScenarioConfig exists for " +
                    "the authoritative match.");
            }

            return new TelemetryProvenanceSnapshot(
                applied.ScenarioConfigVersion,
                applied.PolicyVersion,
                applied.ConfigSource
                    == ScenarioConfigSource.Adaptive
                        ? TelemetryConfigSource.Adaptive
                        : TelemetryConfigSource.Fixed,
                _researchCaptureEnabled);
        }

        private void HandleSessionStateChanged(NetworkSessionState state, string message)
        {
            if (state != NetworkSessionState.InMatch || !HasStateAuthority) return;
            _runtimeNoise?.BeginMatch(MatchId);
            _pendingAuthoritativeTelemetryMatchStart = true;
            TryBeginAuthoritativeTelemetryMatch();
        }

        public void NotifyScenarioResolutionFinalized(
            Guid matchId,
            ScenarioResolutionRecord record)
        {
            if (!HasStateAuthority
                || matchId == Guid.Empty
                || matchId != MatchId
                || record == null
                || record.TargetMatchId != matchId)
            {
                return;
            }

            TryBeginAuthoritativeTelemetryMatch();
        }

        private void TryBeginAuthoritativeTelemetryMatch()
        {
            if (!_pendingAuthoritativeTelemetryMatchStart
                || _telemetryMatchActive
                || !HasStateAuthority
                || MatchId == Guid.Empty
                || _telemetry == null
                || !ScenarioConfigRuntimeRegistry
                    .TryGetAppliedConfig(
                        MatchId,
                        out var applied)
                || applied == null)
            {
                return;
            }

            var scenarioRuntime =
                ScenarioConfigAuthorityRuntime.Instance;

            var resolutionRecord =
                scenarioRuntime != null
                    ? scenarioRuntime.LastResolutionRecord
                    : null;

            if (resolutionRecord == null
                || resolutionRecord.TargetMatchId
                    != MatchId
                || !resolutionRecord.HasStateAuthority)
            {
                return;
            }

            var resolvedContentWhitelistVersion =
                resolutionRecord.ContentWhitelistVersion;

            if (string.IsNullOrWhiteSpace(
                    telemetryMapContentVersion)
                || string.IsNullOrWhiteSpace(
                    resolvedContentWhitelistVersion))
            {
                Debug.LogError(
                    "[Telemetry] MATCH_STARTED blocked: " +
                    "map/content provenance binding is missing.");

                return;
            }

            string activeExperimentCondition =
                string.IsNullOrWhiteSpace(
                    experimentCondition)
                    ? null
                    : experimentCondition;

            string activeExperimentProtocolVersion =
                string.IsNullOrWhiteSpace(
                    experimentProtocolVersion)
                    ? null
                    : experimentProtocolVersion;

            if ((activeExperimentCondition == null)
                != (activeExperimentProtocolVersion == null))
            {
                Debug.LogError(
                    "[Telemetry] MATCH_STARTED blocked: " +
                    "experimentCondition and " +
                    "experimentProtocolVersion must " +
                    "either both be present or both be absent.");

                return;
            }

            if (activeExperimentCondition != null
                && !string.Equals(
                    activeExperimentCondition,
                    "FIXED",
                    StringComparison.Ordinal)
                && !string.Equals(
                    activeExperimentCondition,
                    "ADAPTIVE",
                    StringComparison.Ordinal))
            {
                Debug.LogError(
                    "[Telemetry] MATCH_STARTED blocked: " +
                    "invalid experimentCondition.");

                return;
            }

            var teamSize = 0;

            foreach (var _ in
                     _bootstrap.Runner.ActivePlayers)
            {
                teamSize++;
            }

            if (teamSize < 1)
            {
                Debug.LogWarning(
                    "[Telemetry] MATCH_STARTED deferred: " +
                    "authoritative roster is empty.");

                return;
            }

            if (!_telemetry.TryInitialize())
            {
                return;
            }

            var sequenceAllocator =
                _telemetry.SequenceAllocator;

            if (sequenceAllocator == null)
            {
                return;
            }

            if (sequenceAllocator.IsActive)
            {
                if (sequenceAllocator.MatchId != MatchId)
                {
                    Debug.LogError(
                        "[Telemetry] MATCH_STARTED blocked: " +
                        "another telemetry match is already active.");

                    return;
                }

                // Same authoritative match already owns the sequence
                // domain. Do not BeginMatch again. This allows a
                // previously created-but-not-buffered MATCH_STARTED
                // occurrence to retry through TelemetryEmitter.
            }
            else if (!_telemetry.TryBeginAuthoritativeMatch())
            {
                return;
            }

            var occurredAtUtc =
                DateTime.UtcNow;

            if (!_telemetry.MatchAdapter.EmitMatchStarted(
                "match-start",
                occurredAtUtc,
                applied.MapId,
                teamSize,
                Application.version,
                telemetryMapContentVersion,
                resolvedContentWhitelistVersion,
                _researchCaptureEnabled,
                out _,
                out var startFailure,
                experimentCondition:
                    activeExperimentCondition,
                experimentProtocolVersion:
                    activeExperimentProtocolVersion))
            {
                Debug.LogWarning($"[Telemetry] MATCH_STARTED was not buffered: {startFailure}.");
                return;
            }

            _matchStartedAtUtc = occurredAtUtc;
            _telemetryMatchActive = true;
            _pendingAuthoritativeTelemetryMatchStart = false;
            _matchEndEmitted = false;
            _currentTelemetryPhase = "CORE_COLLECTION";
            if (!_telemetry.MatchAdapter.EmitPhaseStarted(
                    "phase:core-collection:start:1",
                    occurredAtUtc,
                    "CORE_COLLECTION",
                    out _,
                    out var phaseFailure))
            {
                Debug.LogWarning($"[Telemetry] initial PHASE_STARTED was not buffered: {phaseFailure}.");
            }
        }

        private static ScenarioResolutionMode ReadScenarioResolutionMode(NetworkRunner runner)
        {
            if (runner != null
                && runner.SessionInfo.IsValid
                && runner.SessionInfo.Properties != null
                && runner.SessionInfo.Properties.TryGetValue(ScenarioResolutionModeSessionProperty, out var property))
            {
                var raw = ((string)property) ?? string.Empty;
                if (string.Equals(raw, "ADAPTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    return ScenarioResolutionMode.Adaptive;
                }
            }

            return ScenarioResolutionMode.Fixed;
        }

        private static MatchDifficulty ReadMatchDifficulty(NetworkRunner runner)
        {
            if (runner != null && runner.SessionInfo.IsValid
                && runner.SessionInfo.Properties != null
                && runner.SessionInfo.Properties.TryGetValue(MatchDifficultySessionProperty, out var property)
                && Enum.TryParse(((string)property) ?? string.Empty, true, out MatchDifficulty difficulty)
                && Enum.IsDefined(typeof(MatchDifficulty), difficulty))
            {
                return difficulty;
            }

            return MatchDifficulty.Normal;
        }

        private void HandlePickupStateCommitted(NetworkItemTransition transition)
        {
            // Telemetry is observational. Its lifecycle may still be attached to
            // a previous match after Host Migration; it must never interrupt an
            // authoritative inventory or objective state transition.
            var item = transition.Item;
            var actor = transition.Actor;
            if (!HasStateAuthority || !actor.IsValid || item == null)
            {
                return;
            }

            if (!CanEmitProductionTelemetry())
            {
                var allocator = _telemetry?.SequenceAllocator;
                if (allocator != null && allocator.IsActive
                    && allocator.MatchId != MatchId)
                {
                    Debug.LogWarning(
                        $"P04O_TELEMETRY_SKIPPED|reason=MatchIdMismatch|authority={MatchId:D}|" +
                        $"allocator={allocator.MatchId:D}|item={item.Object.Id}|state={transition.State}");
                }
                return;
            }

            try
            {
                if (!TryResolveBackendUser(actor, out var userId))
                {
                    return;
                }

                var coreId = item.Object.Id.ToString();
                string eventType;
                string transitionName;
                switch (transition.State)
                {
                    case NetworkItemState.Carried:
                        eventType = TelemetryEventTypes.CorePickedUp;
                        transitionName = "pickup";
                        break;
                    case NetworkItemState.Dropped:
                        eventType = TelemetryEventTypes.CoreDropped;
                        transitionName = "drop";
                        break;
                    case NetworkItemState.Placed:
                        eventType = TelemetryEventTypes.CorePlaced;
                        transitionName = "place";
                        break;
                    default:
                        return;
                }

                _telemetry.ObjectiveAdapter.EmitCoreTransition(
                    $"{coreId}:{transitionName}:{transition.Ordinal}",
                    DateTime.UtcNow,
                    eventType,
                    userId,
                    coreId,
                    out _,
                    out _,
                    Snapshot(transition.Position));
            }
            catch (Exception exception)
            {
                // Do not rethrow into NetworkPickupItem.TryPlace(): it has
                // already committed the Core pose and carrier state. Rethrowing
                // here would prevent NetworkSectorBox.PlacedCoreCount advancing.
                Debug.LogWarning(
                    $"P04O_TELEMETRY_NONBLOCKING_ERROR|item={item.Object.Id}|" +
                    $"state={transition.State}|kind={exception.GetType().Name}|" +
                    $"message={exception.Message}");
            }
        }

        private void EmitAbortedMatchEndIfActive()
        {
            if (!_telemetryMatchActive || _matchEndEmitted || !HasStateAuthority
                || _telemetry == null || !_telemetry.IsInitialized)
            {
                return;
            }

            var occurredAtUtc = DateTime.UtcNow;
            var durationSeconds = Math.Max(0d, (occurredAtUtc - _matchStartedAtUtc).TotalSeconds);
            var connectedPlayers = 0;
            foreach (var _ in _bootstrap.Runner.ActivePlayers) connectedPlayers++;

            try
            {
                if (!_telemetry.MatchAdapter.EmitMatchEnded(
                        "match-end:host-shutdown",
                        occurredAtUtc,
                        "ABORTED",
                        durationSeconds,
                        connectedPlayers,
                        "MATCH_ABORTED",
                        out _,
                        out var failure))
                {
                    Debug.LogWarning($"[Telemetry] MATCH_ENDED was not buffered: {failure}.");
                    return;
                }

                _matchEndEmitted = true;
                _telemetryMatchActive = false;
                _telemetry.TryFlushNow();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Telemetry] MATCH_ENDED could not be emitted: " + exception.Message);
            }
        }

        public bool RecordMatchEnded(string occurrenceKey, string outcome, int survivorCount, string reasonCode)
        {
            if (!CanEmitProductionTelemetry() || _matchEndEmitted) return false;

            _pendingMatchEnd = true;
            _pendingMatchEndOccurrenceKey = occurrenceKey;
            _pendingMatchEndOutcome = outcome;
            _pendingMatchEndSurvivorCount = survivorCount;
            _pendingMatchEndReasonCode = reasonCode;
            return TryEmitPendingMatchEnd();
        }

        private bool TryEmitPendingMatchEnd()
        {
            if (!_pendingMatchEnd || !CanEmitProductionTelemetry() || _matchEndEmitted)
            {
                return false;
            }

            _nextMatchEndRetryAt = Time.unscaledTime + 1f;
            var occurredAtUtc = DateTime.UtcNow;
            var durationSeconds = Math.Max(0d, (occurredAtUtc - _matchStartedAtUtc).TotalSeconds);
            try
            {
                if (!_telemetry.MatchAdapter.EmitMatchEnded(
                        _pendingMatchEndOccurrenceKey,
                        occurredAtUtc,
                        _pendingMatchEndOutcome,
                        durationSeconds,
                        _pendingMatchEndSurvivorCount,
                        _pendingMatchEndReasonCode,
                        out _,
                        out var failure))
                {
                    if (!_matchEndRetryWarningLogged)
                    {
                        _matchEndRetryWarningLogged = true;
                        Debug.LogWarning($"[Telemetry] MATCH_ENDED was not buffered: {failure}. Retrying with the same occurrence.");
                    }
                    return false;
                }

                _matchEndEmitted = true;
                _telemetryMatchActive = false;
                _pendingMatchEnd = false;
                _telemetry.TryFlushNow();
                return true;
            }
            catch (Exception exception)
            {
                if (!_matchEndRetryWarningLogged)
                {
                    _matchEndRetryWarningLogged = true;
                    Debug.LogWarning("[Telemetry] MATCH_ENDED could not be emitted: " + exception.Message);
                }
                return false;
            }
        }

        private void ResetLocalRewardTracking()
        {
            _localRewardMatchId = Guid.Empty;
            _localRewardFetchQueued = false;
            _localRewardRequestInProgress = false;
            _localRewardFetchAttempts = 0;
            _nextLocalRewardFetchAt = 0f;
            LastLocalReward = null;
        }

        private void QueueLocalRewardFetch(Guid matchId)
        {
            if (matchId == Guid.Empty || _localRewardMatchId == matchId)
            {
                return;
            }

            _localRewardMatchId = matchId;
            _localRewardFetchQueued = true;
            _localRewardFetchAttempts = 0;
            _nextLocalRewardFetchAt = Time.unscaledTime;
            LastLocalReward = null;
        }

        private async void TryFetchLocalReward()
        {
            if (!_localRewardFetchQueued
                || _localRewardRequestInProgress
                || _localRewardMatchId == Guid.Empty)
            {
                return;
            }

            var matchId = _localRewardMatchId;
            _localRewardRequestInProgress = true;
            _localRewardFetchAttempts++;
            var result = await _api.GetMyRewardAsync(matchId);
            _localRewardRequestInProgress = false;

            if (_localRewardMatchId != matchId)
            {
                return;
            }

            if (IsSuccessful(result))
            {
                LastLocalReward = result.Data.data;
                _localRewardFetchQueued = false;
                LocalRewardUpdated?.Invoke(LastLocalReward);
                RuntimeLog.Log(
                    RuntimeLogCategory.MatchAuthority,
                    $"[MatchAuthority] Local reward loaded. Match={matchId:D}, " +
                    $"Credits=+{LastLocalReward.currencyAmount}, " +
                    $"XP=+{LastLocalReward.experiencePointsAwarded}, " +
                    $"Level={LastLocalReward.currentLevel}.");
                return;
            }

            var retryableBusinessError = result != null
                && result.FailureKind == EchoProtocol.Api.ApiFailureKind.Business
                && (string.Equals(result.ErrorCode, "REWARD_PENDING", StringComparison.Ordinal)
                    || string.Equals(result.ErrorCode, "REWARD_RESULT_NOT_FOUND", StringComparison.Ordinal));
            var retryableTransportError = result == null
                || result.FailureKind == EchoProtocol.Api.ApiFailureKind.Network
                || result.FailureKind == EchoProtocol.Api.ApiFailureKind.Timeout;

            if ((retryableBusinessError || retryableTransportError)
                && _localRewardFetchAttempts < LocalRewardMaxAttempts)
            {
                _nextLocalRewardFetchAt = Time.unscaledTime + LocalRewardRetryIntervalSeconds;
                return;
            }

            _localRewardFetchQueued = false;
            Debug.LogWarning(
                $"[MatchAuthority] Local reward could not be loaded: {Describe(result)}");
        }

        public void QueueMatchResult(
            string outcome,
            float objectiveCompletion)
        {
            if (!HasBinding || !IsHostBinding)
            {
                return;
            }

            _pendingBackendOutcome =
                string.Equals(
                    outcome,
                    "WIN",
                    StringComparison.Ordinal)
                    ? "WIN"
                    : "LOSE";

            _pendingObjectiveCompletion =
                Mathf.Clamp01(objectiveCompletion);

            _pendingBackendMatchResult = true;
            CaptureResultOutbox();

            float remainingDelay = 0f;

            if (_backendMatchStartedAtRealtime >= 0f)
            {
                float matchAge =
                    Time.realtimeSinceStartup
                    - _backendMatchStartedAtRealtime;

                remainingDelay =
                    Mathf.Max(
                        0f,
                        MinimumBackendMatchResultAgeSeconds
                        - matchAge);
            }

            _nextBackendMatchResultRetryAt =
                Time.unscaledTime + remainingDelay;

            if (remainingDelay <= 0f)
            {
                TrySubmitPendingMatchResult();
            }
        }

        private void TrySubmitPendingMatchResult()
        {
            CaptureResultOutbox();
            TickResultOutbox();
        }

        private bool TryBuildMatchResultRequest(out SubmitMatchResultRequestDto request)
        {
            request = null;
            var runner = _bootstrap?.Runner;
            if (runner == null || _boundPlayers.Count == 0)
            {
                Debug.LogWarning(
                    "[MatchAuthority] Match result deferred: authoritative backend roster is unavailable.");
                return false;
            }

            var players = new List<SubmitMatchResultPlayerDto>(_boundPlayers.Count);
            foreach (var binding in _boundPlayers)
            {
                var actorNumber = binding.Key;
                var disconnected = _disconnectedActors.Contains(actorNumber);
                var survived = false;
                var downedCount = 0;
                var reviveCount = 0;

                if (!disconnected
                    && TryFindActivePlayerByActorNumber(actorNumber, out var player)
                    && runner.TryGetPlayerObject(player, out var playerObject)
                    && playerObject != null
                    && playerObject.TryGetComponent<NetworkPlayerLifeState>(out var lifeState))
                {
                    survived = lifeState.Status == NetworkPlayerLifeStatus.Escaped;
                    downedCount = Mathf.Max(0, lifeState.DownCount);
                    reviveCount = Mathf.Max(0, lifeState.ReviveCount);
                }

                players.Add(new SubmitMatchResultPlayerDto
                {
                    userId = binding.Value.ToString("D"),
                    survived = survived,
                    disconnected = disconnected,
                    detectionCount = 0,
                    downedCount = downedCount,
                    reviveCount = reviveCount,
                    objectiveContribution = _objectiveContributors.Contains(actorNumber) ? 1 : 0
                });
            }

            players.Sort((left, right) =>
                string.CompareOrdinal(left.userId, right.userId));

            request = new SubmitMatchResultRequestDto
            {
                outcome = _pendingBackendOutcome,
                objectiveCompletion = _pendingObjectiveCompletion,
                players = players.ToArray()
            };
            return true;
        }

        private bool TryFindActivePlayerByActorNumber(
            int actorNumber,
            out PlayerRef player)
        {
            player = PlayerRef.None;
            var runner = _bootstrap?.Runner;
            if (runner == null)
            {
                return false;
            }

            foreach (var candidate in runner.ActivePlayers)
            {
                var candidateActor =
                    runner.GetPlayerActorId(candidate)
                    ?? candidate.PlayerId;
                if (candidateActor != actorNumber)
                {
                    continue;
                }

                player = candidate;
                return true;
            }

            return false;
        }

        public bool RecordPlayerDowned(
            PlayerRef player,
            string occurrenceKey,
            string monsterType,
            int downCount,
            Vector3 position)
        {
            if (!CanEmitProductionTelemetry() || !TryResolveBackendUser(player, out var userId))
            {
                return false;
            }

            var reasonCode = monsterType == "LISTENER" ? "LISTENER_ATTACK" : "STALKER_ATTACK";
            return _telemetry.PlayerAdapter.EmitPlayerDowned(
                occurrenceKey,
                DateTime.UtcNow,
                userId,
                _currentTelemetryPhase,
                monsterType,
                reasonCode,
                out _,
                out _,
                downCount,
                Snapshot(position));
        }

        public bool RecordPlayerRevived(
            PlayerRef revivedPlayer,
            PlayerRef reviver,
            string occurrenceKey,
            int reviveCount,
            bool usedFirstAidKit)
        {
            if (!CanEmitProductionTelemetry()
                || !TryResolveBackendUser(revivedPlayer, out var revivedUserId)
                || !TryResolveBackendUser(reviver, out var reviverUserId))
            {
                return false;
            }

            return _telemetry.PlayerAdapter.EmitPlayerRevived(
                occurrenceKey,
                DateTime.UtcNow,
                revivedUserId,
                reviverUserId,
                _currentTelemetryPhase,
                out _,
                out _,
                reviveCount,
                usedFirstAidKit);
        }

        public bool RecordPlayerEliminated(
            PlayerRef player,
            string occurrenceKey,
            int reviveCount)
        {
            if (!CanEmitProductionTelemetry() || !TryResolveBackendUser(player, out var userId))
            {
                return false;
            }

            return _telemetry.PlayerAdapter.EmitPlayerEliminated(
                occurrenceKey,
                DateTime.UtcNow,
                userId,
                _currentTelemetryPhase,
                out _,
                out _,
                reviveCount);
        }

        public bool RecordPlayerEscaped(
            PlayerRef player,
            string occurrenceKey,
            bool rescuedTeammate)
        {
            if (!CanEmitProductionTelemetry() || !TryResolveBackendUser(player, out var userId))
            {
                return false;
            }

            return _telemetry.PlayerAdapter.EmitPlayerEscaped(
                occurrenceKey,
                DateTime.UtcNow,
                userId,
                out _,
                out _,
                rescuedTeammate);
        }

        public bool RecordPhaseCompleted(string occurrenceKey, string phase, string reasonCode)
        {
            if (!CanEmitProductionTelemetry()) return false;
            return _telemetry.MatchAdapter.EmitPhaseCompleted(
                occurrenceKey,
                DateTime.UtcNow,
                phase,
                out _,
                out _,
                null,
                reasonCode);
        }

        public bool RecordPhaseStarted(string occurrenceKey, string phase, string reasonCode)
        {
            if (!CanEmitProductionTelemetry()) return false;
            var emitted = _telemetry.MatchAdapter.EmitPhaseStarted(
                occurrenceKey,
                DateTime.UtcNow,
                phase,
                out _,
                out _,
                reasonCode);
            if (emitted) _currentTelemetryPhase = phase;
            return emitted;
        }

        public bool RecordPuzzleCompleted(string occurrenceKey)
        {
            return CanEmitProductionTelemetry()
                && _telemetry.ObjectiveAdapter.EmitPuzzleCompleted(
                    occurrenceKey,
                    DateTime.UtcNow,
                    out _,
                    out _);
        }

        public bool RecordSecurityHoldInterrupted(string occurrenceKey)
        {
            return CanEmitProductionTelemetry()
                && _telemetry.ObjectiveAdapter.EmitSecurityHoldInterrupted(
                    occurrenceKey,
                    DateTime.UtcNow,
                    out _,
                    out _);
        }

        public bool RecordTeamToolUsed(
            PlayerRef player,
            string occurrenceKey,
            string toolType,
            string targetId = null)
        {
            // ponytail: Telemetry 1.1 rejects Core Stabilizer; emit it after the schema and backend catalog support it.
            if (toolType == "CORE_STABILIZER") return false;

            if (!CanEmitProductionTelemetry() || !TryResolveBackendUser(player, out var userId))
            {
                return false;
            }

            return _telemetry.PlayerAdapter.EmitTeamToolUsed(
                occurrenceKey,
                DateTime.UtcNow,
                userId,
                _currentTelemetryPhase,
                toolType,
                out _,
                out _,
                targetId);
        }

        public bool RecordHelpPingUsed(
            PlayerRef player,
            string occurrenceKey,
            Vector3 position)
        {
            if (!CanEmitProductionTelemetry() || !TryResolveBackendUser(player, out var userId))
            {
                return false;
            }

            return _telemetry.PlayerAdapter.EmitHelpPingUsed(
                occurrenceKey,
                DateTime.UtcNow,
                userId,
                _currentTelemetryPhase,
                out _,
                out _,
                Snapshot(position));
        }

        public bool RecordRuntimeNoise(
            PlayerRef player,
            string noiseEventId,
            DateTime emittedAtUtc,
            string noiseType,
            double loudness,
            Vector3 position,
            double hearingRadius)
        {
            // ponytail: gameplay noise is broader than Telemetry 1.1.
            // Unsupported types remain gameplay-only until the telemetry contract expands.
            if (!NoiseTelemetryAdapter.SupportsNoiseType(noiseType)) return false;

            if (!CanEmitProductionTelemetry() || !TryResolveBackendUser(player, out var userId))
            {
                return false;
            }

            try
            {
                return _telemetry.NoiseAdapter.EmitAcceptedRuntimeNoise(
                    noiseEventId,
                    emittedAtUtc,
                    userId,
                    _currentTelemetryPhase,
                    noiseType,
                    loudness,
                    Snapshot(position),
                    out _,
                    out _,
                    hearingRadius);
            }
            catch (InvalidOperationException exception)
                when (string.Equals(
                    exception.Message,
                    "Telemetry occurrence identity capacity is exhausted.",
                    StringComparison.Ordinal))
            {
                if (!_runtimeNoiseTelemetryCapacityWarningLogged)
                {
                    _runtimeNoiseTelemetryCapacityWarningLogged = true;

                    Debug.LogWarning(
                        "[Telemetry] Runtime noise telemetry occurrence "
                        + "capacity was exhausted. Runtime noise gameplay "
                        + "continues; additional noise telemetry is suppressed "
                        + "for this match.");
                }

                return false;
            }
            catch (InvalidOperationException exception)
                when (string.Equals(
                    exception.Message,
                    "Telemetry sequence allocation requires an active match.",
                    StringComparison.Ordinal))
            {
                if (!_runtimeNoiseTelemetryInactiveWarningLogged)
                {
                    _runtimeNoiseTelemetryInactiveWarningLogged = true;
                    var allocator = _telemetry?.SequenceAllocator;
                    Debug.LogWarning(
                        $"[Telemetry] Runtime noise skipped because telemetry "
                        + $"is inactive. Match={MatchId:D}, "
                        + $"allocatorMatch={allocator?.MatchId:D}, "
                        + $"isActive={allocator?.IsActive}, "
                        + $"isTerminal={allocator?.IsTerminal}, "
                        + $"telemetryMatchActive={_telemetryMatchActive}, "
                        + $"matchEndEmitted={_matchEndEmitted}, "
                        + $"pendingBuffer={_telemetry?.Buffer?.PendingCount}.");
                }

                return false;
            }
        }

        public bool RecordStalkerAttackResolved(
            string monsterId,
            string attackEpisodeId,
            string outcome)
        {
            return TryRecordStalkerAttackResolved(monsterId, attackEpisodeId, outcome)
                == ProductionTelemetryPublishResult.Accepted;
        }

        public bool RecordStalkerSearchEnded(
            string monsterId,
            string searchEpisodeId,
            string outcome)
        {
            return TryRecordStalkerSearchEnded(monsterId, searchEpisodeId, outcome)
                == ProductionTelemetryPublishResult.Accepted;
        }

        public ProductionTelemetryPublishResult TryRecordStalkerAttackResolved(
            string monsterId,
            string attackEpisodeId,
            string outcome)
        {
            if (!_researchCaptureEnabled) return ProductionTelemetryPublishResult.Suppressed;
            if (!CanEmitProductionTelemetry()) return ProductionTelemetryPublishResult.RetryableFailure;

            try
            {
                return _telemetry.MonsterAdapter.EmitAttackResolved(
                    $"monster:{monsterId}:attack:{attackEpisodeId}:resolved",
                    DateTime.UtcNow,
                    _currentTelemetryPhase,
                    "STALKER",
                    monsterId,
                    attackEpisodeId,
                    outcome,
                    out _,
                    out _)
                    ? ProductionTelemetryPublishResult.Accepted
                    : ProductionTelemetryPublishResult.RetryableFailure;
            }
            catch (ArgumentException)
            {
                return ProductionTelemetryPublishResult.InvalidOccurrence;
            }
            catch (InvalidOperationException)
            {
                return _researchCaptureEnabled
                    ? ProductionTelemetryPublishResult.RetryableFailure
                    : ProductionTelemetryPublishResult.Suppressed;
            }
        }

        public ProductionTelemetryPublishResult TryRecordStalkerSearchEnded(
            string monsterId,
            string searchEpisodeId,
            string outcome)
        {
            if (!_researchCaptureEnabled) return ProductionTelemetryPublishResult.Suppressed;
            if (!CanEmitProductionTelemetry()) return ProductionTelemetryPublishResult.RetryableFailure;

            try
            {
                return _telemetry.MonsterAdapter.EmitStalkerSearchEnded(
                    $"monster:{monsterId}:search:{searchEpisodeId}:ended",
                    DateTime.UtcNow,
                    _currentTelemetryPhase,
                    monsterId,
                    searchEpisodeId,
                    outcome,
                    out _,
                    out _)
                    ? ProductionTelemetryPublishResult.Accepted
                    : ProductionTelemetryPublishResult.RetryableFailure;
            }
            catch (ArgumentException)
            {
                return ProductionTelemetryPublishResult.InvalidOccurrence;
            }
            catch (InvalidOperationException)
            {
                return _researchCaptureEnabled
                    ? ProductionTelemetryPublishResult.RetryableFailure
                    : ProductionTelemetryPublishResult.Suppressed;
            }
        }

        private bool CanEmitProductionTelemetry()
        {
            var allocator = _telemetry?.SequenceAllocator;
            return HasStateAuthority && _telemetryMatchActive
                && _telemetry != null && _telemetry.IsInitialized
                && allocator != null
                && allocator.IsActive
                && allocator.MatchId == MatchId;
        }

        private bool TryResolveBackendUser(PlayerRef player, out Guid userId)
        {
            userId = Guid.Empty;
            if (_bootstrap?.Runner == null || !player.IsValid
                || !_bootstrap.Runner.TryGetPlayerObject(player, out var playerObject)
                || !playerObject.TryGetComponent<LobbyPlayerState>(out var playerState)
                || !Guid.TryParse(playerState.BackendUserId.ToString(), out userId))
            {
                Debug.LogWarning($"[Telemetry] Ignored authoritative player event: no verified identity for {player}.");
                return false;
            }

            return true;
        }

        private static TelemetryPositionSnapshot Snapshot(Vector3 position)
        {
            return new TelemetryPositionSnapshot(position.x, position.y, position.z);
        }

        private static bool IsSuccessful<T>(EchoProtocol.Api.ApiResult<EchoProtocol.Api.ApiResponse<T>> result) where T : class =>
            result != null && result.IsSuccess && result.Data != null && result.Data.success && result.Data.data != null;

        private static string Describe<T>(EchoProtocol.Api.ApiResult<EchoProtocol.Api.ApiResponse<T>> result) where T : class
        {
            if (result == null) return "No backend response";
            if (!string.IsNullOrWhiteSpace(result.ErrorCode)) return $"{result.ErrorCode}: {result.Message}";
            if (result.Data != null && !string.IsNullOrWhiteSpace(result.Data.errorCode))
            {
                return $"{result.Data.errorCode}: {result.Data.message}";
            }
            return string.IsNullOrWhiteSpace(result.Message) ? "Backend request failed" : result.Message;
        }
    }
}
