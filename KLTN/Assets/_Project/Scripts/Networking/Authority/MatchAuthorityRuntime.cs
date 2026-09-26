using System;
using EchoProtocol.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Auth;
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
        private const float LeaseRenewIntervalSeconds = 15f;

        private static MatchAuthorityRuntime _instance;
        private MatchAuthorityApiService _api;
        private NetworkBootstrap _bootstrap;
        private float _nextLeaseRenewal;
        private bool _leaseRequestInProgress;
        private bool _identityRequestInProgress;
        private bool _backendEndRequestInProgress;
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
        [SerializeField] private string experimentCondition;
        [SerializeField]
        private string experimentProtocolVersion;
        [SerializeField]
        private string telemetryMapContentVersion =
            "M2-MAP-1";
        public static MatchAuthorityRuntime Instance => _instance;
        public Guid MatchId { get; private set; }
        public ScenarioResolutionMode RequestedScenarioResolutionMode => requestedScenarioResolutionMode;
        public string ExperimentCondition => experimentCondition ?? string.Empty;
        public string ExperimentProtocolVersion =>
            experimentProtocolVersion ?? string.Empty;
        public bool IsHostBinding { get; private set; }
        public bool HasBinding => MatchId != Guid.Empty;
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
            if (_pendingMatchEnd
                && Time.unscaledTime >= _nextMatchEndRetryAt)
            {
                TryEmitPendingMatchEnd();
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

            MatchId = matchId;
            IsHostBinding = true;
            _nextLeaseRenewal = Time.unscaledTime + LeaseRenewIntervalSeconds;
            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,
                $"[MatchAuthority] Host binding created. Match={MatchId:D}, Session='{sessionName}'.");
            return true;
        }

        public Dictionary<string, SessionProperty> BuildHostSessionProperties() =>
            new Dictionary<string, SessionProperty>
            {
                [MatchIdSessionProperty] = MatchId.ToString("D"),
                [ScenarioResolutionModeSessionProperty] =
                    requestedScenarioResolutionMode == ScenarioResolutionMode.Adaptive
                        ? "ADAPTIVE"
                        : "FIXED"
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
                if (!Debug.isDebugBuild)
                {
                    Debug.LogError("[MatchAuthority] Session has no backend match binding.");
                    return false;
                }
                matchId = Guid.NewGuid();
                Debug.LogWarning($"[MatchAuthority] Session has no backend match binding. Generated dev fallback MatchId={matchId:D}.");
            }

            MatchId = matchId;
            IsHostBinding = runner.IsServer;
            requestedScenarioResolutionMode = ReadScenarioResolutionMode(runner);
            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,

                $"[MatchAuthority] Fusion session attached. Match={MatchId:D}, " +
                $"Host={IsHostBinding}, Mode={requestedScenarioResolutionMode}, Session='{runner.SessionInfo.Name}'.");
            TrySubmitLocalIdentity();
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
            LobbyPlayerState playerState, int actorNumber, string proof)
        {
            if (!IsHostBinding || !HasBinding || playerState == null
                || !playerState.Object.HasStateAuthority)
            {
                return;
            }

            var result = await _api.BindPlayerAsync(MatchId, actorNumber, proof);
            if (!IsSuccessful(result))
            {
                Debug.LogWarning($"[MatchAuthority] Player bind rejected: {Describe(result)}.");
                if (Debug.isDebugBuild)
                    playerState.ApplyVerifiedBackendIdentity(Guid.NewGuid().ToString("D"));
                else if (_bootstrap?.Runner != null)
                    _bootstrap.Runner.Disconnect(playerState.Object.InputAuthority);
                return;
            }

            playerState.ApplyVerifiedBackendIdentity(result.Data.data.userId);
            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,

                $"[MatchAuthority] Player verified. Actor={actorNumber}, " +
                $"User={result.Data.data.userId}, Match={MatchId:D}.");
        }

        public async void MarkPlayerDisconnected(int actorNumber)
        {
            if (!IsHostBinding || !HasBinding || actorNumber < 1) return;
            var result = await _api.DisconnectPlayerAsync(MatchId, actorNumber);
            if (!IsSuccessful(result))
            {
                Debug.LogWarning($"[MatchAuthority] Disconnect binding failed: {Describe(result)}");
            }
        }

        public async Task<(bool Accepted, string Error)> StartMatchAsync()
        {
            if (!IsHostBinding || !HasBinding)
            {
                const string error = "Backend Host binding is not available.";
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning($"[MatchAuthority] {error} Dev bypass: starting match anyway.");
                    return (true, string.Empty);
                }
                return (false, error);
            }

            var result = await _api.StartAsync(MatchId);
            var success = IsSuccessful(result);
            if (success)
            {
                RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,
                $"[MatchAuthority] Backend confirmed match start. Match={MatchId:D}.");
                return (true, string.Empty);
            }
            var failure = Describe(result);
            if (Debug.isDebugBuild)
            {
                Debug.LogWarning($"[MatchAuthority] Backend rejected match start: {failure}. Dev bypass: starting match anyway.");
                return (true, string.Empty);
            }
            return (false, failure);
        }

        public void ResetBinding()
        {
            var oldMatchId = MatchId;
            MatchId = Guid.Empty;
            IsHostBinding = false;
            requestedScenarioResolutionMode = ScenarioResolutionMode.Fixed;
            experimentCondition = string.Empty;
            experimentProtocolVersion =
                string.Empty;
            _leaseRequestInProgress = false;
            _identityRequestInProgress = false;
            _backendEndRequestInProgress = false;
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
                CompleteBackendMatch(_pendingMatchEndReasonCode);
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

        private async void CompleteBackendMatch(string reasonCode)
        {
            if (_backendEndRequestInProgress || !HasBinding || !IsHostBinding) return;

            _backendEndRequestInProgress = true;
            var result = await _api.EndAsync(MatchId, reasonCode);
            _backendEndRequestInProgress = false;
            if (!IsSuccessful(result))
            {
                Debug.LogWarning($"[MatchAuthority] Gameplay completion was not persisted: {Describe(result)}");
                return;
            }

            IsHostBinding = false;
            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,
                $"[MatchAuthority] Backend confirmed match end. Match={MatchId:D}, Reason={reasonCode}.");
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
