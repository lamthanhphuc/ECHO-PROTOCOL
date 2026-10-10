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
        public AEDv2CurrentMatchEvidence LastFrozenAEDv2Evidence => _aedv2Evidence.LastFrozen;
        public IReadOnlyDictionary<string, AEDv2PlayerPhaseEvidence> AEDv2PlayerEvidence => _aedv2Evidence.PlayerEvidence;
        public IReadOnlyDictionary<string, AEDv2PlayerPhaseEvidence> LastFrozenAEDv2PlayerEvidence => _aedv2Evidence.LastFrozenPlayerEvidence;
        public void MarkAEDv2EvidenceIncomplete()
        {
            _aedv2Evidence.MarkIncomplete();
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
            if (!_telemetry.MatchAdapter.EmitPhaseStarted(
                    "phase:core-collection:start:1",
                    occurredAtUtc,
                    "CORE_COLLECTION",
                    out _,
                    out var phaseFailure))
            {
                Debug.LogWarning($"[Telemetry] initial PHASE_STARTED was not buffered: {phaseFailure}.");
            }
            else
            {
                _aedv2Evidence.StartPhase(MatchId, CurrentRosterIdentity(),
                    "CORE_COLLECTION", 1, occurredAtUtc);
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
            if (!HasStateAuthority || !actor.IsValid || item == null
                || _telemetry == null || !_telemetry.IsInitialized)
            {
                return;
            }

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
            Vector3 position)
        {
            if (!CanEmitProductionTelemetry() || !TryResolveBackendUser(player, out var userId))
            {
                _aedv2Evidence.MarkIncomplete();
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
                out _,
                out _,
                downCount,
                Snapshot(position));
            if (accepted) _aedv2Evidence.RecordAcceptedDown(occurrenceKey, userId.ToString("D"));
            else _aedv2Evidence.MarkIncomplete();
            return accepted;
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
                _aedv2Evidence.MarkIncomplete();
                return false;
            }

            var accepted = _telemetry.PlayerAdapter.EmitPlayerRevived(
                occurrenceKey,
                DateTime.UtcNow,
                revivedUserId,
                reviverUserId,
                _currentTelemetryPhase,
                out _,
                out _,
                reviveCount,
                usedFirstAidKit);
            if (accepted) _aedv2Evidence.RecordAcceptedRevive(occurrenceKey, revivedUserId.ToString("D"));
            else _aedv2Evidence.MarkIncomplete();
            return accepted;
        }

        public bool RecordPlayerEliminated(
            PlayerRef player,
            string occurrenceKey,
            int reviveCount)
        {
            if (!CanEmitProductionTelemetry() || !TryResolveBackendUser(player, out var userId))
            {
                _aedv2Evidence.MarkIncomplete();
                return false;
            }

            var accepted = _telemetry.PlayerAdapter.EmitPlayerEliminated(
                occurrenceKey,
                DateTime.UtcNow,
                userId,
                _currentTelemetryPhase,
                out _,
                out _,
                reviveCount);
            if (accepted) _aedv2Evidence.RecordAcceptedElimination(occurrenceKey, userId.ToString("D"));
            else _aedv2Evidence.MarkIncomplete();
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
            if (!CanEmitProductionTelemetry())
            {
                _aedv2Evidence.MarkIncomplete();
                return false;
            }
            var nowUtc = DateTime.UtcNow;
            nowUtc = new DateTime(nowUtc.Ticks - nowUtc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
            var accepted = _telemetry.MatchAdapter.EmitPhaseCompleted(
                occurrenceKey,
                nowUtc,
                phase,
                out _,
                out _,
                null,
                reasonCode);
            if (!accepted) _aedv2Evidence.MarkIncomplete();
            _aedv2Evidence.Freeze(phase, _boundPlayers.Count, CurrentRosterIdentity(), nowUtc);
            return accepted;
        }

        public bool RecordPhaseStarted(string occurrenceKey, string phase, string reasonCode)
        {
            if (!CanEmitProductionTelemetry())
            {
                _aedv2Evidence.MarkIncomplete();
                return false;
            }
            var nowUtc = DateTime.UtcNow;
            nowUtc = new DateTime(nowUtc.Ticks - nowUtc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
            var emitted = _telemetry.MatchAdapter.EmitPhaseStarted(
                occurrenceKey,
                nowUtc,
                phase,
                out _,
                out _,
                reasonCode);
            if (emitted) _currentTelemetryPhase = phase;
            if (emitted) _aedv2Evidence.StartPhase(MatchId, CurrentRosterIdentity(),
                phase, (_aedv2Evidence.LastFrozen?.PhaseOrdinal ?? 0) + 1, nowUtc);
            else _aedv2Evidence.MarkIncomplete();
            return emitted;
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
                return false;

            if (!CanEmitProductionTelemetry() || !TryResolveBackendUser(player, out var userId))
            {
                _aedv2Evidence.MarkIncomplete();
                return false;
            }

            var accepted = _telemetry.PlayerAdapter.EmitTeamToolUsed(
                occurrenceKey,
                DateTime.UtcNow,
                userId,
                _currentTelemetryPhase,
                toolType,
                out _,
                out _,
                targetId);
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
                return false;

            if (!CanEmitProductionTelemetry() || !TryResolveBackendUser(player, out var userId))
            {
                _aedv2Evidence.MarkIncomplete();
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
                    out _,
                    out _,
                    hearingRadius);
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
