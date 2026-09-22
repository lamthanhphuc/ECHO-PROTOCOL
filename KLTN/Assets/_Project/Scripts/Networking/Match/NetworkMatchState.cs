using System;
using EchoProtocol.MatchFlow;
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
        Zone2Objective = 6,
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

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkString<_16> PowerAuthorizationCode { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkBool SecurityHoldCompleted { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkBool PowerAuthorizationAvailable { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkBool PowerPuzzleCompleted { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkBool RestoreMainPowerCompleted { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public Zone2MissionStage Zone2Stage { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public int RelayCompletionMask { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkBool SecurityTerminalDiscovered { get; private set; }

        [Networked, OnChangedRender(nameof(HandleReplicatedStateChanged))]
        public NetworkBool ZoneDoorsUnlocked { get; private set; }

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

        public static NetworkMatchState Instance { get; private set; }

        public int CompletedRelayCount => (RelayCompletionMask & 1) + ((RelayCompletionMask >> 1) & 1) + ((RelayCompletionMask >> 2) & 1) + ((RelayCompletionMask >> 3) & 1);
        public bool AreAllRelaysOnline => (RelayCompletionMask & 0x0F) == 0x0F;
        public int PowerRelaysOnline => (RelayCompletionMask & 1) + ((RelayCompletionMask >> 1) & 1);
        public int DataRelaysOnline => ((RelayCompletionMask >> 2) & 1) + ((RelayCompletionMask >> 3) & 1);

        private MatchFlowController _legacyMatchFlow;
        private EscapeDoorCountdown _legacyEscapeCountdown;
        private bool _endingForTeamDowned;
        private string _serverGeneratedAuthCode;

        public bool IsEnded => Status == NetworkMatchStatus.Ended;
        public bool IsEscapeTimerRunning => CurrentPhase == NetworkMatchPhase.Escape
                                            && EscapeTimer.IsRunning;
        public float EscapeRemainingSeconds => Remaining(EscapeTimer);
        public float MatchRemainingSeconds => Remaining(MatchTimer);

        public override void Spawned()
        {
            Instance = this;
            NetworkPlayerLifeState.StateChanged += HandlePlayerLifeStateChanged;
            ResolveLegacyPresentation();
            if (Object.HasStateAuthority)
            {
                CurrentPhase = NetworkMatchPhase.CoreObjective;
                Zone2Stage = Zone2MissionStage.Zone1CoreObjective;
                RelayCompletionMask = 0;
                SecurityTerminalDiscovered = false;
                ZoneDoorsUnlocked = false;
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
                _serverGeneratedAuthCode = UnityEngine.Random.Range(0, 10000).ToString("D4");
                PowerAuthorizationCode = string.Empty;
                SecurityHoldCompleted = false;
                PowerAuthorizationAvailable = false;
                PowerPuzzleCompleted = false;
                RestoreMainPowerCompleted = false;
            }

            ApplyPresentation(notifyListeners: true);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
            NetworkPlayerLifeState.StateChanged -= HandlePlayerLifeStateChanged;
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
            // Only the derived timer display changes every render frame. Replicated state
            // listeners are notified by OnChangedRender, not spammed once per frame.
            ApplyPresentation(notifyListeners: false);
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

            Zone2Stage = Zone2MissionStage.FindSecurityTerminal;
            if (!TryAdvancePhase(
                    NetworkMatchPhase.CoreObjective,
                    NetworkMatchPhase.Zone2Objective,
                    "CORE_COLLECTION"))
            {
                return false;
            }

            return true;
        }

        public bool TryDiscoverSecurityTerminal(PlayerRef actor)
        {
            if (!Object.HasStateAuthority || IsEnded) return false;
            if (Zone2Stage != Zone2MissionStage.FindSecurityTerminal) return false;

            SecurityTerminalDiscovered = true;
            Zone2Stage = Zone2MissionStage.RepairRelays;
            HandleReplicatedStateChanged();
            Debug.Log($"[MatchState] Security terminal discovered by {actor}. Stage -> RepairRelays.");
            return true;
        }

        public bool TryReportRelayOnline(RelaySlot slot)
        {
            if (!Object.HasStateAuthority || IsEnded) return false;
            int bit = 1 << (int)slot;
            if ((RelayCompletionMask & bit) != 0) return false;

            RelayCompletionMask |= bit;
            Debug.Log($"[MatchState] Relay {slot} repaired. Online mask: {RelayCompletionMask} ({CompletedRelayCount}/4).");

            if (AreAllRelaysOnline && (Zone2Stage == Zone2MissionStage.RepairRelays || Zone2Stage == Zone2MissionStage.FindSecurityTerminal))
            {
                Zone2Stage = Zone2MissionStage.SecurityHoldReady;
                Debug.Log("[MatchState] All 4 relays online. Stage -> SecurityHoldReady.");
            }

            HandleReplicatedStateChanged();
            return true;
        }

        public bool TryStartSecurityHold(PlayerRef actor)
        {
            if (!Object.HasStateAuthority || IsEnded) return false;
            if (!AreAllRelaysOnline) return false;
            if (Zone2Stage == Zone2MissionStage.SecurityHoldReady)
            {
                Zone2Stage = Zone2MissionStage.SecurityHold;
                HandleReplicatedStateChanged();
                return true;
            }
            return Zone2Stage == Zone2MissionStage.SecurityHold;
        }

        public bool TryCompleteSecurityHold(NetworkId sourceId)
        {
            if (sourceId.IsValid && !ValidateObjectiveSource(sourceId))
            {
                return false;
            }

            if (SecurityHoldCompleted)
            {
                return false;
            }

            SecurityHoldCompleted = true;
            PowerAuthorizationAvailable = true;
            if (string.IsNullOrEmpty(PowerAuthorizationCode.ToString()))
            {
                if (string.IsNullOrEmpty(_serverGeneratedAuthCode))
                {
                    _serverGeneratedAuthCode = UnityEngine.Random.Range(0, 10000).ToString("D4");
                }
                PowerAuthorizationCode = _serverGeneratedAuthCode;
            }

            Zone2Stage = Zone2MissionStage.AuthorizationCodeGranted;
            HandleReplicatedStateChanged();
            Debug.Log($"[MatchState] Security Hold completed! Code: {PowerAuthorizationCode}. Stage -> AuthorizationCodeGranted.");
            return true;
        }

        public bool TrySubmitZoneAccessCode(PlayerRef requester, string code)
        {
            if (!Object.HasStateAuthority || IsEnded) return false;
            if (!SecurityHoldCompleted || ZoneDoorsUnlocked) return false;
            if (string.IsNullOrEmpty(code) || code != _serverGeneratedAuthCode) return false;

            ZoneDoorsUnlocked = true;
            PowerPuzzleCompleted = true;
            RestoreMainPowerCompleted = true;
            Zone2Stage = Zone2MissionStage.Zone2Completed;
            HandleReplicatedStateChanged();
            Debug.Log($"[MatchState] Zone access code '{code}' accepted! Doors unlocked. Zone 2 complete.");
            return true;
        }

        public bool TrySubmitPowerCode(PlayerRef requester, string code)
        {
            return TrySubmitZoneAccessCode(requester, code);
        }

        public bool TryCompletePuzzle(NetworkId sourceId)
        {
            if (sourceId.IsValid && !ValidateObjectiveSource(sourceId)) return false;
            if (ZoneDoorsUnlocked) return false;

            return TrySubmitZoneAccessCode(PlayerRef.None, _serverGeneratedAuthCode);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcDiscoverSecurityTerminal(PlayerRef sender)
        {
            TryDiscoverSecurityTerminal(sender);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcReportRelayOnline(PlayerRef sender, int relaySlot)
        {
            TryReportRelayOnline((RelaySlot)relaySlot);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcSubmitZoneAccessCode(PlayerRef sender, string code)
        {
            TrySubmitZoneAccessCode(sender, code);
        }

        public bool VerifyPowerCode(string code)
        {
            return !string.IsNullOrEmpty(code) && code == _serverGeneratedAuthCode;
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
            EscapeTimer = TickTimer.CreateFromSeconds(Runner, _escapeDurationSeconds);
            HandleReplicatedStateChanged();
            Debug.Log($"[MatchState] Escape started by {actor}; duration={_escapeDurationSeconds:0.##}s.");
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
                NetworkMatchPhase.Zone2Objective => "ZONE_2_OBJECTIVE",
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
            _legacyMatchFlow?.ApplyAuthoritativeSnapshot(
                CurrentPhase,
                Status,
                Result,
                PowerAuthorizationCode.ToString(),
                SecurityHoldCompleted,
                PowerPuzzleCompleted,
                RestoreMainPowerCompleted);
            _legacyEscapeCountdown?.ApplyAuthoritativeSnapshot(
                CurrentPhase == NetworkMatchPhase.FinalHunt || CurrentPhase == NetworkMatchPhase.Escape,
                IsEscapeTimerRunning,
                IsEnded && Result == NetworkMatchResult.Win,
                EscapeRemainingSeconds);

            if (Zone2MissionDirector.Instance != null)
            {
                Zone2MissionDirector.Instance.OnAuthoritativeStateChanged(
                    Zone2Stage,
                    RelayCompletionMask,
                    SecurityTerminalDiscovered,
                    SecurityHoldCompleted,
                    ZoneDoorsUnlocked,
                    PowerAuthorizationCode.ToString());
            }

            if (notifyListeners) StateChanged?.Invoke(this);
        }

        private void HandleReplicatedStateChanged()
        {
            ApplyPresentation(notifyListeners: true);
        }
    }
}
