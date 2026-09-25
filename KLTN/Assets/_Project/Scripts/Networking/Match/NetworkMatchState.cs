using System;
using EchoProtocol.MatchFlow;
using EchoProtocol.Diagnostics;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.Networking.Authority;
using EchoProtocol.RelayA;
using EchoProtocol.RelayB;
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
        [SerializeField, Min(0.1f)] private float _zone2InteractionDistance = 3f;
        [SerializeField, Min(0.1f)] private float _zoneAccessCooldownSeconds = 5f;
        [SerializeField, Min(1)] private int _zoneAccessFailuresBeforeCooldown = 3;
        [SerializeField, Min(1f)] private float _securityHoldRelayRetryWindowSeconds = 300f;

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

        [Networked] public PlayerRef RelayA1Operator { get; private set; }
        [Networked] public PlayerRef RelayA2Operator { get; private set; }
        [Networked] public PlayerRef RelayB1Operator { get; private set; }
        [Networked] public PlayerRef RelayB2Operator { get; private set; }
        [Networked] public int RelayA1AttemptSeed { get; private set; }
        [Networked] public Vector3 RelayA1Controls { get; private set; }
        [Networked] public NetworkBool RelayA1Running { get; private set; }
        [Networked] public int RelayA2AttemptSeed { get; private set; }
        [Networked] public Vector3 RelayA2Controls { get; private set; }
        [Networked] public NetworkBool RelayA2Running { get; private set; }
        [Networked] public int RelayB1AttemptSeed { get; private set; }
        [Networked] public int RelayB1PresetIndex { get; private set; }
        [Networked] public int RelayB1Channel { get; private set; }
        [Networked] public float RelayB1Frequency { get; private set; }
        [Networked] public float RelayB1Phase { get; private set; }
        [Networked] public NetworkBool RelayB1Synchronizing { get; private set; }
        [Networked] public int RelayB2AttemptSeed { get; private set; }
        [Networked] public int RelayB2PresetIndex { get; private set; }
        [Networked] public int RelayB2Channel { get; private set; }
        [Networked] public float RelayB2Frequency { get; private set; }
        [Networked] public float RelayB2Phase { get; private set; }
        [Networked] public NetworkBool RelayB2Synchronizing { get; private set; }
        [Networked] private NetworkBool Zone2RelayRuntimeInitialized { get; set; }
        [Networked] public PlayerRef SecurityHoldOperator { get; private set; }
        [Networked] public PlayerRef SecurityHoldOperator2 { get; private set; }
        [Networked] public PlayerRef SecurityHoldOperator3 { get; private set; }
        [Networked] public PlayerRef SecurityHoldOperator4 { get; private set; }
        private readonly TickTimer[] _securityHoldLeases = new TickTimer[4];
        private static readonly RuntimeNoiseCatalog RelayNoiseCatalog = RuntimeNoiseCatalog.CreateDefault();
        private TickTimer _relayA1NoiseTimer;
        private TickTimer _relayA2NoiseTimer;
        private TickTimer _relayB1NoiseTimer;
        private TickTimer _relayB2NoiseTimer;
        private long _relayA1NoiseSequence;
        private long _relayA2NoiseSequence;
        private long _relayB1NoiseSequence;
        private long _relayB2NoiseSequence;
        [Networked] public float SecurityHoldDurationSeconds { get; private set; }
        [Networked] public float SecurityHoldAccumulatedSeconds { get; private set; }
        [Networked] private TickTimer RelayRepairWindowTimer { get; set; }
        [Networked] private TickTimer RelayRepairResetNoticeTimer { get; set; }
        [Networked] public int ZoneAccessFailureCount { get; private set; }
        [Networked] private TickTimer ZoneAccessCooldown { get; set; }

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

        public static NetworkMatchState Instance { get; private set; }
        public static event Action<Zone2AccessCodeResult> LocalZoneAccessCodeRequestCompleted;

        public int CompletedRelayCount => (RelayCompletionMask & 1) + ((RelayCompletionMask >> 1) & 1) + ((RelayCompletionMask >> 2) & 1) + ((RelayCompletionMask >> 3) & 1);
        public bool AreAllRelaysOnline => (RelayCompletionMask & 0x0F) == 0x0F;
        public int PowerRelaysOnline => (RelayCompletionMask & 1) + ((RelayCompletionMask >> 1) & 1);
        public int DataRelaysOnline => ((RelayCompletionMask >> 2) & 1) + ((RelayCompletionMask >> 3) & 1);
        public int SecurityHoldParticipantCount => (SecurityHoldOperator.IsNone ? 0 : 1)
            + (SecurityHoldOperator2.IsNone ? 0 : 1)
            + (SecurityHoldOperator3.IsNone ? 0 : 1)
            + (SecurityHoldOperator4.IsNone ? 0 : 1);
        public float SecurityHoldEstimatedRemainingSeconds
        {
            get
            {
                float rate = SecurityHoldWorkRate(SecurityHoldParticipantCount);
                return rate > 0f
                    ? Mathf.Max(0f, SecurityHoldDurationSeconds - SecurityHoldAccumulatedSeconds) / rate
                    : 0f;
            }
        }
        public bool IsSecurityHoldRunning => !SecurityHoldCompleted
            && CurrentPhase == NetworkMatchPhase.Zone2Objective
            && Zone2Stage == Zone2MissionStage.SecurityHold
            && SecurityHoldParticipantCount > 0;
        public float SecurityHoldProgress01
        {
            get
            {
                if (SecurityHoldCompleted) return 1f;
                float duration = Mathf.Max(0f, SecurityHoldDurationSeconds);
                if (duration <= 0f) return 0f;
                return Mathf.Clamp01(SecurityHoldAccumulatedSeconds / duration);
            }
        }
        public bool IsZoneAccessCooldownActive => ZoneAccessCooldown.IsRunning
            && Runner != null
            && !ZoneAccessCooldown.Expired(Runner);
        public float ZoneAccessCooldownRemainingSeconds => IsZoneAccessCooldownActive
            ? Remaining(ZoneAccessCooldown)
            : 0f;
        public bool IsRelayRepairWindowRunning => RelayRepairWindowTimer.IsRunning
            && Runner != null
            && !RelayRepairWindowTimer.Expired(Runner);
        public float RelayRepairWindowRemainingSeconds => IsRelayRepairWindowRunning
            ? Remaining(RelayRepairWindowTimer)
            : 0f;
        public bool IsRelayRepairResetNoticeActive => RelayRepairResetNoticeTimer.IsRunning
            && Runner != null && !RelayRepairResetNoticeTimer.Expired(Runner);
        public bool HasInitializedZone2RelayRuntime => Zone2RelayRuntimeInitialized;

        private MatchFlowController _legacyMatchFlow;
        private EscapeDoorCountdown _legacyEscapeCountdown;
        private bool _scenarioDecisionWindowOpen;
        private ScenarioDecisionPoint _scenarioDecisionWindowPoint;
        private string _scenarioDecisionWindowPhaseContext =
            string.Empty;
        private uint _lastPublishedScenarioConfigRevision;
        private Guid _lastPublishedScenarioConfigMatchId;
        private bool _endingForTeamDowned;
        private string _serverGeneratedAuthCode;

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
                ResetZone2AuthoritativeState();
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
            if (Instance == this) Instance = null;
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

            if (CurrentPhase == NetworkMatchPhase.Zone2Objective)
            {
                if (!Zone2RelayRuntimeInitialized)
                {
                    InitializeZone2RelayRuntimeAuthoritative();
                }
                ReleaseInvalidRelayOperatorsAuthoritative();
                if (ShouldResetRelayRepairForSecurityHoldTimeout())
                {
                    ResetRelayRepairForRetryAuthoritative();
                    return;
                }
                EmitRelayRepairNoiseAuthoritative();
                AdvanceSecurityHoldAuthoritative();
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
            RuntimeLog.Log(
                RuntimeLogCategory.MatchState,

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
            if (CurrentPhase != NetworkMatchPhase.CoreObjective
                || !ValidateObjectiveSource(source))
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

            ResetZone2AuthoritativeState();
            if (!InitializeZone2RelayRuntimeAuthoritative())
            {
                return false;
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
            if (!Object.HasStateAuthority
                || IsEnded
                || CurrentPhase != NetworkMatchPhase.Zone2Objective
                || Zone2Stage != Zone2MissionStage.FindSecurityTerminal
                || !TryGetZone2Director(out var director)
                || director.SecurityTerminal == null
                || !TryValidateZone2Requester(actor, director.SecurityTerminal, director.SecurityTerminal.MaxInteractorDistance))
            {
                return false;
            }

            SecurityTerminalDiscovered = true;
            Zone2Stage = Zone2MissionStage.RepairRelays;
            HandleReplicatedStateChanged();
            return true;
        }

        public bool RequestDiscoverSecurityTerminal()
        {
            if (!HasValidNetworkObject()) return false;
            if (Object.HasStateAuthority)
            {
                return TryGetLocalRequester(out var requester)
                    && TryDiscoverSecurityTerminal(requester);
            }
            RpcDiscoverSecurityTerminal();
            return true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcDiscoverSecurityTerminal(RpcInfo info = default)
        {
            TryDiscoverSecurityTerminal(info.Source);
        }

        public bool TryReportRelayOnline(RelaySlot slot)
        {
            if (!Object.HasStateAuthority
                || IsEnded
                || CurrentPhase != NetworkMatchPhase.Zone2Objective
                || Zone2Stage != Zone2MissionStage.RepairRelays
                || !IsValidRelaySlot(slot)) return false;
            int bit = 1 << (int)slot;
            if ((RelayCompletionMask & bit) != 0) return false;

            RelayCompletionMask |= bit;
            SetRelayOperator(slot, PlayerRef.None);
            SetRelayActiveState(slot, false);
            Debug.Log($"[MatchState] Relay {slot} repaired. Online mask: {RelayCompletionMask} ({CompletedRelayCount}/4).");

            if (AreAllRelaysOnline)
            {
                Zone2Stage = Zone2MissionStage.SecurityHoldReady;
                RelayRepairWindowTimer = TickTimer.CreateFromSeconds(
                    Runner,
                    Mathf.Max(1f, _securityHoldRelayRetryWindowSeconds));
                Debug.Log("[MatchState] All 4 relays online. Stage -> SecurityHoldReady.");
            }

            HandleReplicatedStateChanged();
            return true;
        }

        public bool TryStartSecurityHold(PlayerRef actor)
        {
            return TryStartSecurityHoldAuthoritative(actor);
        }

        public bool TryCompleteSecurityHold(NetworkId sourceId)
        {
            // Legacy compatibility: scene objects cannot bypass the authoritative timer.
            return CompleteSecurityHoldAuthoritative();
        }

        public bool TrySubmitZoneAccessCode(PlayerRef requester, string code)
        {
            // Legacy compatibility: callers without an exact panel cannot satisfy target validation.
            return false;
        }

        public bool TrySubmitPowerCode(PlayerRef requester, string code)
        {
            return TrySubmitZoneAccessCode(requester, code);
        }

        public bool TryCompletePuzzle(NetworkId sourceId)
        {
            // Legacy compatibility: retired NetworkPowerPuzzle cannot unlock Zone 2.
            return false;
        }

        public bool VerifyPowerCode(string code)
        {
            return !string.IsNullOrEmpty(code) && code == _serverGeneratedAuthCode;
        }

        public bool RequestAcquireRelay(RelaySlot slot)
        {
            if (!HasValidNetworkObject()) return false;
            if (Object.HasStateAuthority)
            {
                return TryGetLocalRequester(out var requester)
                    && TryAcquireRelayAuthoritative(requester, slot);
            }
            RpcAcquireRelay((int)slot);
            return true;
        }

        public void RequestReleaseRelay(RelaySlot slot)
        {
            if (!HasValidNetworkObject()) return;
            if (Object.HasStateAuthority)
            {
                if (TryGetLocalRequester(out var requester)) ReleaseRelayAuthoritative(requester, slot);
            }
            else RpcReleaseRelay((int)slot);
        }

        public bool RequestRelayAControls(RelaySlot slot, float generatorOutput, float frequencyRegulator, float loadDistribution)
        {
            if (!HasValidNetworkObject()) return false;
            if (Object.HasStateAuthority)
            {
                if (!TryGetLocalRequester(out var requester)) return false;
                return TryApplyRelayAControlsAuthoritative(
                    requester, slot, generatorOutput, frequencyRegulator, loadDistribution);
            }
            RpcRelayAControls((int)slot, generatorOutput, frequencyRegulator, loadDistribution);
            return true;
        }

        public bool RequestRelayAStart(RelaySlot slot)
        {
            if (!HasValidNetworkObject()) return false;
            if (Object.HasStateAuthority)
            {
                return TryGetLocalRequester(out var requester)
                    && TrySetRelayARunningAuthoritative(requester, slot, true);
            }
            RpcRelayAStart((int)slot);
            return true;
        }

        public bool RequestRelayAEmergencyStop(RelaySlot slot)
        {
            if (!HasValidNetworkObject()) return false;
            if (Object.HasStateAuthority)
            {
                return TryGetLocalRequester(out var requester)
                    && TrySetRelayARunningAuthoritative(requester, slot, false);
            }
            RpcRelayAEmergencyStop((int)slot);
            return true;
        }

        public bool RequestRelayBControls(RelaySlot slot, int channel, float frequency, float phase)
        {
            if (!HasValidNetworkObject()) return false;
            if (Object.HasStateAuthority)
            {
                return TryGetLocalRequester(out var requester)
                    && TryApplyRelayBControlsAuthoritative(requester, slot, channel, frequency, phase);
            }
            RpcRelayBControls((int)slot, channel, frequency, phase);
            return true;
        }

        public bool RequestRelayBScan(RelaySlot slot)
        {
            if (!HasValidNetworkObject()) return false;
            if (Object.HasStateAuthority)
            {
                return TryGetLocalRequester(out var requester)
                    && TryRelayBActionAuthoritative(requester, slot, RelayBAction.Scan);
            }
            RpcRelayBScan((int)slot);
            return true;
        }

        public bool RequestRelayBStartSync(RelaySlot slot)
        {
            if (!HasValidNetworkObject()) return false;
            if (Object.HasStateAuthority)
            {
                return TryGetLocalRequester(out var requester)
                    && TryRelayBActionAuthoritative(requester, slot, RelayBAction.StartSync);
            }
            RpcRelayBStartSync((int)slot);
            return true;
        }

        public bool RequestRelayBCancelSync(RelaySlot slot)
        {
            if (!HasValidNetworkObject()) return false;
            if (Object.HasStateAuthority)
            {
                return TryGetLocalRequester(out var requester)
                    && TryRelayBActionAuthoritative(requester, slot, RelayBAction.CancelSync);
            }
            RpcRelayBCancelSync((int)slot);
            return true;
        }

        public bool RequestStartSecurityHold()
        {
            if (!HasValidNetworkObject()) return false;
            if (Object.HasStateAuthority)
            {
                return TryGetLocalRequester(out var requester)
                    && TryStartSecurityHoldAuthoritative(requester);
            }
            RpcStartSecurityHold();
            return true;
        }

        public void RequestCancelSecurityHold()
        {
            if (!HasValidNetworkObject()) return;
            if (Object.HasStateAuthority)
            {
                if (TryGetLocalRequester(out var requester)) PauseSecurityHoldAuthoritative(requester);
            }
            else RpcCancelSecurityHold();
        }

        public void RequestRefreshSecurityHold()
        {
            if (!HasValidNetworkObject()) return;
            if (Object.HasStateAuthority)
            {
                if (TryGetLocalRequester(out var requester)) RefreshSecurityHoldAuthoritative(requester);
            }
            else RpcRefreshSecurityHold();
        }

        public Zone2AccessSubmissionDisposition RequestSubmitZoneAccessCode(int panelIndex, string code)
        {
            if (!HasValidNetworkObject()) return Zone2AccessSubmissionDisposition.Rejected;
            if (Object.HasStateAuthority)
            {
                if (!TryGetLocalRequester(out var requester)) return Zone2AccessSubmissionDisposition.Rejected;
                TrySubmitZoneAccessCodeAuthoritative(requester, panelIndex, code, out var result);
                float cooldownSeconds = result == Zone2NetworkCommandResult.Cooldown
                    ? ZoneAccessCooldownRemainingSeconds
                    : 0f;

                LocalZoneAccessCodeRequestCompleted?.Invoke(
                    new Zone2AccessCodeResult(
                        panelIndex,
                        result,
                        cooldownSeconds));

                return result == Zone2NetworkCommandResult.Accepted
                    ? Zone2AccessSubmissionDisposition.Accepted
                    : Zone2AccessSubmissionDisposition.Rejected;
            }
            RpcSubmitZoneAccessCode(panelIndex, code);
            return Zone2AccessSubmissionDisposition.Pending;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcAcquireRelay(int relaySlot, RpcInfo info = default) =>
            TryAcquireRelayAuthoritative(info.Source, (RelaySlot)relaySlot);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcReleaseRelay(int relaySlot, RpcInfo info = default) =>
            ReleaseRelayAuthoritative(info.Source, (RelaySlot)relaySlot);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRelayAControls(int relaySlot, float generatorOutput, float frequencyRegulator, float loadDistribution, RpcInfo info = default) =>
            TryApplyRelayAControlsAuthoritative(info.Source, (RelaySlot)relaySlot, generatorOutput, frequencyRegulator, loadDistribution);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRelayAStart(int relaySlot, RpcInfo info = default) =>
            TrySetRelayARunningAuthoritative(info.Source, (RelaySlot)relaySlot, true);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRelayAEmergencyStop(int relaySlot, RpcInfo info = default) =>
            TrySetRelayARunningAuthoritative(info.Source, (RelaySlot)relaySlot, false);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRelayBControls(int relaySlot, int channel, float frequency, float phase, RpcInfo info = default) =>
            TryApplyRelayBControlsAuthoritative(info.Source, (RelaySlot)relaySlot, channel, frequency, phase);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRelayBScan(int relaySlot, RpcInfo info = default) =>
            TryRelayBActionAuthoritative(info.Source, (RelaySlot)relaySlot, RelayBAction.Scan);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRelayBStartSync(int relaySlot, RpcInfo info = default) =>
            TryRelayBActionAuthoritative(info.Source, (RelaySlot)relaySlot, RelayBAction.StartSync);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRelayBCancelSync(int relaySlot, RpcInfo info = default) =>
            TryRelayBActionAuthoritative(info.Source, (RelaySlot)relaySlot, RelayBAction.CancelSync);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcStartSecurityHold(RpcInfo info = default) => TryStartSecurityHoldAuthoritative(info.Source);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcCancelSecurityHold(RpcInfo info = default) => PauseSecurityHoldAuthoritative(info.Source);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRefreshSecurityHold(RpcInfo info = default) => RefreshSecurityHoldAuthoritative(info.Source);

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcSubmitZoneAccessCode(int panelIndex, string code, RpcInfo info = default)
        {
            TrySubmitZoneAccessCodeAuthoritative(info.Source, panelIndex, code, out var result);
            float cooldownSeconds = result == Zone2NetworkCommandResult.Cooldown
                ? ZoneAccessCooldownRemainingSeconds
                : 0f;

            if (info.Source.IsRealPlayer) RpcZoneAccessCodeResult(info.Source, panelIndex, (int)result, cooldownSeconds);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcZoneAccessCodeResult([RpcTarget] PlayerRef target, int panelIndex, int result, float cooldownSeconds)
        {
            LocalZoneAccessCodeRequestCompleted?.Invoke(
                new Zone2AccessCodeResult(
                    panelIndex,
                    (Zone2NetworkCommandResult)result,
                    cooldownSeconds));
        }

        private bool TryAcquireRelayAuthoritative(PlayerRef requester, RelaySlot slot)
        {
            if (!TryValidateRelayCommand(requester, slot, out var target)) return false;
            var current = GetRelayOperator(slot);
            if (!current.IsNone && current != requester) return false;
            SetRelayOperator(slot, requester);
            EmitRelayInteractionNoiseAuthoritative(requester, slot, target);
            HandleReplicatedStateChanged();
            return true;
        }

        private void ReleaseRelayAuthoritative(PlayerRef requester, RelaySlot slot)
        {
            if (Object == null
                || !Object.HasStateAuthority
                || !IsValidRelaySlot(slot)
                || !requester.IsRealPlayer
                || GetRelayOperator(slot) != requester)
            {
                return;
            }

            SetRelayOperator(
                slot,
                PlayerRef.None);

            HandleReplicatedStateChanged();
        }

        private bool TryApplyRelayAControlsAuthoritative(
            PlayerRef requester, RelaySlot slot, float generatorOutput, float frequencyRegulator, float loadDistribution)
        {
            if (!IsRelayASlot(slot) || !TryValidateRelayCommand(requester, slot, out var target)) return false;
            if (!float.IsFinite(generatorOutput) || !float.IsFinite(frequencyRegulator) || !float.IsFinite(loadDistribution)) return false;
            if (!ClaimRelayOperator(requester, slot)) return false;
            var controller = (RelayAController)target;
            controller.SetControls(generatorOutput, frequencyRegulator, loadDistribution);
            var controls = controller.Snapshot.Controls;
            if (slot == RelaySlot.RelayA_1) RelayA1Controls = controls;
            else RelayA2Controls = controls;
            HandleReplicatedStateChanged();
            return true;
        }

        private bool TrySetRelayARunningAuthoritative(PlayerRef requester, RelaySlot slot, bool running)
        {
            if (!IsRelayASlot(slot) || !TryValidateRelayCommand(requester, slot, out var target)) return false;
            if (!ClaimRelayOperator(requester, slot)) return false;
            if (slot == RelaySlot.RelayA_1) RelayA1Running = running;
            else RelayA2Running = running;
            if (running) ((RelayAController)target).StartStabilization();
            else ((RelayAController)target).EmergencyStop();
            HandleReplicatedStateChanged();
            return true;
        }

        private bool TryApplyRelayBControlsAuthoritative(
            PlayerRef requester, RelaySlot slot, int channel, float frequency, float phase)
        {
            if (!IsRelayBSlot(slot) || channel < -1 || channel > 3
                || !float.IsFinite(frequency) || !float.IsFinite(phase)
                || !TryValidateRelayCommand(requester, slot, out var target)) return false;
            if (!ClaimRelayOperator(requester, slot)) return false;
            var controller = (RelayBController)target;
            controller.ApplyAuthoritativeControls(channel, frequency, phase);
            var snapshot = controller.Snapshot;
            if (slot == RelaySlot.RelayB_1)
            {
                RelayB1Channel = snapshot.SelectedChannelIndex;
                RelayB1Frequency = snapshot.CurrentFrequency;
                RelayB1Phase = snapshot.CurrentPhase;
            }
            else
            {
                RelayB2Channel = snapshot.SelectedChannelIndex;
                RelayB2Frequency = snapshot.CurrentFrequency;
                RelayB2Phase = snapshot.CurrentPhase;
            }
            HandleReplicatedStateChanged();
            return true;
        }

        private bool TryRelayBActionAuthoritative(PlayerRef requester, RelaySlot slot, RelayBAction action)
        {
            if (!IsRelayBSlot(slot) || !TryValidateRelayCommand(requester, slot, out var target)) return false;
            var controller = (RelayBController)target;
            if (action == RelayBAction.StartSync && controller.Snapshot.SelectedChannelIndex < 0) return false;
            if (!ClaimRelayOperator(requester, slot)) return false;
            switch (action)
            {
                case RelayBAction.Scan:
                    controller.ScanChannels();
                    break;
                case RelayBAction.StartSync:
                    SetRelayActiveState(slot, true);
                    controller.StartSynchronization();
                    break;
                default:
                    SetRelayActiveState(slot, false);
                    controller.CancelSynchronization();
                    break;
            }
            HandleReplicatedStateChanged();
            return true;
        }

        private bool TryStartSecurityHoldAuthoritative(PlayerRef requester)
        {
            if (!Object.HasStateAuthority || IsEnded || CurrentPhase != NetworkMatchPhase.Zone2Objective
                || !AreAllRelaysOnline || SecurityHoldCompleted
                || (Zone2Stage != Zone2MissionStage.SecurityHoldReady && Zone2Stage != Zone2MissionStage.SecurityHold)
                || !TryGetZone2Director(out var director)
                || director.SecurityTerminal == null
                || !TryValidateZone2Requester(requester, director.SecurityTerminal, director.SecurityTerminal.MaxInteractorDistance)) return false;

            if (HasSecurityHoldParticipant(requester)) return true;
            if (SecurityHoldParticipantCount >= 4) return false;

            SecurityHoldDurationSeconds = director.SecurityTerminal.DownloadDurationSeconds;
            SecurityHoldAccumulatedSeconds = Mathf.Clamp(SecurityHoldAccumulatedSeconds, 0f, SecurityHoldDurationSeconds);
            for (int index = 0; index < 4; index++)
            {
                if (!GetSecurityHoldParticipant(index).IsNone) continue;
                SetSecurityHoldParticipant(index, requester);
                _securityHoldLeases[index] = TickTimer.CreateFromSeconds(Runner, 1.25f);
                break;
            }
            Zone2Stage = Zone2MissionStage.SecurityHold;
            HandleReplicatedStateChanged();
            return true;
        }

        private void PauseSecurityHoldAuthoritative(PlayerRef requester)
        {
            if (Object == null
                || !Object.HasStateAuthority
                || IsEnded
                || CurrentPhase != NetworkMatchPhase.Zone2Objective
                || Zone2Stage != Zone2MissionStage.SecurityHold
                || !requester.IsRealPlayer)
            {
                return;
            }

            if (RemoveSecurityHoldParticipant(requester)) HandleReplicatedStateChanged();
        }

        private bool HasSecurityHoldParticipant(PlayerRef player)
        {
            if (!player.IsRealPlayer) return false;
            for (int index = 0; index < 4; index++)
                if (GetSecurityHoldParticipant(index) == player) return true;
            return false;
        }

        private void RefreshSecurityHoldAuthoritative(PlayerRef requester)
        {
            if (!Object.HasStateAuthority || Zone2Stage != Zone2MissionStage.SecurityHold) return;
            for (int index = 0; index < 4; index++)
            {
                if (GetSecurityHoldParticipant(index) != requester) continue;
                _securityHoldLeases[index] = TickTimer.CreateFromSeconds(Runner, 1.25f);
                return;
            }
        }

        private PlayerRef GetSecurityHoldParticipant(int index) => index switch
        {
            0 => SecurityHoldOperator,
            1 => SecurityHoldOperator2,
            2 => SecurityHoldOperator3,
            3 => SecurityHoldOperator4,
            _ => PlayerRef.None
        };

        private void SetSecurityHoldParticipant(int index, PlayerRef player)
        {
            if (player.IsNone) _securityHoldLeases[index] = TickTimer.None;
            switch (index)
            {
                case 0: SecurityHoldOperator = player; break;
                case 1: SecurityHoldOperator2 = player; break;
                case 2: SecurityHoldOperator3 = player; break;
                case 3: SecurityHoldOperator4 = player; break;
            }
        }

        private bool RemoveSecurityHoldParticipant(PlayerRef player)
        {
            if (!player.IsRealPlayer) return false;
            for (int index = 0; index < 4; index++)
            {
                if (GetSecurityHoldParticipant(index) != player) continue;
                SetSecurityHoldParticipant(index, PlayerRef.None);
                if (SecurityHoldParticipantCount == 0 && !SecurityHoldCompleted)
                    Zone2Stage = Zone2MissionStage.SecurityHoldReady;
                return true;
            }
            return false;
        }

        private void ClearSecurityHoldParticipants()
        {
            for (int index = 0; index < 4; index++)
                SetSecurityHoldParticipant(index, PlayerRef.None);
        }

        private void AdvanceSecurityHoldAuthoritative()
        {
            if (!IsSecurityHoldRunning) return;
            if (!TryGetZone2Director(out var director) || director.SecurityTerminal == null)
            {
                ClearSecurityHoldParticipants();
                Zone2Stage = Zone2MissionStage.SecurityHoldReady;
                HandleReplicatedStateChanged();
                return;
            }

            bool changed = false;
            for (int index = 0; index < 4; index++)
            {
                var player = GetSecurityHoldParticipant(index);
                if (player.IsNone || (!_securityHoldLeases[index].ExpiredOrNotRunning(Runner)
                    && TryValidateZone2Requester(player, director.SecurityTerminal,
                        director.SecurityTerminal.MaxInteractorDistance))) continue;
                SetSecurityHoldParticipant(index, PlayerRef.None);
                changed = true;
            }
            int participants = SecurityHoldParticipantCount;
            if (participants == 0)
            {
                Zone2Stage = Zone2MissionStage.SecurityHoldReady;
                HandleReplicatedStateChanged();
                return;
            }

            SecurityHoldAccumulatedSeconds = Mathf.Min(SecurityHoldDurationSeconds,
                SecurityHoldAccumulatedSeconds + Runner.DeltaTime * SecurityHoldWorkRate(participants));
            if (SecurityHoldAccumulatedSeconds >= SecurityHoldDurationSeconds)
                CompleteSecurityHoldAuthoritative();
            else if (changed) HandleReplicatedStateChanged();
        }

        public static float SecurityHoldWorkRate(int participants) => participants switch
        {
            1 => 1f,
            2 => 1.2f,
            3 => 1.5f,
            4 => 2f,
            _ => 0f
        };

        private bool CompleteSecurityHoldAuthoritative()
        {
            if (!Object.HasStateAuthority || IsEnded || CurrentPhase != NetworkMatchPhase.Zone2Objective
                || !AreAllRelaysOnline || Zone2Stage != Zone2MissionStage.SecurityHold || SecurityHoldCompleted
                || SecurityHoldDurationSeconds <= 0f
                || SecurityHoldAccumulatedSeconds < SecurityHoldDurationSeconds) return false;

            SecurityHoldCompleted = true;
            PowerAuthorizationAvailable = true;
            SecurityHoldAccumulatedSeconds = SecurityHoldDurationSeconds;
            RelayRepairWindowTimer = TickTimer.None;
            RelayRepairResetNoticeTimer = TickTimer.None;
            ClearSecurityHoldParticipants();
            if (string.IsNullOrEmpty(_serverGeneratedAuthCode))
            {
                _serverGeneratedAuthCode = UnityEngine.Random.Range(0, 10000).ToString("D4");
            }
            PowerAuthorizationCode = _serverGeneratedAuthCode;
            Zone2Stage = Zone2MissionStage.AuthorizationCodeGranted;
            HandleReplicatedStateChanged();
            return true;
        }

        private bool TrySubmitZoneAccessCodeAuthoritative(
            PlayerRef requester, int panelIndex, string code, out Zone2NetworkCommandResult result)
        {
            result = Zone2NetworkCommandResult.InvalidStage;
            if (!Object.HasStateAuthority || IsEnded || CurrentPhase != NetworkMatchPhase.Zone2Objective
                || !SecurityHoldCompleted || !PowerAuthorizationAvailable
                || Zone2Stage != Zone2MissionStage.AuthorizationCodeGranted)
            {
                return false;
            }
            if (ZoneDoorsUnlocked)
            {
                result = Zone2NetworkCommandResult.AlreadyComplete;
                return false;
            }
            if (!TryGetDistributionPanelTarget(panelIndex, out var panel))
            {
                result = Zone2NetworkCommandResult.InvalidTarget;
                return false;
            }
            result = ValidateZone2Requester(requester, panel, _zone2InteractionDistance);
            if (result != Zone2NetworkCommandResult.Accepted)
            {
                return false;
            }
            if (IsZoneAccessCooldownActive)
            {
                result = Zone2NetworkCommandResult.Cooldown;
                return false;
            }
            if (string.IsNullOrEmpty(code) || code.Length != 4 || code != _serverGeneratedAuthCode)
            {
                ZoneAccessFailureCount++;
                bool startedCooldown = false;

                if (ZoneAccessFailureCount >= Mathf.Max(1, _zoneAccessFailuresBeforeCooldown))
                {
                    ZoneAccessFailureCount = 0;
                    ZoneAccessCooldown = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0.1f, _zoneAccessCooldownSeconds));
                    startedCooldown = true;
                }

                result = startedCooldown
                    ? Zone2NetworkCommandResult.Cooldown
                    : Zone2NetworkCommandResult.InvalidCode;

                HandleReplicatedStateChanged();
                return false;
            }
            if (!NetworkMatchStateRules.CanAdvance(
                    Status, CurrentPhase, NetworkMatchPhase.Zone2Objective, NetworkMatchPhase.FinalHunt)) return false;

            int previousFailureCount = ZoneAccessFailureCount;
            TickTimer previousCooldown = ZoneAccessCooldown;
            ZoneAccessFailureCount = 0;
            ZoneAccessCooldown = TickTimer.None;
            ZoneDoorsUnlocked = true;
            PowerPuzzleCompleted = true;
            RestoreMainPowerCompleted = true;
            Zone2Stage = Zone2MissionStage.Zone2Completed;
            if (!TryAdvancePhase(NetworkMatchPhase.Zone2Objective, NetworkMatchPhase.FinalHunt, "ZONE2_OBJECTIVE"))
            {
                ZoneDoorsUnlocked = false;
                PowerPuzzleCompleted = false;
                RestoreMainPowerCompleted = false;
                Zone2Stage = Zone2MissionStage.AuthorizationCodeGranted;
                ZoneAccessFailureCount = previousFailureCount;
                ZoneAccessCooldown = previousCooldown;
                HandleReplicatedStateChanged();
                return false;
            }
            result = Zone2NetworkCommandResult.Accepted;
            return true;
        }

        private enum RelayBAction { Scan, StartSync, CancelSync }

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
            RuntimeLog.Log(
                RuntimeLogCategory.MatchState,
                $"[MatchState] Escape started by {actor}; duration={CurrentScenarioEscapeDoorTimerSeconds:0.##}s.");
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
            RuntimeLog.Log(
                RuntimeLogCategory.MatchState,
                $"[MatchState] Phase {expected} -> {next}.");
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
            RuntimeLog.Log(
                RuntimeLogCategory.MatchState,

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

        private void ResetZone2AuthoritativeState()
        {
            RelayCompletionMask = 0;
            SecurityTerminalDiscovered = false;
            SecurityHoldCompleted = false;
            PowerAuthorizationAvailable = false;
            PowerAuthorizationCode = string.Empty;
            ZoneDoorsUnlocked = false;
            PowerPuzzleCompleted = false;
            RestoreMainPowerCompleted = false;
            ClearSecurityHoldParticipants();
            SecurityHoldDurationSeconds = 0f;
            SecurityHoldAccumulatedSeconds = 0f;
            RelayRepairWindowTimer = TickTimer.None;
            RelayRepairResetNoticeTimer = TickTimer.None;
            RelayA1Operator = PlayerRef.None;
            RelayA2Operator = PlayerRef.None;
            RelayB1Operator = PlayerRef.None;
            RelayB2Operator = PlayerRef.None;
            _relayA1NoiseTimer = TickTimer.None;
            _relayA2NoiseTimer = TickTimer.None;
            _relayB1NoiseTimer = TickTimer.None;
            _relayB2NoiseTimer = TickTimer.None;
            _relayA1NoiseSequence = 0;
            _relayA2NoiseSequence = 0;
            _relayB1NoiseSequence = 0;
            _relayB2NoiseSequence = 0;
            RelayA1AttemptSeed = 0;
            RelayA2AttemptSeed = 0;
            RelayB1AttemptSeed = 0;
            RelayB2AttemptSeed = 0;
            RelayA1Controls = Vector3.zero;
            RelayA2Controls = Vector3.zero;
            RelayA1Running = false;
            RelayA2Running = false;
            RelayB1PresetIndex = 0;
            RelayB2PresetIndex = 0;
            RelayB1Channel = -1;
            RelayB2Channel = -1;
            RelayB1Frequency = 0f;
            RelayB2Frequency = 0f;
            RelayB1Phase = 0f;
            RelayB2Phase = 0f;
            RelayB1Synchronizing = false;
            RelayB2Synchronizing = false;
            ZoneAccessFailureCount = 0;
            ZoneAccessCooldown = TickTimer.None;
            Zone2RelayRuntimeInitialized = false;
        }

        private bool ShouldResetRelayRepairForSecurityHoldTimeout()
        {
            return Object.HasStateAuthority
                && CurrentPhase == NetworkMatchPhase.Zone2Objective
                && !SecurityHoldCompleted
                && AreAllRelaysOnline
                && RelayRepairWindowTimer.IsRunning
                && RelayRepairWindowTimer.Expired(Runner);
        }

        private void ResetRelayRepairForRetryAuthoritative()
        {
            RelayCompletionMask = 0;
            Zone2Stage = Zone2MissionStage.RepairRelays;
            RelayRepairResetNoticeTimer = TickTimer.CreateFromSeconds(Runner, 6f);
            ClearSecurityHoldParticipants();
            SecurityHoldDurationSeconds = 0f;
            SecurityHoldAccumulatedSeconds = 0f;
            RelayRepairWindowTimer = TickTimer.None;
            RelayA1Operator = PlayerRef.None;
            RelayA2Operator = PlayerRef.None;
            RelayB1Operator = PlayerRef.None;
            RelayB2Operator = PlayerRef.None;
            RelayA1AttemptSeed = 0;
            RelayA2AttemptSeed = 0;
            RelayB1AttemptSeed = 0;
            RelayB2AttemptSeed = 0;
            RelayA1Running = false;
            RelayA2Running = false;
            RelayB1Synchronizing = false;
            RelayB2Synchronizing = false;

            if (TryGetZone2Director(out var director))
            {
                director.SecurityTerminal?.ResetDownload();
                RelayA1AttemptSeed = NewRelayAttemptSeed();
                RelayA2AttemptSeed = NewRelayAttemptSeed();
                RelayB1AttemptSeed = NewRelayAttemptSeed();
                RelayB2AttemptSeed = NewRelayAttemptSeed();

                director.RelayA1?.ResetForRetry(RelayA1AttemptSeed);
                director.RelayA2?.ResetForRetry(RelayA2AttemptSeed);

                RelayB1PresetIndex = ChooseRelayBPreset(director.RelayB1);
                director.RelayB1?.ResetForRetry(RelayB1PresetIndex, RelayB1AttemptSeed);
                RelayB1Channel = director.RelayB1 != null ? director.RelayB1.Snapshot.SelectedChannelIndex : -1;
                RelayB1Frequency = director.RelayB1 != null ? director.RelayB1.Snapshot.CurrentFrequency : 0f;
                RelayB1Phase = director.RelayB1 != null ? director.RelayB1.Snapshot.CurrentPhase : 0f;

                RelayB2PresetIndex = ChooseRelayBPreset(director.RelayB2, director.RelayB1, RelayB1PresetIndex);
                director.RelayB2?.ResetForRetry(RelayB2PresetIndex, RelayB2AttemptSeed);
                RelayB2Channel = director.RelayB2 != null ? director.RelayB2.Snapshot.SelectedChannelIndex : -1;
                RelayB2Frequency = director.RelayB2 != null ? director.RelayB2.Snapshot.CurrentFrequency : 0f;
                RelayB2Phase = director.RelayB2 != null ? director.RelayB2.Snapshot.CurrentPhase : 0f;

                RelayA1Controls = director.RelayA1 != null ? director.RelayA1.Snapshot.Controls : Vector3.zero;
                RelayA2Controls = director.RelayA2 != null ? director.RelayA2.Snapshot.Controls : Vector3.zero;
            }

            HandleReplicatedStateChanged();
        }

        private bool InitializeZone2RelayRuntimeAuthoritative()
        {
            if (Zone2RelayRuntimeInitialized) return true;
            if (!Object.HasStateAuthority || !TryGetZone2Director(out var director)
                || director.RelayA1 == null || director.RelayA2 == null
                || director.RelayB1 == null || director.RelayB2 == null) return false;

            RelayA1AttemptSeed = NewRelayAttemptSeed();
            director.RelayA1.ApplyAuthoritativeAttemptSeed(RelayA1AttemptSeed);
            var a1 = director.RelayA1.Snapshot;
            RelayA1Controls = a1.Controls;
            RelayA1Running = a1.IsRunning;
            RelayA2AttemptSeed = NewRelayAttemptSeed();
            director.RelayA2.ApplyAuthoritativeAttemptSeed(RelayA2AttemptSeed);
            var a2 = director.RelayA2.Snapshot;
            RelayA2Controls = a2.Controls;
            RelayA2Running = a2.IsRunning;

            RelayB1PresetIndex = ChooseRelayBPreset(director.RelayB1);
            RelayB1AttemptSeed = NewRelayAttemptSeed();
            director.RelayB1.ApplyAuthoritativeAttempt(RelayB1PresetIndex, RelayB1AttemptSeed);
            var b1 = director.RelayB1.Snapshot;
            RelayB1Channel = b1.SelectedChannelIndex;
            RelayB1Frequency = b1.CurrentFrequency;
            RelayB1Phase = b1.CurrentPhase;
            RelayB1Synchronizing = false;

            RelayB2PresetIndex = ChooseRelayBPreset(director.RelayB2, director.RelayB1, RelayB1PresetIndex);
            RelayB2AttemptSeed = NewRelayAttemptSeed();
            director.RelayB2.ApplyAuthoritativeAttempt(RelayB2PresetIndex, RelayB2AttemptSeed);
            var b2 = director.RelayB2.Snapshot;
            RelayB2Channel = b2.SelectedChannelIndex;
            RelayB2Frequency = b2.CurrentFrequency;
            RelayB2Phase = b2.CurrentPhase;
            RelayB2Synchronizing = false;
            Zone2RelayRuntimeInitialized = true;
            return true;
        }

        private static int ChooseRelayBPreset(RelayBController controller,
            RelayBController otherController = null, int otherPresetIndex = -1)
        {
            int count = controller != null && controller.Config != null && controller.Config.Presets != null
                ? controller.Config.Presets.Count
                : 0;
            if (count <= 1) return 0;

            RelayBPreset other = otherController != null && otherController.Config != null && otherPresetIndex >= 0
                ? otherController.Config.GetPreset(otherPresetIndex) : null;
            int selected = -1;
            int candidates = 0;
            for (int index = 0; index < count; index++)
            {
                var preset = controller.Config.GetPreset(index);
                if (other != null && preset != null
                    && preset.CorrectChannelIndex == other.CorrectChannelIndex
                    && preset.ReferenceWaveform == other.ReferenceWaveform
                    && Mathf.Approximately(preset.TargetFrequency, other.TargetFrequency)
                    && Mathf.Approximately(preset.TargetPhase, other.TargetPhase)) continue;
                if (UnityEngine.Random.Range(0, ++candidates) == 0) selected = index;
            }
            return selected >= 0 ? selected : UnityEngine.Random.Range(0, count);
        }

        private static int NewRelayAttemptSeed()
        {
            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            return seed == 0 ? 1 : seed;
        }

        private bool TryGetZone2Director(out Zone2MissionDirector director)
        {
            director = Zone2MissionDirector.Instance;
            if (director == null) return false;
            director.ResolveReferences();
            return true;
        }

        private bool TryGetRelayTarget(RelaySlot slot, out Component target)
        {
            target = null;
            if (!IsValidRelaySlot(slot) || !TryGetZone2Director(out var director)) return false;
            target = slot switch
            {
                RelaySlot.RelayA_1 => director.RelayA1,
                RelaySlot.RelayA_2 => director.RelayA2,
                RelaySlot.RelayB_1 => director.RelayB1,
                RelaySlot.RelayB_2 => director.RelayB2,
                _ => null,
            };
            return target != null;
        }

        private bool TryGetDistributionPanelTarget(int panelIndex, out PowerControlUIController panel)
        {
            panel = null;
            if ((panelIndex != 0 && panelIndex != 1) || !TryGetZone2Director(out var director)) return false;
            panel = panelIndex == 0 ? director.DistributionPanel1 : director.DistributionPanel2;
            return panel != null;
        }

        private bool TryValidateZone2Requester(PlayerRef requester, Component target, float maxDistance)
        {
            return ValidateZone2Requester(requester, target, maxDistance) == Zone2NetworkCommandResult.Accepted;
        }

        private Zone2NetworkCommandResult ValidateZone2Requester(PlayerRef requester, Component target, float maxDistance)
        {
            if (target == null) return Zone2NetworkCommandResult.InvalidTarget;
            if (!requester.IsRealPlayer || Runner == null
                || !Runner.TryGetPlayerObject(requester, out var playerObject)
                || playerObject == null || !playerObject.IsValid || playerObject.InputAuthority != requester
                || !TryResolveActivePlayer(requester, out _)) return Zone2NetworkCommandResult.InvalidRequester;

            Vector3 requesterPosition = playerObject.transform.position;
            float closestSqrDistance = float.PositiveInfinity;
            bool foundCollider = false;
            foreach (var targetCollider in target.GetComponentsInChildren<Collider>(true))
            {
                if (targetCollider == null || !targetCollider.enabled) continue;
                foundCollider = true;
                float sqrDistance = (targetCollider.ClosestPoint(requesterPosition) - requesterPosition).sqrMagnitude;
                if (sqrDistance < closestSqrDistance) closestSqrDistance = sqrDistance;
            }
            if (!foundCollider)
            {
                closestSqrDistance = (target.transform.position - requesterPosition).sqrMagnitude;
            }
            float distance = Mathf.Max(0.1f, maxDistance);
            return closestSqrDistance <= distance * distance
                ? Zone2NetworkCommandResult.Accepted
                : Zone2NetworkCommandResult.OutOfRange;
        }

        private bool TryValidateRelayCommand(PlayerRef requester, RelaySlot slot, out Component target)
        {
            target = null;
            if (!Object.HasStateAuthority || IsEnded || CurrentPhase != NetworkMatchPhase.Zone2Objective
                || Zone2Stage != Zone2MissionStage.RepairRelays || !IsValidRelaySlot(slot)
                || (RelayCompletionMask & (1 << (int)slot)) != 0
                || !TryGetRelayTarget(slot, out target)) return false;
            return TryValidateZone2Requester(requester, target, _zone2InteractionDistance);
        }

        private void ReleaseInvalidRelayOperatorsAuthoritative()
        {
            if (Object == null || !Object.HasStateAuthority)
            {
                return;
            }

            bool changed = false;

            for (int index = 0; index < 4; index++)
            {
                var slot = (RelaySlot)index;
                var currentOperator = GetRelayOperator(slot);

                if (currentOperator.IsNone)
                {
                    continue;
                }

                bool relayStillAvailable =
                    CurrentPhase == NetworkMatchPhase.Zone2Objective
                    && Zone2Stage == Zone2MissionStage.RepairRelays
                    && (RelayCompletionMask & (1 << index)) == 0;

                bool operatorStillValid = false;

                if (relayStillAvailable
                    && TryGetRelayTarget(
                        slot,
                        out var target))
                {
                    operatorStillValid =
                        ValidateZone2Requester(
                            currentOperator,
                            target,
                            _zone2InteractionDistance)
                        == Zone2NetworkCommandResult.Accepted;
                }

                if (operatorStillValid)
                {
                    continue;
                }

                SetRelayOperator(
                    slot,
                    PlayerRef.None);

                changed = true;
            }

            if (changed)
            {
                HandleReplicatedStateChanged();
            }
        }

        public void ReleasePlayerOperationsAuthoritative(PlayerRef player)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority || !player.IsRealPlayer) return;
            bool changed = false;
            for (int index = 0; index < 4; index++)
            {
                var slot = (RelaySlot)index;
                if (GetRelayOperator(slot) != player) continue;
                SetRelayOperator(slot, PlayerRef.None);
                changed = true;
            }
            if (RemoveSecurityHoldParticipant(player)) changed = true;
            if (changed) HandleReplicatedStateChanged();
        }

        private bool ClaimRelayOperator(PlayerRef requester, RelaySlot slot)
        {
            var current = GetRelayOperator(slot);
            if (!current.IsNone && current != requester) return false;
            if (current.IsNone) SetRelayOperator(slot, requester);
            return true;
        }

        private PlayerRef GetRelayOperator(RelaySlot slot) => slot switch
        {
            RelaySlot.RelayA_1 => RelayA1Operator,
            RelaySlot.RelayA_2 => RelayA2Operator,
            RelaySlot.RelayB_1 => RelayB1Operator,
            RelaySlot.RelayB_2 => RelayB2Operator,
            _ => PlayerRef.None,
        };

        private void SetRelayOperator(RelaySlot slot, PlayerRef player)
        {
            switch (slot)
            {
                case RelaySlot.RelayA_1: RelayA1Operator = player; break;
                case RelaySlot.RelayA_2: RelayA2Operator = player; break;
                case RelaySlot.RelayB_1: RelayB1Operator = player; break;
                case RelaySlot.RelayB_2: RelayB2Operator = player; break;
            }
        }

        private void SetRelayActiveState(RelaySlot slot, bool active)
        {
            switch (slot)
            {
                case RelaySlot.RelayA_1: RelayA1Running = active; break;
                case RelaySlot.RelayA_2: RelayA2Running = active; break;
                case RelaySlot.RelayB_1: RelayB1Synchronizing = active; break;
                case RelaySlot.RelayB_2: RelayB2Synchronizing = active; break;
            }
        }

        private void EmitRelayRepairNoiseAuthoritative()
        {
            if (!RelayNoiseCatalog.TryGetDefinition(
                    RuntimeNoiseType.MACHINE_REPAIR,
                    out var definition)
                || definition.PulseInterval <= TimeSpan.Zero)
            {
                return;
            }

            EmitRelayRepairNoise(
                RelaySlot.RelayA_1,
                RelayA1Operator,
                RelayA1Running,
                ref _relayA1NoiseTimer,
                ref _relayA1NoiseSequence,
                definition.PulseInterval);
            EmitRelayRepairNoise(
                RelaySlot.RelayA_2,
                RelayA2Operator,
                RelayA2Running,
                ref _relayA2NoiseTimer,
                ref _relayA2NoiseSequence,
                definition.PulseInterval);
            EmitRelayRepairNoise(
                RelaySlot.RelayB_1,
                RelayB1Operator,
                RelayB1Synchronizing,
                ref _relayB1NoiseTimer,
                ref _relayB1NoiseSequence,
                definition.PulseInterval);
            EmitRelayRepairNoise(
                RelaySlot.RelayB_2,
                RelayB2Operator,
                RelayB2Synchronizing,
                ref _relayB2NoiseTimer,
                ref _relayB2NoiseSequence,
                definition.PulseInterval);
        }

        private void EmitRelayInteractionNoiseAuthoritative(
            PlayerRef actor,
            RelaySlot slot,
            Component target)
        {
            if (!actor.IsRealPlayer
                || target == null
                || MatchAuthorityRuntime.Instance == null
                || MatchAuthorityRuntime.Instance.MatchId == Guid.Empty)
            {
                return;
            }

            var authority = MatchAuthorityRuntime.Instance;
            var sequence = slot switch
            {
                RelaySlot.RelayA_1 => ++_relayA1NoiseSequence,
                RelaySlot.RelayA_2 => ++_relayA2NoiseSequence,
                RelaySlot.RelayB_1 => ++_relayB1NoiseSequence,
                RelaySlot.RelayB_2 => ++_relayB2NoiseSequence,
                _ => 0,
            };
            if (sequence <= 0)
            {
                return;
            }

            var key = new RuntimeNoiseSourceOccurrenceKey(
                $"relay-interaction:{authority.MatchId:D}:{slot}",
                sequence);
            var noiseService = HostRuntimeNoiseService.EnsureExists(authority);
            if (!noiseService.TryAccept(
                    actor,
                    RuntimeNoiseType.INTERACTION,
                    key,
                    target.transform.position,
                    out _))
            {
                switch (slot)
                {
                    case RelaySlot.RelayA_1: _relayA1NoiseSequence--; break;
                    case RelaySlot.RelayA_2: _relayA2NoiseSequence--; break;
                    case RelaySlot.RelayB_1: _relayB1NoiseSequence--; break;
                    case RelaySlot.RelayB_2: _relayB2NoiseSequence--; break;
                }
            }
        }

        private void EmitRelayRepairNoise(
            RelaySlot slot,
            PlayerRef operatorPlayer,
            bool active,
            ref TickTimer pulseTimer,
            ref long sequence,
            TimeSpan pulseInterval)
        {
            if (!active
                || !operatorPlayer.IsRealPlayer
                || !TryGetRelayTarget(slot, out var target)
                || !TryValidateZone2Requester(
                    operatorPlayer,
                    target,
                    _zone2InteractionDistance)
                || !pulseTimer.Expired(Runner))
            {
                if (!active || !operatorPlayer.IsRealPlayer)
                {
                    pulseTimer = TickTimer.None;
                }
                return;
            }

            pulseTimer = TickTimer.CreateFromSeconds(
                Runner,
                (float)pulseInterval.TotalSeconds);

            var authority = MatchAuthorityRuntime.Instance;
            if (authority == null || authority.MatchId == Guid.Empty)
            {
                return;
            }

            var noiseService = HostRuntimeNoiseService.EnsureExists(authority);
            var nextSequence = sequence == long.MaxValue ? 1 : sequence + 1;
            var key = new RuntimeNoiseSourceOccurrenceKey(
                $"relay-repair:{authority.MatchId:D}:{slot}",
                nextSequence);

            if (noiseService.TryAccept(
                    operatorPlayer,
                    RuntimeNoiseType.MACHINE_REPAIR,
                    key,
                    target.transform.position,
                    out _))
            {
                sequence = nextSequence;
            }
        }

        private static bool IsValidRelaySlot(RelaySlot slot) => (int)slot >= 0 && (int)slot <= 3;
        private static bool IsRelayASlot(RelaySlot slot) => slot == RelaySlot.RelayA_1 || slot == RelaySlot.RelayA_2;
        private static bool IsRelayBSlot(RelaySlot slot) => slot == RelaySlot.RelayB_1 || slot == RelaySlot.RelayB_2;
        private bool HasValidNetworkObject() => Object != null && Object.IsValid && Runner != null;

        private bool TryGetLocalRequester(out PlayerRef requester)
        {
            requester = Runner != null ? Runner.LocalPlayer : PlayerRef.None;
            return requester.IsRealPlayer;
        }

        private bool TryResolveActivePlayer(PlayerRef player, out NetworkPlayerLifeState lifeState)
        {
            lifeState = null;
            return player.IsRealPlayer
                && Runner.TryGetPlayerObject(player, out var playerObject)
                && playerObject != null
                && playerObject.IsValid
                && playerObject.InputAuthority == player
                && playerObject.TryGetComponent<LobbyPlayerState>(out var lobbyState)
                && lobbyState.IsGameplayPlayer
                && playerObject.TryGetComponent(out lifeState)
                && lifeState.CanInitiateAction;
        }

        
        private bool ValidateObjectiveSource(NetworkSectorBox source)
        {
            return Object != null
                && Object.HasStateAuthority
                && !IsEnded
                && ObjectiveSourceId.IsValid
                && source != null
                && source.Object != null
                && source.Object.IsValid
                && source.Object.HasStateAuthority
                && source.MatchStateId == Object.Id
                && IsTrackedSectorBox(source);
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
