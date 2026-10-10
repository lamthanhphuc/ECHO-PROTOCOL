using System;
using EchoProtocol.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    public sealed class MatchAuthorityRuntime : MonoBehaviour,
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
        private const string PlayerBindingConflictErrorCode =
            "MATCH_PLAYER_BINDING_CONFLICT";

        private static MatchAuthorityRuntime _instance;
        private MatchAuthorityApiService _api;
        private NetworkBootstrap _bootstrap;
        private float _nextLeaseRenewal;
        private bool _leaseRequestInProgress;
        private bool _identityRequestInProgress;
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
        private readonly AEDv2MatchEvidenceCollector _aedv2Evidence = new AEDv2MatchEvidenceCollector();
        private readonly AEDObjectiveEvidenceCollectorV1 _aedObjectiveEvidence =
            new AEDObjectiveEvidenceCollectorV1();
        private readonly AEDSurvivalEvidenceCollectorV1 _aedSurvivalEvidence =
            new AEDSurvivalEvidenceCollectorV1();
        private readonly AEDToolNoiseEvidenceCollectorV1 _aedToolNoiseEvidence =
            new AEDToolNoiseEvidenceCollectorV1();
        private readonly AEDPursuitEvidenceCollectorV1 _aedPursuitEvidence =
            new AEDPursuitEvidenceCollectorV1();
        private readonly AEDMinionEvidenceCollectorV1 _aedMinionEvidence =
            new AEDMinionEvidenceCollectorV1();
        public AEDv2CurrentMatchEvidence LastFrozenAEDv2Evidence => _aedv2Evidence.LastFrozen;
        public IReadOnlyDictionary<string, AEDv2PlayerPhaseEvidence> AEDv2PlayerEvidence => _aedv2Evidence.PlayerEvidence;
        public IReadOnlyDictionary<string, AEDv2PlayerPhaseEvidence> LastFrozenAEDv2PlayerEvidence => _aedv2Evidence.LastFrozenPlayerEvidence;
        public AEDObjectiveEvidenceV1 LastFrozenAEDObjectiveEvidence => _aedObjectiveEvidence.LastFrozen;
        public AEDSurvivalEvidenceSnapshotV1 LastFrozenAEDSurvivalEvidence => _aedSurvivalEvidence.LastFrozen;
        public AEDToolNoiseEvidenceSnapshotV1 LastFrozenAEDToolNoiseEvidence => _aedToolNoiseEvidence.LastFrozen;
        public AEDPursuitEvidenceSnapshotV1 LastFrozenAEDPursuitEvidence => _aedPursuitEvidence.LastFrozen;
        public AEDMinionEvidenceSnapshotV1 LastFrozenAEDMinionEvidence => _aedMinionEvidence.LastFrozen;
        public AEDCurrentMatchPressureV1 LastFrozenAEDCurrentPressure
            { get; private set; }
        public void MarkAEDv2EvidenceIncomplete()
        {
            _aedv2Evidence.MarkIncomplete();
        }

        private long CurrentAuthorityTick => _bootstrap?.Runner != null
            ? _bootstrap.Runner.Tick.Raw : 0L;

        public bool TryResolveVerifiedBackendUserId(PlayerRef player, out string userId)
        {
            userId = null;
            if (!TryResolveBackendUser(player, out var verifiedId)) return false;
            userId = verifiedId.ToString("D");
            return true;
        }

        public void RecordStalkerPursuitSnapshot(string stalkerNetworkId,
            string state, PlayerRef target, long tick, int tickRate)
        {
            if (!HasStateAuthority || !_telemetryMatchActive) return;
            string targetUserId = null;
            if (target.IsValid
                && !TryResolveVerifiedBackendUserId(target, out targetUserId)
                && state == "CHASE")
                _aedPursuitEvidence.MarkIncomplete();
            _aedPursuitEvidence.Observe(true, stalkerNetworkId,
                targetUserId, state, tick, tickRate,
                $"stalker-state:{stalkerNetworkId}:{tick}");
        }

        public void RecordStalkerPursuitFact(string stalkerNetworkId,
            PlayerRef target,
            AEDPursuitFactKindV1 kind, string occurrenceKey,
            long tick, string cause, bool directFromHit = false)
        {
            if (!HasStateAuthority || !_telemetryMatchActive) return;
            if (!TryResolveVerifiedBackendUserId(target, out var userId))
            {
                _aedPursuitEvidence.MarkIncomplete();
                return;
            }
            _aedPursuitEvidence.RecordConsequence(stalkerNetworkId, userId, kind,
                occurrenceKey, tick, cause, directFromHit);
        }

        public void RecordStalkerDespawn(string stalkerNetworkId, long tick)
        {
            if (HasStateAuthority)
                _aedPursuitEvidence.CensorStalker(stalkerNetworkId,
                    "STALKER_DESPAWN", tick);
        }

        public void RecordMinionSnapshot(NetworkId minionId, string zone,
            string state, PlayerRef target, long tick, int tickRate)
        {
            if (!HasStateAuthority || !_telemetryMatchActive) return;
            string userId = null;
            if (target.IsValid && !TryResolveVerifiedBackendUserId(target, out userId)
                && (state == "Track" || state == "Harass"))
                _aedMinionEvidence.MarkIncomplete();
            _aedMinionEvidence.Observe(true, minionId.ToString(), zone,
                state, userId, tick, tickRate,
                $"minion-state:{minionId}:{tick}");
        }

        public void RecordMinionFact(AEDMinionFactKindV1 kind,
            NetworkId minionId, string occurrenceKey, long tick,
            PlayerRef user = default, PlayerRef relatedUser = default,
            string objectId = null, string effectKind = null,
            long attemptOrdinal = 0, bool accepted = false,
            double seconds = 0, string sourceEventId = null,
            Vector3 position = default)
        {
            if (!HasStateAuthority || !_telemetryMatchActive) return;
            string userId = null;
            string relatedUserId = null;
            if (user.IsValid && !TryResolveVerifiedBackendUserId(user, out userId)
                || relatedUser.IsValid
                    && !TryResolveVerifiedBackendUserId(relatedUser, out relatedUserId))
            {
                _aedMinionEvidence.MarkIncomplete();
                return;
            }
            _aedMinionEvidence.RecordFact(kind, minionId.ToString(),
                occurrenceKey, tick, userId, relatedUserId, objectId,
                effectKind, attemptOrdinal, accepted, seconds, sourceEventId,
                position.x, position.y, position.z);
        }

        public void RecordMinionDespawn(NetworkId minionId, long tick)
        {
            if (HasStateAuthority)
                _aedMinionEvidence.CensorMinion(minionId.ToString(),
                    "MINION_DESPAWN", tick);
        }

        public void MarkMinionEvidenceIncomplete() => _aedMinionEvidence.MarkIncomplete();

        public void RecordMinionRecoveredItem(NetworkId itemId,
            PlayerRef player, string occurrenceKey, long tick,
            string sourceEventId = null)
        {
            if (!HasStateAuthority || !_telemetryMatchActive) return;
            if (!TryResolveVerifiedBackendUserId(player, out var userId))
            {
                _aedMinionEvidence.MarkIncomplete();
                return;
            }
            _aedMinionEvidence.RecordFact(AEDMinionFactKindV1.ItemRecovered,
                "world-pickup", occurrenceKey, tick, userId,
                objectId: itemId.ToString(), accepted: true,
                sourceEventId: sourceEventId);
        }

        public string AEDv2RosterIdentity => HasStateAuthority ? CurrentRosterIdentity() : string.Empty;
        public IReadOnlyCollection<string> AEDv2BoundUserIds => _boundPlayers.Values
            .Select(userId => userId.ToString("D")).ToArray();

        private string CurrentRosterIdentity()
        {
            var users = new List<string>();
            foreach (var userId in _boundPlayers.Values) users.Add(userId.ToString("D"));
            users.Sort(StringComparer.Ordinal);
            var roster = new StringBuilder(MatchId.ToString("D")).Append('|');
            for (var i = 0; i < users.Count; i++)
            {
                if (i > 0) roster.Append(',');
                roster.Append(users[i]);
            }
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(roster.ToString())))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }
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
        public bool RequiresFreshHostBinding =>
            !HasBinding || !IsHostBinding;
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
            var matchState = NetworkMatchState.Instance;
            SampleAEDv2PlayerObservation(matchState);
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

        private void SampleAEDv2PlayerObservation(NetworkMatchState matchState)
        {
            if (!CanEmitProductionTelemetry()
                || matchState == null
                || matchState.Status != NetworkMatchStatus.Running)
                return;

            var runner = _bootstrap?.Runner;
            if (runner == null) return;

            double seconds = Math.Min((double)Time.unscaledDeltaTime, 1d);
            if (seconds <= 0) return;

            foreach (var player in runner.ActivePlayers)
            {
                int actorNumber = runner.GetPlayerActorId(player) ?? player.PlayerId;
                if (_disconnectedActors.Contains(actorNumber)
                    || !_boundPlayers.TryGetValue(actorNumber, out var userId))
                    continue;

                if (!runner.TryGetPlayerObject(player, out var playerObject)
                    || playerObject == null || !playerObject.IsValid
                    || !playerObject.TryGetComponent<LobbyPlayerState>(out var lobby)
                    || !lobby.IsGameplayPlayer
                    || !Guid.TryParse(lobby.BackendUserId.ToString(), out var verifiedId)
                    || verifiedId != userId
                    || !playerObject.TryGetComponent<NetworkPlayerLifeState>(out var life)
                    || life.Status != NetworkPlayerLifeStatus.Alive)
                    continue;

                _aedv2Evidence.RecordActiveObservation(userId.ToString("D"), seconds);
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
                [MatchDifficultySessionProperty] = requestedDifficulty.ToString().ToUpperInvariant()
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

        public async Task<bool> EndAsync(string reason)
        {
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

        public async void TrySubmitLocalIdentity(LobbyPlayerState knownState = null, bool force = false)
        {
            if (!HasBinding || _identityRequestInProgress || _bootstrap?.Runner == null
                || string.IsNullOrWhiteSpace(_bootstrap.CurrentSessionName))
            {
                return;
            }
            var runner = _bootstrap.Runner;
            var state = knownState;
            if (state == null && runner.TryGetPlayerObject(runner.LocalPlayer, out var playerObject))
            {
                playerObject.TryGetComponent(out state);
            }
            if (state == null || !state.Object.HasInputAuthority
                || (state.HasVerifiedBackendIdentity && !force)) return;

            var actorNumber = runner.GetPlayerActorId(runner.LocalPlayer) ?? runner.LocalPlayer.PlayerId;
            _identityRequestInProgress = true;
            var result = await _api.IssueJoinProofAsync(MatchId, _bootstrap.CurrentSessionName, actorNumber);
            _identityRequestInProgress = false;
            if (!IsSuccessful(result))
            {
                Debug.LogWarning($"[MatchAuthority] Join proof failed: {Describe(result)}.");
                return;
            }

            state.SubmitJoinProof(result.Data.data.proof, actorNumber);
            _bootstrap?.MarkReconnectProofSubmitted();
        }

        public async void BindPlayerFromProof(
            LobbyPlayerState playerState,
            int actorNumber,
            string proof)
        {
            if (!IsHostBinding
                || !HasBinding
                || playerState == null
                || playerState.Object == null
                || !playerState.Object.IsValid
                || !playerState.Object.HasStateAuthority)
            {
                return;
            }

            Guid bindingMatchId = MatchId;
            PlayerRef inputAuthority =
                playerState.Object.InputAuthority;

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

        public async void MarkPlayerDisconnected(int actorNumber)
        {
            if (!IsHostBinding || !HasBinding || actorNumber < 1) return;
            var result = await _api.DisconnectPlayerAsync(MatchId, actorNumber);
            if (!IsSuccessful(result))
            {
                Debug.LogWarning($"[MatchAuthority] Disconnect binding failed: {Describe(result)}");
                return;
            }

            _disconnectedActors.Add(actorNumber);
            if (_boundPlayers.TryGetValue(actorNumber, out var disconnectedUser))
            {
                var userId = disconnectedUser.ToString("D");
                _aedPursuitEvidence.CensorTarget(userId, "PLAYER_DISCONNECTED", CurrentAuthorityTick);
                _aedMinionEvidence.CensorTarget(userId, "PLAYER_DISCONNECTED", CurrentAuthorityTick);
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

            var result = await _api.StartAsync(MatchId);
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
            _aedv2Evidence.Clear();
            _aedObjectiveEvidence.Clear();
            _aedSurvivalEvidence.Clear();
            _aedToolNoiseEvidence.Clear();
            _aedPursuitEvidence.Clear();
            _aedMinionEvidence.Clear();
            LastFrozenAEDCurrentPressure = null;
            BackendAdaptiveInputSnapshotProvider.Current?.ClearForMatch(oldMatchId);
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
            var phaseStarted = _telemetry.MatchAdapter.EmitPhaseStarted(
                    "phase:core-collection:start:1",
                    occurredAtUtc,
                    "CORE_COLLECTION",
                    out _,
                    out var phaseFailure);
            if (!phaseStarted)
            {
                Debug.LogWarning($"[Telemetry] initial PHASE_STARTED was not buffered: {phaseFailure}.");
            }
            _aedv2Evidence.StartPhase(MatchId, CurrentRosterIdentity(),
                "CORE_COLLECTION", 1, occurredAtUtc);
            _aedPursuitEvidence.ResetMatch(MatchId);
            _aedPursuitEvidence.StartPhase(MatchId, 1, "CORE_COLLECTION",
                BuildAEDMetricPolicyContext("CORE_COLLECTION"));
            _aedMinionEvidence.ResetMatch(MatchId);
            _aedMinionEvidence.StartPhase(MatchId, 1, "CORE_COLLECTION",
                BuildAEDMetricPolicyContext("CORE_COLLECTION"));
            _aedObjectiveEvidence.StartPhase(MatchId, 1, "CORE_COLLECTION",
                windowStartedAtUtc: occurredAtUtc,
                policyContext: BuildAEDMetricPolicyContext("CORE_COLLECTION"));
            _aedSurvivalEvidence.StartPhase(MatchId, 1, "CORE_COLLECTION");
            _aedToolNoiseEvidence.StartPhase(MatchId, 1, "CORE_COLLECTION");
            if (!phaseStarted)
            {
                _aedv2Evidence.MarkIncomplete();
                _aedObjectiveEvidence.MarkIncomplete();
                _aedSurvivalEvidence.MarkIncomplete();
                _aedToolNoiseEvidence.MarkIncomplete();
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
            var item = transition.Item;
            var actor = transition.Actor;
            if (!HasStateAuthority || !actor.IsValid || item == null)
            {
                return;
            }

            if (_telemetry == null || !_telemetry.IsInitialized)
            {
                if (transition.State == NetworkItemState.Placed
                    && _telemetryMatchActive)
                {
                    _aedObjectiveEvidence.MarkIncomplete();
                    _aedv2Evidence.MarkIncomplete();
                }
                if (transition.State == NetworkItemState.Carried && _telemetryMatchActive)
                    _aedMinionEvidence.MarkIncomplete();

                return;
            }

            if (!TryResolveBackendUser(actor, out var userId))
            {
                if (HasStateAuthority
                    && transition.State == NetworkItemState.Placed
                    && _telemetryMatchActive)
                {
                    _aedObjectiveEvidence.MarkIncomplete();
                    _aedv2Evidence.MarkIncomplete();
                }
                if (transition.State == NetworkItemState.Carried && _telemetryMatchActive)
                    _aedMinionEvidence.MarkIncomplete();

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

            var occurrenceKey = $"{coreId}:{transitionName}:{transition.Ordinal}";
        if (transition.State == NetworkItemState.Placed
            && _aedObjectiveEvidence.HasRecordedCorePlacementOccurrence(
                occurrenceKey))
        {
            return;
        }

            var accepted = _telemetry.ObjectiveAdapter.EmitCoreTransition(
                occurrenceKey,
                DateTime.UtcNow,
                eventType,
                userId,
                coreId,
                out var emittedEvent,
                out _,
                Snapshot(transition.Position));

            if (transition.State == NetworkItemState.Carried
                && item.Object != null && item.Object.IsValid)
                RecordMinionRecoveredItem(item.Object.Id, actor,
                    $"core-player-pickup:{occurrenceKey}",
                    _bootstrap?.Runner != null ? _bootstrap.Runner.Tick.Raw : 0L,
                    accepted ? emittedEvent?.Id.ToString("D") : null);

            if (transition.State == NetworkItemState.Placed && accepted
                && emittedEvent != null
                && emittedEvent.EventType == TelemetryEventTypes.CorePlaced
                && emittedEvent.MatchId == MatchId
                && emittedEvent.UserId == userId
                && item.Object != null && item.Object.IsValid)
            {
                var runner = _bootstrap?.Runner;
                _aedObjectiveEvidence.RecordCorePlaced(
                    true, emittedEvent.Id, coreId,
                    item.PlacedSectorId.ToString(), item.PlacementSlot,
                    transition.Ordinal, occurrenceKey, userId.ToString("D"),
                    runner != null ? runner.Tick.Raw : 0L,
                    "FusionStateAuthority", _currentTelemetryPhase);
            }
            else if (transition.State == NetworkItemState.Placed && accepted)
            {
                _aedObjectiveEvidence.MarkIncomplete();
            }
        else if (transition.State == NetworkItemState.Placed && !accepted)
        {
            _aedObjectiveEvidence.MarkIncomplete();
            _aedv2Evidence.MarkIncomplete();
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
            FreezeAEDv1Evidence(DateTime.UtcNow, objectivePhaseIncomplete: true);
            if (!CanEmitProductionTelemetry() || _matchEndEmitted) return false;

            _pendingMatchEnd = true;
            _pendingMatchEndOccurrenceKey = occurrenceKey;
            _pendingMatchEndOutcome = outcome;
            _pendingMatchEndSurvivorCount = survivorCount;
            _pendingMatchEndReasonCode = reasonCode;
            return TryEmitPendingMatchEnd();
        }

        private void FreezeAEDv1Evidence(DateTime windowEndedAtUtc,
            bool objectivePhaseIncomplete = false)
        {
            if (objectivePhaseIncomplete) _aedObjectiveEvidence.MarkIncomplete();
            _aedPursuitEvidence.CensorActive("PHASE_END", CurrentAuthorityTick);
            _aedMinionEvidence.CensorAll("PHASE_END", CurrentAuthorityTick);
            _aedObjectiveEvidence.Freeze(windowEndedAtUtc);
            _aedSurvivalEvidence.Freeze();
            _aedToolNoiseEvidence.Freeze();
            _aedPursuitEvidence.Freeze();
            _aedMinionEvidence.Freeze();

            if (HasStateAuthority)
            {
                var pressureTickRate = _bootstrap?.Runner != null
                    ? (double)_bootstrap.Runner.TickRate
                    : 0d;

                LastFrozenAEDCurrentPressure =
                    AEDCurrentMatchPressureV1.Project(
                        _aedv2Evidence.LastFrozen,
                        _aedPursuitEvidence.LastFrozen,
                        _aedMinionEvidence.LastFrozen,
                        _aedSurvivalEvidence.LastFrozen,
                        CurrentAuthorityTick,
                        pressureTickRate);
            }
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

        private async void TrySubmitPendingMatchResult()
        {
            if (!_pendingBackendMatchResult
                || _backendEndRequestInProgress
                || !HasBinding
                || !IsHostBinding)
            {
                return;
            }

            if (!TryBuildMatchResultRequest(out var request))
            {
                _nextBackendMatchResultRetryAt = Time.unscaledTime + 1f;
                return;
            }

            _backendEndRequestInProgress = true;
            var result = await _api.SubmitResultAsync(MatchId, request);
            _backendEndRequestInProgress = false;

            if (!IsSuccessful(result))
            {
                bool durationTooEarly =
                    result != null
                    && result.FailureKind ==
                        EchoProtocol.Api.ApiFailureKind.Business
                    && string.Equals(
                        result.ErrorCode,
                        "MATCH_RESULT_INVALID_DURATION",
                        StringComparison.Ordinal)
                    && _backendMatchStartedAtRealtime >= 0f
                    && Time.realtimeSinceStartup
                        - _backendMatchStartedAtRealtime
                        < MinimumBackendMatchResultAgeSeconds;

                if (durationTooEarly)
                {
                    _nextBackendMatchResultRetryAt =
                        Time.unscaledTime + 1f;

                    Debug.LogWarning(
                        "[MatchAuthority] Match result is waiting for the backend minimum duration.");

                    return;
                }

                if (result != null
                    && result.FailureKind ==
                        EchoProtocol.Api.ApiFailureKind.Business)
                {
                    _pendingBackendMatchResult = false;

                    Debug.LogError(
                        $"[MatchAuthority] Match result rejected and will not be retried: {Describe(result)}");

                    return;
                }

                _nextBackendMatchResultRetryAt =
                    Time.unscaledTime + 2f;

                Debug.LogWarning(
                    $"[MatchAuthority] Match result submission failed; retrying safely: {Describe(result)}");

                return;
            }

            _pendingBackendMatchResult = false;
            IsHostBinding = false;
            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,
                $"[MatchAuthority] Backend confirmed match result. Match={MatchId:D}, " +
                $"RewardStatus={result.Data.data.rewardStatus}, Replay={result.Data.data.isReplay}.");
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
            Vector3 position,
            uint transitionOrdinal)
        {
            if (!TryResolveBackendUser(player, out var userId))
            {
                _aedv2Evidence.MarkIncomplete();
                _aedSurvivalEvidence.MarkIncomplete();
                return false;
            }

            if (!CanEmitProductionTelemetry())
            {
                _aedv2Evidence.MarkIncomplete();
                _aedSurvivalEvidence.Record(AEDSurvivalOutcomeKindV1.Downed,
                    userId.ToString("D"), null, occurrenceKey, transitionOrdinal,
                    monsterType, false, null, "FusionStateAuthority");
                return false;
            }

            var reasonCode = monsterType == "LISTENER" ? "LISTENER_ATTACK" : "STALKER_ATTACK";
            var accepted = _telemetry.PlayerAdapter.EmitPlayerDowned(
                occurrenceKey,
                DateTime.UtcNow,
                userId,
                _currentTelemetryPhase,
                monsterType,
                reasonCode,
                out var telemetryEvent,
                out _,
                downCount,
                Snapshot(position));
            _aedSurvivalEvidence.Record(AEDSurvivalOutcomeKindV1.Downed,
                userId.ToString("D"), null, occurrenceKey, transitionOrdinal,
                monsterType, false,
                accepted ? telemetryEvent?.Id.ToString("D") : null,
                "FusionStateAuthority");
              if (accepted) _aedv2Evidence.RecordAcceptedDown(occurrenceKey, userId.ToString("D"));
              else
              {
                  _aedv2Evidence.MarkIncomplete();
                  _aedSurvivalEvidence.MarkIncomplete();
              }
            return accepted;
        }

        public bool RecordPlayerRevived(
            PlayerRef revivedPlayer,
            PlayerRef reviver,
            string occurrenceKey,
            int reviveCount,
            bool usedFirstAidKit,
            uint transitionOrdinal)
        {
            if (!TryResolveBackendUser(revivedPlayer, out var revivedUserId))
            {
                _aedv2Evidence.MarkIncomplete();
                _aedSurvivalEvidence.MarkIncomplete();
                if (usedFirstAidKit) _aedToolNoiseEvidence.MarkIncomplete();
                return false;
            }

            var hasReviver = TryResolveBackendUser(reviver, out var reviverUserId);
            if (!CanEmitProductionTelemetry() || !hasReviver)
            {
                _aedv2Evidence.MarkIncomplete();
                _aedSurvivalEvidence.Record(AEDSurvivalOutcomeKindV1.Revived,
                    revivedUserId.ToString("D"),
                    hasReviver ? reviverUserId.ToString("D") : null,
                    occurrenceKey, transitionOrdinal,
                    "REVIVE_COMPLETED", false, null, "FusionStateAuthority");
                if (usedFirstAidKit && hasReviver)
                    _aedToolNoiseEvidence.RecordToolEffect("FIRST_AID_KIT",
                        reviverUserId.ToString("D"), revivedUserId.ToString("D"),
                        occurrenceKey, AEDToolEffectOutcomeV1.ResolvedSuccess,
                        null, "FusionStateAuthority");
                else if (usedFirstAidKit)
                    _aedToolNoiseEvidence.MarkIncomplete();
                return false;
            }

            var accepted = _telemetry.PlayerAdapter.EmitPlayerRevived(
                occurrenceKey,
                DateTime.UtcNow,
                revivedUserId,
                reviverUserId,
                _currentTelemetryPhase,
                out var telemetryEvent,
                out _,
                reviveCount,
                usedFirstAidKit);
            _aedSurvivalEvidence.Record(AEDSurvivalOutcomeKindV1.Revived,
                revivedUserId.ToString("D"), reviverUserId.ToString("D"),
                occurrenceKey, transitionOrdinal, "REVIVE_COMPLETED", false,
                accepted ? telemetryEvent?.Id.ToString("D") : null,
                "FusionStateAuthority");
            if (usedFirstAidKit)
                _aedToolNoiseEvidence.RecordToolEffect("FIRST_AID_KIT",
                    reviverUserId.ToString("D"), revivedUserId.ToString("D"),
                    occurrenceKey, AEDToolEffectOutcomeV1.ResolvedSuccess,
                    accepted ? telemetryEvent?.Id.ToString("D") : null,
                    "FusionStateAuthority");
              if (accepted) _aedv2Evidence.RecordAcceptedRevive(occurrenceKey, revivedUserId.ToString("D"));
              else
              {
                  _aedv2Evidence.MarkIncomplete();
                  _aedSurvivalEvidence.MarkIncomplete();
                  if (usedFirstAidKit)
                      _aedToolNoiseEvidence.MarkIncomplete();
              }
            return accepted;
        }

        public bool RecordPlayerEliminated(
            PlayerRef player,
            string occurrenceKey,
            int reviveCount,
            NetworkPlayerLifeTransitionCause cause,
            bool directFromHit,
            uint transitionOrdinal,
            string reason)
        {
            if (!TryResolveBackendUser(player, out var userId))
            {
                _aedv2Evidence.MarkIncomplete();
                _aedSurvivalEvidence.MarkIncomplete();
                return false;
            }

            var kind = directFromHit && cause == NetworkPlayerLifeTransitionCause.ReviveLimit
                ? AEDSurvivalOutcomeKindV1.DirectElimination
                : cause == NetworkPlayerLifeTransitionCause.Bleedout && reason == "TEAM_DOWNED"
                    ? AEDSurvivalOutcomeKindV1.TeamElimination
                    : cause == NetworkPlayerLifeTransitionCause.Bleedout
                        ? AEDSurvivalOutcomeKindV1.BleedoutElimination
                        : AEDSurvivalOutcomeKindV1.OtherElimination;

            if (!CanEmitProductionTelemetry())
            {
                _aedv2Evidence.MarkIncomplete();
                _aedSurvivalEvidence.Record(kind, userId.ToString("D"), null,
                    occurrenceKey, transitionOrdinal, cause.ToString(),
                    directFromHit, null, "FusionStateAuthority");
                return false;
            }

            var accepted = _telemetry.PlayerAdapter.EmitPlayerEliminated(
                occurrenceKey,
                DateTime.UtcNow,
                userId,
                _currentTelemetryPhase,
                out var telemetryEvent,
                out _,
                reviveCount);
            _aedSurvivalEvidence.Record(kind, userId.ToString("D"), null,
                occurrenceKey, transitionOrdinal, cause.ToString(), directFromHit,
                accepted ? telemetryEvent?.Id.ToString("D") : null,
                "FusionStateAuthority");
              if (accepted) _aedv2Evidence.RecordAcceptedElimination(occurrenceKey, userId.ToString("D"));
              else
              {
                  _aedv2Evidence.MarkIncomplete();
                  _aedSurvivalEvidence.MarkIncomplete();
              }
            return accepted;
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
            var nowUtc = DateTime.UtcNow;
            nowUtc = new DateTime(nowUtc.Ticks - nowUtc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
            var accepted = CanEmitProductionTelemetry()
                && _telemetry.MatchAdapter.EmitPhaseCompleted(
                occurrenceKey,
                nowUtc,
                phase,
                out _,
                out _,
                null,
                reasonCode);
            if (!accepted)
            {
                _aedv2Evidence.MarkIncomplete();
                _aedObjectiveEvidence.MarkIncomplete();
                _aedSurvivalEvidence.MarkIncomplete();
                _aedToolNoiseEvidence.MarkIncomplete();
            }
            _aedv2Evidence.Freeze(phase, _boundPlayers.Count, CurrentRosterIdentity(), nowUtc);
            FreezeAEDv1Evidence(nowUtc);
            var frozen = _aedv2Evidence.LastFrozen;
            var players = _aedv2Evidence.LastFrozenPlayerEvidence;
            var safety = AEDv2RosterSafety.FromEvidence(
                AEDv2BoundUserIds, players);

            if (frozen != null)
            {
                var observedSeconds = string.Join(",", players.Values.Select(
                    p => p.ActiveObservedSeconds.ToString("F1")));
                Debug.Log(
                    $"[AED_P0] phase={phase} " +
                    $"complete={frozen.TelemetryCompleteness} " +
                    $"fingerprintValid={frozen.HasValidFingerprint()} " +
                    $"rosterValid={frozen.RosterIdentity == CurrentRosterIdentity()} " +
                    $"allObserved={safety.AllPlayersObserved} " +
                    $"allowPressure={safety.AllowPressure} " +
                    $"observedSeconds={observedSeconds} " +
                    $"reasons={string.Join(",", frozen.ReasonCodes)}");
            }
            return accepted;
        }

        public bool RecordPhaseStarted(string occurrenceKey, string phase, string reasonCode)
        {
            var nowUtc = DateTime.UtcNow;
            nowUtc = new DateTime(nowUtc.Ticks - nowUtc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
            var emitted = CanEmitProductionTelemetry()
                && _telemetry.MatchAdapter.EmitPhaseStarted(
                occurrenceKey,
                nowUtc,
                phase,
                out _,
                out _,
                reasonCode);
            _currentTelemetryPhase = phase;
            var phaseOrdinal = (_aedv2Evidence.LastFrozen?.PhaseOrdinal ?? 0) + 1;
            _aedv2Evidence.StartPhase(MatchId, CurrentRosterIdentity(),
                phase, phaseOrdinal, nowUtc);
            _aedObjectiveEvidence.StartPhase(MatchId, phaseOrdinal, phase,
                windowStartedAtUtc: nowUtc,
                policyContext: BuildAEDMetricPolicyContext(phase));
            _aedSurvivalEvidence.StartPhase(MatchId, phaseOrdinal, phase);
            _aedToolNoiseEvidence.StartPhase(MatchId, phaseOrdinal, phase);
            _aedPursuitEvidence.StartPhase(MatchId, phaseOrdinal, phase,
                BuildAEDMetricPolicyContext(phase));
            _aedMinionEvidence.StartPhase(MatchId, phaseOrdinal, phase,
                BuildAEDMetricPolicyContext(phase));
            if (!emitted)
            {
                _aedv2Evidence.MarkIncomplete();
                _aedObjectiveEvidence.MarkIncomplete();
                _aedSurvivalEvidence.MarkIncomplete();
                _aedToolNoiseEvidence.MarkIncomplete();
            }
            return emitted;
        }

        public void RegisterCoreObjectiveSlots(
            IReadOnlyCollection<NetworkObject> sectorBoxObjects,
            int expectedSectorCount)
        {
            if (!HasStateAuthority || sectorBoxObjects == null
                || sectorBoxObjects.Count == 0 || expectedSectorCount <= 0
                || MatchId == Guid.Empty
                || _currentTelemetryPhase != "CORE_COLLECTION")
                return;

            var phaseOrdinal = (_aedv2Evidence.LastFrozen?.PhaseOrdinal ?? 0) + 1;
            _aedObjectiveEvidence.StartPhase(MatchId, phaseOrdinal,
                _currentTelemetryPhase);
            foreach (var boxObject in sectorBoxObjects)
            {
                if (boxObject == null || !boxObject.IsValid
                    || !boxObject.TryGetComponent<NetworkSectorBox>(out var box)
                    || !box.MatchStateId.IsValid || box.RequiredCoreCount < 0
                    || !_aedObjectiveEvidence.RegisterCorePlacementSlots(
                        boxObject.Id.ToString(), box.RequiredCoreCount))
                {
                    _aedObjectiveEvidence.MarkIncomplete();
                    return;
                }
            }
            _aedObjectiveEvidence.CompleteCorePlacementCoverage(expectedSectorCount);
        }

        private AEDMetricPolicyContextV1 BuildAEDMetricPolicyContext(string phase)
        {
            ScenarioConfigRuntimeRegistry.TryGetAppliedConfig(MatchId, out var config);
            var hasAppliedPlan = AEDv2Authority.TryGetApplied(MatchId,
                out var plan, out var revision);
            var comparisonContext = config == null ? null
                : $"{config.MapId}|{phase}|{requestedDifficulty}|{requestedScenarioResolutionMode}|{_boundPlayers.Count}";
            return new AEDMetricPolicyContextV1(
                requestedDifficulty.ToString(), requestedScenarioResolutionMode.ToString(),
                config?.ConfigSource.ToString(), config?.ScenarioConfigVersion,
                config?.PolicyVersion, hasAppliedPlan ? revision : (long?)null,
                hasAppliedPlan ? plan.Fingerprint() : null, comparisonContext);
        }

        public bool RecordPuzzleCompleted(string occurrenceKey)
        {
            var accepted = CanEmitProductionTelemetry()
                && _telemetry.ObjectiveAdapter.EmitPuzzleCompleted(
                    occurrenceKey,
                    DateTime.UtcNow,
                    out _,
                    out _);
            if (accepted) _aedv2Evidence.RecordAcceptedObjective(occurrenceKey);
            else _aedv2Evidence.MarkIncomplete();
            return accepted;
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
            // CoreStabilizer is gameplay-only in Telemetry v1.1.
            if (toolType == "CORE_STABILIZER")
            {
                if (TryResolveBackendUser(player, out var stabilizerUserId))
                    _aedToolNoiseEvidence.RecordToolAction(toolType,
                        stabilizerUserId.ToString("D"), occurrenceKey,
                        AEDEvidenceSourceCategoryV1.GameplayOnlyAccepted,
                        null, "FusionStateAuthority");
                else
                    _aedToolNoiseEvidence.MarkIncomplete();
                return false;
            }

            if (!TryResolveBackendUser(player, out var userId))
            {
                _aedv2Evidence.MarkIncomplete();
                _aedToolNoiseEvidence.MarkIncomplete();
                return false;
            }

            if (!CanEmitProductionTelemetry())
            {
                _aedv2Evidence.MarkIncomplete();
                _aedToolNoiseEvidence.RecordToolAction(toolType,
                    userId.ToString("D"), occurrenceKey,
                    AEDEvidenceSourceCategoryV1.CanonicalEmissionRejected,
                    null, "FusionStateAuthority");
                return false;
            }

            var accepted = _telemetry.PlayerAdapter.EmitTeamToolUsed(
                occurrenceKey,
                DateTime.UtcNow,
                userId,
                _currentTelemetryPhase,
                toolType,
                out var telemetryEvent,
                out _,
                targetId);
            _aedToolNoiseEvidence.RecordToolAction(toolType,
                userId.ToString("D"), occurrenceKey,
                accepted
                    ? AEDEvidenceSourceCategoryV1.CanonicalTelemetryAccepted
                    : AEDEvidenceSourceCategoryV1.CanonicalEmissionRejected,
                accepted ? telemetryEvent?.Id.ToString("D") : null,
                "FusionStateAuthority");
            if (accepted) _aedv2Evidence.RecordAcceptedTeamTool(occurrenceKey, userId.ToString("D"));
            else _aedv2Evidence.MarkIncomplete();
            return accepted;
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
            if (!NoiseTelemetryAdapter.SupportsNoiseType(noiseType))
            {
                if (TryResolveBackendUser(player, out var gameplayUserId))
                    _aedToolNoiseEvidence.RecordNoise(noiseType,
                        gameplayUserId.ToString("D"), noiseEventId,
                        AEDEvidenceSourceCategoryV1.GameplayOnlyAccepted,
                        null, "FusionStateAuthority");
                else
                    _aedToolNoiseEvidence.MarkIncomplete();
                return false;
            }

            if (!TryResolveBackendUser(player, out var userId))
            {
                _aedv2Evidence.MarkIncomplete();
                _aedToolNoiseEvidence.MarkIncomplete();
                return false;
            }

            if (!CanEmitProductionTelemetry())
            {
                _aedv2Evidence.MarkIncomplete();
                _aedToolNoiseEvidence.RecordNoise(noiseType,
                    userId.ToString("D"), noiseEventId,
                    AEDEvidenceSourceCategoryV1.CanonicalEmissionRejected,
                    null, "FusionStateAuthority");
                return false;
            }

            try
            {
                var accepted = _telemetry.NoiseAdapter.EmitAcceptedRuntimeNoise(
                    noiseEventId,
                    emittedAtUtc,
                    userId,
                    _currentTelemetryPhase,
                    noiseType,
                    loudness,
                    Snapshot(position),
                    out var telemetryEvent,
                    out _,
                    hearingRadius);
                _aedToolNoiseEvidence.RecordNoise(noiseType,
                    userId.ToString("D"), noiseEventId,
                    accepted
                        ? AEDEvidenceSourceCategoryV1.CanonicalTelemetryAccepted
                        : AEDEvidenceSourceCategoryV1.CanonicalEmissionRejected,
                    accepted ? telemetryEvent?.Id.ToString("D") : null,
                    "FusionStateAuthority");
                if (accepted) _aedv2Evidence.RecordAcceptedNoise(noiseEventId, userId.ToString("D"));
                else _aedv2Evidence.MarkIncomplete();
                return accepted;
            }
            catch (InvalidOperationException exception)
                when (string.Equals(
                    exception.Message,
                    "Telemetry occurrence identity capacity is exhausted.",
                    StringComparison.Ordinal))
            {
                _aedv2Evidence.MarkIncomplete();
                _aedToolNoiseEvidence.RecordNoise(noiseType,
                    userId.ToString("D"), noiseEventId,
                    AEDEvidenceSourceCategoryV1.CanonicalEmissionRejected,
                    null, "FusionStateAuthority");
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
                _aedv2Evidence.MarkIncomplete();
                _aedToolNoiseEvidence.RecordNoise(noiseType,
                    userId.ToString("D"), noiseEventId,
                    AEDEvidenceSourceCategoryV1.CanonicalEmissionRejected,
                    null, "FusionStateAuthority");
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
