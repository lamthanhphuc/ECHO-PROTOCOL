using System;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public enum NetworkMatchPhase
    {
        CoreObjective = 0,
        Puzzle = 1,
        SecurityHold = 2,
        FinalHunt = 3,
        Escape = 4,
        MatchEnded = 5,
    }

    public enum NetworkMatchStatus
    {
        Running = 0,
        Ended = 1,
    }

    public enum NetworkMatchResult
    {
        None = 0,
        Win = 1,
        Lose = 2,
    }

    public enum NetworkMatchEndReason
    {
        None = 0,
        PlayerEscaped = 1,
        EscapeTimeout = 2,
        MatchTimeout = 3,
        AllPlayersEliminated = 4,
    }

    public static class NetworkMatchStateRules
    {
        public static bool CanAdvance(
            NetworkMatchStatus status,
            NetworkMatchPhase current,
            NetworkMatchPhase expected,
            NetworkMatchPhase next) =>
            status == NetworkMatchStatus.Running
            && current == expected
            && next != current
            && next != NetworkMatchPhase.MatchEnded;

        public static bool CanEnd(NetworkMatchStatus status, NetworkMatchResult result) =>
            status == NetworkMatchStatus.Running
            && result != NetworkMatchResult.None;

        public static bool IsObjectiveMutationAllowed(
            NetworkMatchStatus status,
            NetworkMatchPhase current,
            NetworkMatchPhase required) =>
            status == NetworkMatchStatus.Running && current == required;
    }

    /// <summary>Host-owned match FSM. Objective state stays in its authoritative source object.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkMatchState : NetworkBehaviour
    {
        public static event Action<NetworkMatchState> StateChanged;

        [SerializeField, Min(1f)] private float _escapeDurationSeconds = 45f;
        [SerializeField, Min(1f)] private float _matchDurationSeconds = 900f;
        [SerializeField, Min(0.1f)] private float _returnToLobbyDelaySeconds = 4f;

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkMatchPhase CurrentPhase { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkMatchStatus Status { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkMatchResult Result { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkMatchEndReason EndReason { get; private set; }

        [Networked] public NetworkId ObjectiveSourceId { get; private set; }
        [Networked] public NetworkId EscapeDoorId { get; private set; }
        [Networked] public PlayerRef LastActor { get; private set; }
        [Networked] public int FinalSurvivorCount { get; private set; }
        [Networked] public uint PhaseOrdinal { get; private set; }
        [Networked] public uint EndOrdinal { get; private set; }
        [Networked] private TickTimer EscapeTimer { get; set; }
        [Networked] private TickTimer MatchTimer { get; set; }
        [Networked] private TickTimer ReturnToLobbyTimer { get; set; }
        [Networked] private NetworkBool ReturnToLobbyRequested { get; set; }
        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public uint ScenarioConfigRevision { get; private set; }
        [Networked] public NetworkBool HasAppliedScenarioConfig { get; private set; }
        [Networked] public NetworkString<_64> AppliedScenarioConfigVersion { get; private set; }
        [Networked] public NetworkString<_64> AppliedScenarioPolicyVersion { get; private set; }
        [Networked] public NetworkString<_64> AppliedScenarioMapId { get; private set; }
        [Networked] public NetworkString<_64> AppliedScenarioMonsterType { get; private set; }
        [Networked] public NetworkString<_64> AppliedScenarioObjectiveSpawnSetId { get; private set; }
        [Networked] public NetworkString<_64> AppliedScenarioRouteModifier { get; private set; }
        [Networked] public NetworkString<_64> AppliedScenarioFallbackConfigId { get; private set; }
        [Networked] public int ScenarioConfigSourceValue { get; private set; }
        [Networked] public int ScenarioSupportItemBudget { get; private set; }
        [Networked] public double ScenarioDetectionFillRate { get; private set; }
        [Networked] public double ScenarioDetectionDecayRate { get; private set; }
        [Networked] public double ScenarioChaseSpeed { get; private set; }
        [Networked] public double ScenarioSearchDuration { get; private set; }
        [Networked] public double ScenarioEscapeDoorTimerSeconds { get; private set; }

        private MatchFlowController _legacyMatchFlow;
        private EscapeDoorCountdown _legacyEscapeCountdown;
        private bool _scenarioDecisionWindowOpen;
        private ScenarioDecisionPoint _scenarioDecisionWindowPoint;
        private string _scenarioDecisionWindowPhaseContext =
            string.Empty;
        private uint _lastPublishedScenarioConfigRevision;
        private Guid _lastPublishedScenarioConfigMatchId;
        private bool _endingForTeamDowned;

        public bool IsEnded => Status == NetworkMatchStatus.Ended;
        public bool IsEscapeTimerRunning => CurrentPhase == NetworkMatchPhase.Escape
                                            && EscapeTimer.IsRunning;
        public float EscapeRemainingSeconds => Remaining(EscapeTimer);
        public float MatchRemainingSeconds => Remaining(MatchTimer);
        public float CurrentScenarioEscapeDoorTimerSeconds =>
            Mathf.Max(
                0.1f,
                (float)(
                    ScenarioEscapeDoorTimerSeconds > 0d
                        ? ScenarioEscapeDoorTimerSeconds
                        : _escapeDurationSeconds));
        public ScenarioConfig CurrentAppliedScenarioConfig
        {
            get
            {
                return TryBuildReplicatedScenarioConfig(
                    out var config)
                    ? config
                    : null;
            }
        }

        public bool TryGetAppliedScenarioConfig(
            out ScenarioConfig config)
        {
            return TryBuildReplicatedScenarioConfig(
                out config);
        }

        public override void Spawned()
        {
            NetworkPlayerLifeState.StateChanged += HandlePlayerLifeStateChanged;
            ResolveLegacyPresentation();
            if (Object.HasStateAuthority)
            {
                CurrentPhase = NetworkMatchPhase.CoreObjective;
                Status = NetworkMatchStatus.Running;
                Result = NetworkMatchResult.None;
                EndReason = NetworkMatchEndReason.None;
                ObjectiveSourceId = default;
                EscapeDoorId = default;
                LastActor = PlayerRef.None;
                FinalSurvivorCount = 0;
                PhaseOrdinal = 0;
                EndOrdinal = 0;
                EscapeTimer = TickTimer.None;
                MatchTimer = TickTimer.CreateFromSeconds(Runner, _matchDurationSeconds);
                ReturnToLobbyTimer = TickTimer.None;
                ReturnToLobbyRequested = false;
                HasAppliedScenarioConfig = false;
                ScenarioConfigRevision = 0;
                ScenarioConfigSourceValue = 0;
                AppliedScenarioConfigVersion = default;
                AppliedScenarioPolicyVersion = default;
                AppliedScenarioMapId = default;
                AppliedScenarioMonsterType = default;
                AppliedScenarioObjectiveSpawnSetId = default;
                AppliedScenarioRouteModifier = default;
                AppliedScenarioFallbackConfigId = default;
                ScenarioSupportItemBudget = 0;
                ScenarioDetectionFillRate = 0d;
                ScenarioDetectionDecayRate = 0d;
                ScenarioChaseSpeed = 0d;
                ScenarioSearchDuration = 0d;
                ScenarioEscapeDoorTimerSeconds = 0d;
                OpenScenarioDecisionWindow(
                    ScenarioDecisionPoint.PreMatch,
                    "PRE_MATCH");

                try
                {
                    ResolveScenarioConfigAuthoritative(
                        ScenarioDecisionPoint.PreMatch,
                        "PRE_MATCH");
                }
                finally
                {
                    CloseScenarioDecisionWindow();
                }
            }

            if (Object == null || !Object.HasStateAuthority)
            {
                TryPublishReplicatedScenarioConfigToRegistry();
            }
            ApplyPresentation(notifyListeners: true);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            NetworkPlayerLifeState.StateChanged -= HandlePlayerLifeStateChanged;
            _lastPublishedScenarioConfigRevision = 0;
            _lastPublishedScenarioConfigMatchId = Guid.Empty;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            if (IsEnded)
            {
                if (!ReturnToLobbyRequested && ReturnToLobbyTimer.Expired(Runner))
                {
                    ReturnToLobbyRequested = true;
                    if (Runner != null && Runner.IsSceneAuthority)
                    {
                        if (Runner.SessionInfo.IsValid)
                        {
                            Runner.SessionInfo.IsOpen = true;
                            Runner.SessionInfo.IsVisible = true;
                        }

                        _ = Runner.LoadScene(NetworkBootstrap.LobbySceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
                    }
                }
                return;
            }

            if (MatchTimer.Expired(Runner))
            {
                TryEndMatch(NetworkMatchResult.Lose, NetworkMatchEndReason.MatchTimeout, PlayerRef.None);
                return;
            }

            if (CurrentPhase == NetworkMatchPhase.Escape && EscapeTimer.Expired(Runner))
            {
                TryEndMatch(NetworkMatchResult.Lose, NetworkMatchEndReason.EscapeTimeout, PlayerRef.None);
            }
        }

        public override void Render()
        {
            if (Object != null
                && !Object.HasStateAuthority
                && HasAppliedScenarioConfig
                && ScenarioConfigRevision != 0)
            {
                TryPublishReplicatedScenarioConfigToRegistry();
            }

            ApplyPresentation(
                notifyListeners: false);
        }

        public void InitializeAuthoritative(NetworkId objectiveSourceId, NetworkId escapeDoorId)
        {
            if (!Object.HasStateAuthority || !objectiveSourceId.IsValid) return;

            ObjectiveSourceId = objectiveSourceId;
            EscapeDoorId = escapeDoorId;
            HandleReplicatedStateChanged();
            Debug.Log(
                $"[MatchState] Initialized match={Object.Id}, objective={objectiveSourceId}, door={escapeDoorId}.");
        }

        public bool TryGetObjectiveProgress(out int current, out int required)
        {
            current = 0;
            required = 0;
            if (!TryResolveObjectiveSource(out var source)) return false;
            current = source.PlacedCoreCount;
            required = source.RequiredCoreCount;
            var allBoxes = FindObjectsByType<NetworkSectorBox>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allBoxes.Length; i++)
            {
                var other = allBoxes[i];
                if (other != null && other != source)
                {
                    current += other.PlacedCoreCount;
                    required += other.RequiredCoreCount;
                }
            }
            return true;
        }

        public bool TryCompleteCoreObjective(NetworkSectorBox source)
        {
            if (!ValidateObjectiveSource(source))
            {
                return false;
            }

            var allBoxes = FindObjectsByType<NetworkSectorBox>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allBoxes.Length; i++)
            {
                var box = allBoxes[i];
                if (box != null && !box.IsCoreObjectiveComplete)
                {
                    return false;
                }
            }

            if (!TryAdvancePhase(
                    NetworkMatchPhase.CoreObjective,
                    NetworkMatchPhase.Puzzle,
                    "CORE_COLLECTION"))
            {
                return false;
            }

            return true;
        }

        public bool TryCompletePuzzle(NetworkId sourceId)
        {
            return ValidateObjectiveSource(sourceId)
                && TryAdvancePhase(NetworkMatchPhase.Puzzle, NetworkMatchPhase.SecurityHold, "PUZZLE");
        }

        public bool TryCompleteSecurityHold(NetworkId sourceId)
        {
            return ValidateObjectiveSource(sourceId)
                && TryAdvancePhase(
                    NetworkMatchPhase.SecurityHold,
                    NetworkMatchPhase.FinalHunt,
                    "SECURITY_HOLD");
        }

        public bool TryEnterEscape(NetworkId doorId, PlayerRef actor)
        {
            if (!Object.HasStateAuthority
                || IsEnded
                || doorId != EscapeDoorId
                || !TryResolveActivePlayer(actor, out _)
                || !TryAdvancePhase(
                    NetworkMatchPhase.FinalHunt,
                    NetworkMatchPhase.Escape,
                    "FINAL_HUNT"))
            {
                return false;
            }

            LastActor = actor;
            EscapeTimer = TickTimer.CreateFromSeconds(Runner, CurrentScenarioEscapeDoorTimerSeconds);
            HandleReplicatedStateChanged();
            Debug.Log($"[MatchState] Escape started by {actor}; duration={CurrentScenarioEscapeDoorTimerSeconds:0.##}s.");
            return true;
        }

        public bool TryCommitPlayerEscaped(PlayerRef player)
        {
            if (!Object.HasStateAuthority
                || IsEnded
                || CurrentPhase != NetworkMatchPhase.Escape
                || !TryResolveActivePlayer(player, out var lifeState)
                || !lifeState.TryEscape())
            {
                return false;
            }

            return TryEndMatch(NetworkMatchResult.Win, NetworkMatchEndReason.PlayerEscaped, player);
        }

        private bool TryAdvancePhase(
            NetworkMatchPhase expected,
            NetworkMatchPhase next,
            string completedPhase)
        {
            if (!Object.HasStateAuthority
                || !NetworkMatchStateRules.CanAdvance(Status, CurrentPhase, expected, next))
            {
                return false;
            }

            var runtime = MatchAuthorityRuntime.Instance;
            runtime?.RecordPhaseCompleted(
                BuildKey("phase-completed-" + completedPhase.ToLowerInvariant()),
                completedPhase,
                "OBJECTIVE_COMPLETED");
            CurrentPhase = next;
            AdvancePhaseOrdinal();

            if (next != NetworkMatchPhase.Escape
                && next != NetworkMatchPhase.MatchEnded)
            {
                var decisionPoint =
                    next == NetworkMatchPhase.FinalHunt
                        ? ScenarioDecisionPoint.FinalHuntSetup
                        : ScenarioDecisionPoint.AllowedPhaseBoundary;

                var phaseContext =
                    PhaseName(next);

                OpenScenarioDecisionWindow(
                    decisionPoint,
                    phaseContext);

                try
                {
                    ResolveScenarioConfigAuthoritative(
                        decisionPoint,
                        phaseContext);
                }
                finally
                {
                    CloseScenarioDecisionWindow();
                }
            }
            runtime?.RecordPhaseStarted(
                BuildKey("phase-started-" + next.ToString().ToLowerInvariant()),
                PhaseName(next),
                "PREVIOUS_PHASE_COMPLETED");
            HandleReplicatedStateChanged();
            Debug.Log($"[MatchState] Phase {expected} -> {next}.");
            return true;
        }

        private bool TryEndMatch(
            NetworkMatchResult result,
            NetworkMatchEndReason reason,
            PlayerRef actor)
        {
            if (!Object.HasStateAuthority || !NetworkMatchStateRules.CanEnd(Status, result))
            {
                return false;
            }

            CountFinalPlayers(out var survivorCount, out _, out _);
            Status = NetworkMatchStatus.Ended;
            CurrentPhase = NetworkMatchPhase.MatchEnded;
            Result = result;
            EndReason = reason;
            LastActor = actor;
            FinalSurvivorCount = survivorCount;
            EscapeTimer = TickTimer.None;
            MatchTimer = TickTimer.None;
            ReturnToLobbyTimer = TickTimer.CreateFromSeconds(
                Runner,
                Mathf.Max(0.1f, _returnToLobbyDelaySeconds));
            ReturnToLobbyRequested = false;
            EndOrdinal++;
            if (EndOrdinal == 0) EndOrdinal = 1;
            AdvancePhaseOrdinal();

            MatchAuthorityRuntime.Instance?.RecordMatchEnded(
                BuildKey("match-ended"),
                result == NetworkMatchResult.Win ? "SUCCESS" : "FAILURE",
                survivorCount,
                ReasonCode(reason));
            HandleReplicatedStateChanged();
            Debug.Log(
                $"[MatchState] Match ended result={result}, reason={reason}, survivors={survivorCount}.");
            return true;
        }

        private void HandlePlayerLifeStateChanged(NetworkPlayerLifeState _)
        {
            if (Object == null || !Object.HasStateAuthority || IsEnded || _endingForTeamDowned) return;

            CountFinalPlayers(
                out var survivorCount,
                out var activeCount,
                out var trackedCount,
                out var downedCount,
                out var nonDownedMatchActiveCount);

            if (trackedCount > 0
                && survivorCount == 0
                && downedCount > 0
                && nonDownedMatchActiveCount == 0)
            {
                _endingForTeamDowned = true;
                try
                {
                    EliminateDownedPlayersAuthoritative();
                    TryEndMatch(
                        NetworkMatchResult.Lose,
                        NetworkMatchEndReason.AllPlayersEliminated,
                        PlayerRef.None);
                }
                finally
                {
                    _endingForTeamDowned = false;
                }
                return;
            }

            if (trackedCount > 0 && activeCount == 0 && survivorCount == 0)
            {
                TryEndMatch(
                    NetworkMatchResult.Lose,
                    NetworkMatchEndReason.AllPlayersEliminated,
                    PlayerRef.None);
            }
        }

        private void CountFinalPlayers(
            out int survivorCount,
            out int activeCount,
            out int trackedCount)
        {
            CountFinalPlayers(
                out survivorCount,
                out activeCount,
                out trackedCount,
                out _,
                out _);
        }

        private void CountFinalPlayers(
            out int survivorCount,
            out int activeCount,
            out int trackedCount,
            out int downedCount,
            out int nonDownedMatchActiveCount)
        {
            survivorCount = 0;
            activeCount = 0;
            trackedCount = 0;
            downedCount = 0;
            nonDownedMatchActiveCount = 0;
            foreach (var player in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(player, out var playerObject)
                    || playerObject == null
                    || !playerObject.TryGetComponent<LobbyPlayerState>(out var lobbyState)
                    || !lobbyState.IsGameplayPlayer
                    || !playerObject.TryGetComponent<NetworkPlayerLifeState>(out var lifeState))
                {
                    continue;
                }

                trackedCount++;
                if (lifeState.Status == NetworkPlayerLifeStatus.Escaped) survivorCount++;
                else if (lifeState.IsMatchActive)
                {
                    activeCount++;
                    if (lifeState.IsDowned)
                    {
                        downedCount++;
                    }
                    else
                    {
                        nonDownedMatchActiveCount++;
                    }
                }
            }
        }

        private void EliminateDownedPlayersAuthoritative()
        {
            foreach (var player in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(player, out var playerObject)
                    || playerObject == null
                    || !playerObject.TryGetComponent<LobbyPlayerState>(out var lobbyState)
                    || !lobbyState.IsGameplayPlayer
                    || !playerObject.TryGetComponent<NetworkPlayerLifeState>(out var lifeState)
                    || !lifeState.IsDowned)
                {
                    continue;
                }

                lifeState.ForceEliminateAuthoritative(
                    NetworkPlayerLifeTransitionCause.Bleedout,
                    "TEAM_DOWNED");
            }
        }

        private bool TryResolveActivePlayer(PlayerRef player, out NetworkPlayerLifeState lifeState)
        {
            lifeState = null;
            return player.IsValid
                && Runner.TryGetPlayerObject(player, out var playerObject)
                && playerObject != null
                && playerObject.InputAuthority == player
                && playerObject.TryGetComponent(out lifeState)
                && lifeState.CanInitiateAction;
        }

        private bool ValidateObjectiveSource(NetworkSectorBox source)
        {
            return Object.HasStateAuthority
                && !IsEnded
                && source != null
                && source.Object != null
                && source.Object.Id == ObjectiveSourceId
                && (source.Object.Id == ObjectiveSourceId || IsTrackedSectorBox(source))
                && source.Object.HasStateAuthority;
        }

        private bool IsTrackedSectorBox(NetworkSectorBox source)
        {
            if (source == null || source.Object == null) return false;
            var allBoxes = FindObjectsByType<NetworkSectorBox>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allBoxes.Length; i++)
            {
                if (allBoxes[i] == source) return true;
            }
            return false;
        }

        private bool ValidateObjectiveSource(NetworkId sourceId)
        {
            return sourceId.IsValid
                && sourceId == ObjectiveSourceId
                && TryResolveObjectiveSource(out var source)
                && ValidateObjectiveSource(source);
        }

        private bool TryResolveObjectiveSource(out NetworkSectorBox source)
        {
            source = null;
            return ObjectiveSourceId.IsValid
                && Runner.TryFindObject(ObjectiveSourceId, out var sourceObject)
                && sourceObject != null
                && sourceObject.TryGetComponent(out source);
        }

        private void AdvancePhaseOrdinal()
        {
            PhaseOrdinal++;
            if (PhaseOrdinal == 0) PhaseOrdinal = 1;
        }

        private float Remaining(TickTimer timer)
        {
            return Runner == null ? 0f : Mathf.Max(0f, timer.RemainingTime(Runner) ?? 0f);
        }

        private string BuildKey(string occurrence)
        {
            return $"match-state:{Object.Id}:{occurrence}:{PhaseOrdinal}:{EndOrdinal}";
        }

        private static string PhaseName(NetworkMatchPhase phase)
        {
            return phase switch
            {
                NetworkMatchPhase.CoreObjective => "CORE_COLLECTION",
                NetworkMatchPhase.Puzzle => "POWER_PUZZLE",
                NetworkMatchPhase.SecurityHold => "SECURITY_HOLD",
                NetworkMatchPhase.FinalHunt => "FINAL_HUNT",
                NetworkMatchPhase.Escape => "ESCAPE",
                _ => "MATCH_ENDED",
            };
        }

        private static string ReasonCode(NetworkMatchEndReason reason)
        {
            return reason switch
            {
                NetworkMatchEndReason.PlayerEscaped => "TEAM_ESCAPED",
                NetworkMatchEndReason.AllPlayersEliminated => "TEAM_ELIMINATED",
                // The telemetry contract only accepts these three terminal reason codes.
                // Precise timeout semantics remain replicated in NetworkMatchEndReason.
                NetworkMatchEndReason.MatchTimeout => "MATCH_ABORTED",
                NetworkMatchEndReason.EscapeTimeout => "MATCH_ABORTED",
                _ => "MATCH_ABORTED",
            };
        }

        private void ResolveLegacyPresentation()
        {
            if (_legacyMatchFlow == null) _legacyMatchFlow = FindAnyObjectByType<MatchFlowController>();
            if (_legacyEscapeCountdown == null)
            {
                _legacyEscapeCountdown = FindAnyObjectByType<EscapeDoorCountdown>();
            }
            _legacyMatchFlow?.SetNetworkAuthorityPresentationOnly(true);
            _legacyEscapeCountdown?.SetNetworkAuthorityPresentationOnly(true);
        }

        private void ApplyPresentation(bool notifyListeners)
        {
            ResolveLegacyPresentation();
            _legacyMatchFlow?.ApplyAuthoritativeSnapshot(CurrentPhase, Status, Result);
            _legacyEscapeCountdown?.ApplyAuthoritativeSnapshot(
                CurrentPhase == NetworkMatchPhase.FinalHunt || CurrentPhase == NetworkMatchPhase.Escape,
                IsEscapeTimerRunning,
                IsEnded && Result == NetworkMatchResult.Win,
                EscapeRemainingSeconds);
            if (notifyListeners) StateChanged?.Invoke(this);
        }

        private void HandleReplicatedStateChanged()
        {
            if (Object == null || !Object.HasStateAuthority)
            {
                TryPublishReplicatedScenarioConfigToRegistry();
            }
            ApplyPresentation(notifyListeners: true);
        }

        private void OpenScenarioDecisionWindow(
            ScenarioDecisionPoint decisionPoint,
            string phaseContext)
        {
            _scenarioDecisionWindowOpen = true;
            _scenarioDecisionWindowPoint = decisionPoint;
            _scenarioDecisionWindowPhaseContext =
                phaseContext ?? string.Empty;
        }

        private void CloseScenarioDecisionWindow()
        {
            _scenarioDecisionWindowOpen = false;
            _scenarioDecisionWindowPhaseContext =
                string.Empty;
        }

        private bool IsScenarioDecisionWindowOpen(
            ScenarioDecisionPoint decisionPoint,
            string phaseContext)
        {
            if (!_scenarioDecisionWindowOpen
                || _scenarioDecisionWindowPoint != decisionPoint
                || !string.Equals(
                    _scenarioDecisionWindowPhaseContext,
                    phaseContext ?? string.Empty,
                    StringComparison.Ordinal)
                || Status != NetworkMatchStatus.Running)
            {
                return false;
            }

            switch (decisionPoint)
            {
                case ScenarioDecisionPoint.PreMatch:
                    return CurrentPhase == NetworkMatchPhase.CoreObjective
                           && ScenarioConfigRevision == 0;

                case ScenarioDecisionPoint.AllowedPhaseBoundary:
                    return CurrentPhase == NetworkMatchPhase.Puzzle
                           || CurrentPhase == NetworkMatchPhase.SecurityHold;

                case ScenarioDecisionPoint.FinalHuntSetup:
                    return CurrentPhase == NetworkMatchPhase.FinalHunt
                           && !EscapeTimer.IsRunning;

                default:
                    return false;
            }
        }

        private ScenarioResolutionEngineResult ValidateScenarioPrecommit(
            ScenarioResolutionEngineResult result,
            Guid matchId,
            ScenarioResolutionMode mode,
            ScenarioDecisionPoint decisionPoint,
            string phaseContext,
            string experimentCondition)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!result.ShouldApplyConfig)
            {
                return result;
            }

            if (!IsScenarioDecisionWindowOpen(
                    decisionPoint,
                    phaseContext))
            {
                return result.WithPrecommitRejection(
                    AEDReasonCodes.DecisionWindowClosed);
            }

            var scenarioRuntime =
                ScenarioConfigAuthorityRuntime.EnsureExists();

            var currentBase =
                decisionPoint == ScenarioDecisionPoint.PreMatch
                    ? FixedDirector.CreateFixedBaseline()
                    : scenarioRuntime.CurrentAppliedScenarioConfig;

            if (currentBase == null
                || !FixedDirector.MatchesBaseRef(
                    result.CapturedBaseRef,
                    currentBase,
                    decisionPoint))
            {
                return result.WithPrecommitRejection(
                    AEDReasonCodes.StaleBaseConfig);
            }

            var appliedConfig =
                result.Decision.AppliedConfig;

            var requiresAdaptiveInputRevalidation =
                mode == ScenarioResolutionMode.Adaptive
                && result.Decision.Result == AdaptiveDecisionResult.Applied
                && appliedConfig != null
                && appliedConfig.ConfigSource == ScenarioConfigSource.Adaptive;

            if (requiresAdaptiveInputRevalidation)
            {
                var request =
                    new ScenarioResolutionRequest(
                        result.Decision.DecisionId,
                        matchId,
                        mode,
                        decisionPoint,
                        phaseContext ?? string.Empty,
                        experimentCondition ?? string.Empty);

                if (!AdaptiveInputSnapshotRuntime.IsStillCurrent(
                        request,
                        result.CapturedAdaptiveSnapshotId,
                        result.CapturedAdaptiveSnapshotFingerprint,
                        out _))
                {
                    return result.WithPrecommitRejection(
                        AEDReasonCodes.StaleInput);
                }
            }

            return result;
        }

        private ScenarioResolutionEngineResult
            MaterializePreMatchResolvedBaseForNoChange(
                ScenarioResolutionEngineResult result,
                Guid matchId,
                ScenarioDecisionPoint decisionPoint,
                string phaseContext)
        {
            if (result == null)
            {
                throw new ArgumentNullException(
                    nameof(result));
            }

            if (decisionPoint
                    != ScenarioDecisionPoint.PreMatch
                || result.CommitDisposition
                    != ScenarioResolutionCommitDisposition.NewDecision
                || result.AttemptedDecision.Result
                    != AdaptiveDecisionResult.NoChange
                || result.AttemptedDecision.FallbackAction
                    != ScenarioFallbackAction.None
                || HasAppliedScenarioConfig
                || ScenarioConfigRevision != 0)
            {
                return result;
            }

            if (!IsScenarioDecisionWindowOpen(
                    decisionPoint,
                    phaseContext))
            {
                return result.WithPrecommitRejection(
                    AEDReasonCodes.DecisionWindowClosed);
            }

            var currentResolvedBase =
                FixedDirector.CreateFixedBaseline();

            if (!FixedDirector.MatchesBaseRef(
                    result.CapturedBaseRef,
                    currentResolvedBase,
                    ScenarioDecisionPoint.PreMatch))
            {
                return result.WithPrecommitRejection(
                    AEDReasonCodes.StaleBaseConfig);
            }

            var capturedBase =
                result.CapturedBaseConfig;

            if (capturedBase == null)
            {
                return result.WithPrecommitRejection(
                    AEDReasonCodes.FallbackConfigInvalid);
            }

            var validation =
                ScenarioValidator.ValidateFixedBaseline(
                    capturedBase);

            if (!validation.IsValid)
            {
                return result.WithPrecommitRejection(
                    AEDReasonCodes.FallbackConfigInvalid);
            }

            // This is NOT FixedDirector fallback.
            // It establishes the exact zero-delta PRE_MATCH base
            // as the first durable AppliedScenarioConfig.
            ApplyScenarioConfigAuthoritative(
                capturedBase,
                matchId);

            return result;
        }

        private bool TryApplyPreMatchFixedFallbackAuthoritative(
            Guid matchId)
        {
            var fixedConfig =
                FixedDirector.CreateFixedBaseline();

            var validation =
                ScenarioValidator.ValidateFixedBaseline(
                    fixedConfig);

            if (!validation.IsValid)
            {
                Debug.LogError(
                    "[AED] Fixed baseline invalid during " +
                    "PRE_MATCH fallback.");

                return false;
            }

            ApplyScenarioConfigAuthoritative(
                fixedConfig,
                matchId);

            return true;
        }

        private void ResolveScenarioConfigAuthoritative(
            ScenarioDecisionPoint decisionPoint,
            string phaseContext)
        {
            if (Object == null || !Object.HasStateAuthority)
            {
                return;
            }

            var authority = MatchAuthorityRuntime.Instance;
            var matchId = authority != null && authority.TryGetMatchId(out var resolvedMatchId)
                ? resolvedMatchId
                : ScenarioConfigFingerprint.DeterministicGuid("network-match-state:" + Object.Id);
            var mode = authority != null
                ? authority.RequestedScenarioResolutionMode
                : ScenarioResolutionMode.Fixed;
            var scenarioRuntime =
                ScenarioConfigAuthorityRuntime.EnsureExists();

            var experimentCondition =
                authority != null
                    ? authority.ExperimentCondition
                    : string.Empty;

            var result = scenarioRuntime.Resolve(
                matchId,
                mode,
                decisionPoint,
                phaseContext,
                PhaseOrdinal,
                experimentCondition);
            if (result.ShouldApplyConfig)
            {
                result =
                    ValidateScenarioPrecommit(
                        result,
                        matchId,
                        mode,
                        decisionPoint,
                        phaseContext,
                        experimentCondition);

                if (result.ShouldApplyConfig)
                {
                    ApplyScenarioConfigAuthoritative(
                        result.Decision.AppliedConfig,
                        matchId);
                }
                else if (result.IsPrecommitRejected
                         && decisionPoint
                            == ScenarioDecisionPoint.PreMatch)
                {
                    if (!TryApplyPreMatchFixedFallbackAuthoritative(
                            matchId))
                    {
                        result =
                            result.WithPrecommitRejection(
                                AEDReasonCodes.FallbackConfigInvalid);
                    }
                }
            }

            if (!result.ShouldApplyConfig)
            {
                result =
                    MaterializePreMatchResolvedBaseForNoChange(
                        result,
                        matchId,
                        decisionPoint,
                        phaseContext);
            }

            scenarioRuntime.FinalizeResolution(
                matchId,
                result,
                Object != null
                && Object.HasStateAuthority);

            authority?.NotifyScenarioResolutionFinalized(
                matchId,
                scenarioRuntime.LastResolutionRecord);
        }

        private void ApplyScenarioConfigAuthoritative(ScenarioConfig config, Guid matchId)
        {
            if (config == null
                || Object == null
                || !Object.HasStateAuthority)
            {
                return;
            }

            var nextRevision =
                ScenarioConfigRevision + 1;

            if (nextRevision == 0)
            {
                nextRevision = 1;
            }

            // Exact durable identity/provenance.
            AppliedScenarioConfigVersion =
                config.ScenarioConfigVersion;

            AppliedScenarioPolicyVersion =
                config.PolicyVersion;

            AppliedScenarioMapId =
                config.MapId;

            AppliedScenarioMonsterType =
                config.MonsterType;

            AppliedScenarioObjectiveSpawnSetId =
                config.ObjectiveSpawnSetId;

            AppliedScenarioRouteModifier =
                config.RouteModifier;

            AppliedScenarioFallbackConfigId =
                config.FallbackConfigId;

            ScenarioConfigSourceValue =
                config.ConfigSource == ScenarioConfigSource.Adaptive
                    ? 1
                    : 0;

            // Durable gameplay values.
            ScenarioSupportItemBudget =
                config.SupportItemBudget;

            ScenarioDetectionFillRate =
                config.MonsterParameters.DetectionFillRate;

            ScenarioDetectionDecayRate =
                config.MonsterParameters.DetectionDecayRate;

            ScenarioChaseSpeed =
                config.MonsterParameters.ChaseSpeed;

            ScenarioSearchDuration =
                config.MonsterParameters.SearchDuration;

            ScenarioEscapeDoorTimerSeconds =
                config.FinalHuntParameters.EscapeDoorTimerSeconds;

            HasAppliedScenarioConfig = true;

            // Commit marker MUST be last networked write.
            ScenarioConfigRevision =
                nextRevision;

            // Publish exact authoritative object only AFTER durable commit.
            ScenarioConfigAuthorityRuntime
                .EnsureExists()
                .Apply(
                    matchId,
                    config);
        }

        private bool TryBuildReplicatedScenarioConfig(
            out ScenarioConfig config)
        {
            config = null;

            if (ScenarioConfigSourceValue != 0
                && ScenarioConfigSourceValue != 1)
            {
                return false;
            }

            if (!HasAppliedScenarioConfig
                || ScenarioConfigRevision == 0)
            {
                return false;
            }

            var scenarioConfigVersion =
                AppliedScenarioConfigVersion.ToString();

            var policyVersion =
                AppliedScenarioPolicyVersion.ToString();

            var mapId =
                AppliedScenarioMapId.ToString();

            var monsterType =
                AppliedScenarioMonsterType.ToString();

            var objectiveSpawnSetId =
                AppliedScenarioObjectiveSpawnSetId.ToString();

            var routeModifier =
                AppliedScenarioRouteModifier.ToString();

            var fallbackConfigId =
                AppliedScenarioFallbackConfigId.ToString();

            if (string.IsNullOrWhiteSpace(scenarioConfigVersion)
                || string.IsNullOrWhiteSpace(policyVersion)
                || string.IsNullOrWhiteSpace(mapId)
                || string.IsNullOrWhiteSpace(monsterType)
                || string.IsNullOrWhiteSpace(objectiveSpawnSetId)
                || string.IsNullOrWhiteSpace(routeModifier)
                || string.IsNullOrWhiteSpace(fallbackConfigId))
            {
                return false;
            }

            if (ScenarioSupportItemBudget < 0
                || !double.IsFinite(ScenarioDetectionFillRate)
                || !double.IsFinite(ScenarioDetectionDecayRate)
                || !double.IsFinite(ScenarioChaseSpeed)
                || !double.IsFinite(ScenarioSearchDuration)
                || !double.IsFinite(ScenarioEscapeDoorTimerSeconds))
            {
                return false;
            }

            if (ScenarioConfigSourceValue != 0
                && ScenarioConfigSourceValue != 1)
            {
                return false;
            }

            var source =
                ScenarioConfigSourceValue == 1
                    ? ScenarioConfigSource.Adaptive
                    : ScenarioConfigSource.Fixed;

            config =
                new ScenarioConfig(
                    scenarioConfigVersion,
                    policyVersion,
                    source,
                    mapId,
                    monsterType,
                    objectiveSpawnSetId,
                    ScenarioSupportItemBudget,
                    new ScenarioMonsterParameters(
                        ScenarioDetectionFillRate,
                        ScenarioDetectionDecayRate,
                        ScenarioChaseSpeed,
                        ScenarioSearchDuration),
                    routeModifier,
                    new ScenarioFinalHuntParameters(
                        ScenarioEscapeDoorTimerSeconds),
                    fallbackConfigId);

            return true;
        }

        private bool TryPublishReplicatedScenarioConfigToRegistry()
        {
            if (!HasAppliedScenarioConfig
                || ScenarioConfigRevision == 0)
            {
                return false;
            }

            var authority =
                MatchAuthorityRuntime.Instance;

            if (authority == null
                || !authority.TryGetMatchId(
                    out var matchId)
                || matchId == Guid.Empty)
            {
                return false;
            }

            if (_lastPublishedScenarioConfigMatchId
                    == matchId
                && _lastPublishedScenarioConfigRevision
                    == ScenarioConfigRevision)
            {
                return true;
            }

            if (!TryBuildReplicatedScenarioConfig(
                    out var config)
                || config == null)
            {
                return false;
            }

            ScenarioConfigRuntimeRegistry.Apply(
                matchId,
                config);

            _lastPublishedScenarioConfigMatchId =
                matchId;

            _lastPublishedScenarioConfigRevision =
                ScenarioConfigRevision;

            return true;
        }
    }
}
