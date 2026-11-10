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
        private RemoteFixedBalanceSnapshot _remoteFixedBalance;
        public bool HasAutomaticBalance { get; private set; }

        public double V02StalkerValue(AEDV02Plan plan, AEDV02Key key) => V02EffectiveValue(plan, key);

        public double V02EffectiveValue(AEDV02Plan plan, AEDV02Key key)
        {
            var state = NetworkMatchState.Instance;
            if (HasAutomaticBalance && state != null && state.TryReadV02ResolvedValue(MatchId, plan, key, out var approved))
                return approved;
            return TryResolveV02Value(key, plan.Get(key), out var value) ? value : V02Baseline(key);
        }

        internal bool TryResolveV02ToolMask(double tier, int initial, out int mask)
        {
            mask = 0;
            if (NetworkMatchState.Instance?.IsAutomaticSoloMatch == true)
                return tier == initial;
            int basis = 0;
            float defaultTools = _remoteFixedBalance?.Get("toolsPerZone", 5) ?? 5;
            for (int zone = 1; zone <= 3; zone++)
                if ((_remoteFixedBalance?.Get($"zone{zone}ToolBudget", defaultTools) ?? defaultTools) == 6)
                    basis |= 1 << (zone - 1);
            return AEDV02Plan.TryResolveOptionalMask(basis, (int)tier - initial, out mask);
        }

        internal double V02Baseline(AEDV02Key key)
        {
            var profile = MatchDifficultyProfiles.Get(MatchDifficulty.Normal);
            var normal = key switch
            {
                AEDV02Key.Zone1MinionCap => profile.Zone1MinionCap,
                AEDV02Key.Zone2MinionCap => profile.Zone2MinionCap,
                AEDV02Key.Zone3AdaptiveMinionCap => profile.Zone3MinionCap,
                _ => AEDV02Plan.Normal().Get(key)
            };
            var snapshot = _remoteFixedBalance;
            if (snapshot == null) return normal;
            return key switch
            {
                AEDV02Key.ChaseSpeed => snapshot.stalker.chaseSpeed,
                AEDV02Key.DetectionAcquireSeconds => snapshot.Get("stalkerDetectSeconds", (float)normal),
                AEDV02Key.SearchSeconds => snapshot.Get("stalkerSearchSeconds", (float)normal),
                AEDV02Key.HearingMultiplier => snapshot.Get("stalkerHearing", (float)normal),
                AEDV02Key.SpecialCooldownSeconds => snapshot.Get("stalkerSpecialCooldown", (float)normal),
                AEDV02Key.Zone1MinionCap => snapshot.Get("minionCapZone1", (float)normal),
                AEDV02Key.Zone2MinionCap => snapshot.Get("minionCapZone2", (float)normal),
                AEDV02Key.Zone3AdaptiveMinionCap => snapshot.Get("minionCapZone3", (float)normal),
                _ => normal
            };
        }

        internal bool TryResolveV02Value(AEDV02Key key, double tier, out double value)
        {
            value = tier;
            return !HasAutomaticBalance || (int)key >= (int)AEDV02Key.OptionalToolBudget ||
                AEDV02Plan.TryResolveRelative(key, _remoteFixedBalance?.stalker.patrolSpeed ?? 6.5f,
                    V02Baseline(key), tier, out value);
        }

        public bool TryGetRemoteFixedBalance(out RemoteFixedBalanceSnapshot snapshot)
        {
            snapshot = IsHostBinding && HasBinding ? _remoteFixedBalance : null;
            return snapshot != null;
        }
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
        internal int StartedRosterCount { get; private set; }
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
        private readonly Dictionary<Guid, bool> _migratedResultRoster = new();
        private bool _eventsSubscribed;
        private TelemetryRuntimeBehaviour _telemetry;
        private DateTime _matchStartedAtUtc;
        private bool _telemetryMatchActive;
        private readonly List<Action> _pendingInitialSpawns = new();
        private bool _phase8DebugEvidenceInvalidated;
        private bool _phase8InventoryEvidenceInvalidated;

        public bool Phase8InventoryEvidenceInvalidated =>
            _phase8InventoryEvidenceInvalidated;

        public bool Phase8DebugEvidenceInvalidated =>
            _phase8DebugEvidenceInvalidated;
        private bool _matchEndEmitted;
        private bool _pendingMatchEnd;
        private string _pendingMatchEndOccurrenceKey;
        private string _pendingMatchEndOutcome;
        private int _pendingMatchEndSurvivorCount;
        private string _pendingMatchEndReasonCode;
        private float _nextMatchEndRetryAt;
        private bool _matchEndRetryWarningLogged;
        private bool _pendingAuthoritativeTelemetryMatchStart;
        private float _nextTelemetryMatchStartAttempt;
        private string _managedPreMatchContentWhitelistVersion;
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
        private readonly AEDPhase8AuthoritySourceCollectorV1 _aedPhase8Source =
            new AEDPhase8AuthoritySourceCollectorV1();
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

        public void MarkPhase8InventoryEvidenceIncomplete(string reason)
        {
            if (!HasStateAuthority || MatchId == Guid.Empty ||
                (!_telemetryMatchActive && !_pendingAuthoritativeTelemetryMatchStart))
                return;
            _phase8InventoryEvidenceInvalidated = true;
            ApplyPhase8InventoryEvidenceInvalidation();
            Debug.LogWarning($"[AED_P8][INVENTORY_EVIDENCE_INCOMPLETE] reason={reason}");
        }

        private void ApplyPhase8InventoryEvidenceInvalidation()
        {
            if (!_phase8InventoryEvidenceInvalidated) return;
            _aedv2Evidence.MarkIncomplete();
            _aedObjectiveEvidence.MarkIncomplete();
            _aedToolNoiseEvidence.MarkIncomplete();
        }

        public void MarkPhase8DebugEvidenceInvalid(
            PlayerRef player,
            string reason)
        {
            if (!HasStateAuthority || MatchId == Guid.Empty ||
                (!_telemetryMatchActive && !_pendingAuthoritativeTelemetryMatchStart))
                return;

            _phase8DebugEvidenceInvalidated = true;
            ApplyPhase8DebugEvidenceInvalidation();
            Debug.LogWarning($"[AED_P8][DEBUG_EVIDENCE_INVALIDATED] player={player} reason={reason}");
        }

        private void ApplyPhase8DebugEvidenceInvalidation()
        {
            if (!_phase8DebugEvidenceInvalidated) return;
            _aedv2Evidence.MarkIncomplete();
            _aedObjectiveEvidence.MarkIncomplete();
            _aedSurvivalEvidence.MarkIncomplete();
            _aedToolNoiseEvidence.MarkIncomplete();
            _aedPursuitEvidence.MarkIncomplete();
            _aedMinionEvidence.MarkIncomplete();
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
            if (state == "CHASE" &&
                (target == PlayerRef.None ||
                 !target.IsValid ||
                 !TryResolveVerifiedBackendUserId(target, out targetUserId)))
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
            if (kind == AEDPursuitFactKindV1.Downed)
                RecordV02CausalImpact(target, stalkerNetworkId, "STALKER_DOWN", occurrenceKey, tick);
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
            if ((state == "Track" || state == "Harass") &&
                target.IsRealPlayer &&
                !TryResolveVerifiedBackendUserId(target, out userId))
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
            if ((user.IsRealPlayer &&
                 !TryResolveVerifiedBackendUserId(user, out userId)) ||
                (relatedUser.IsRealPlayer &&
                 !TryResolveVerifiedBackendUserId(relatedUser, out relatedUserId)))
            {
                _aedMinionEvidence.MarkIncomplete();
                return;
            }
            _aedMinionEvidence.RecordFact(kind, minionId.ToString(),
                occurrenceKey, tick, userId, relatedUserId, objectId,
                effectKind, attemptOrdinal, accepted, seconds, sourceEventId,
                position.x, position.y, position.z);
            if (accepted && kind is AEDMinionFactKindV1.SlowApplied or AEDMinionFactKindV1.CoreForcedDrop or AEDMinionFactKindV1.ToolRelocated)
                RecordV02CausalImpact(user, minionId.ToString(), kind == AEDMinionFactKindV1.SlowApplied ? "MINION_SLOW" :
                    kind == AEDMinionFactKindV1.CoreForcedDrop ? "MINION_CORE_DROP" : "MINION_TOOL_LOSS", occurrenceKey, tick, objectId);
        }

        private void TryPublishPhase8Lifecycle(string occurrenceKey, Func<bool> enqueue)
        {
            if (!_researchCaptureEnabled) return;
            if (!CanEmitProductionTelemetry() || _telemetry.Phase8LifecycleAdapter == null)
            {
                MarkPhase8LifecycleReceiptFailure(occurrenceKey);
                Debug.LogWarning($"[AED_P8][LIFECYCLE_NOT_READY] key={occurrenceKey}");
                return;
            }
            try
            {
                if (enqueue()) return;
                MarkPhase8LifecycleReceiptFailure(occurrenceKey);
                Debug.LogWarning($"[AED_P8][LIFECYCLE_ENQUEUE_FAILED] key={occurrenceKey}");
            }
            catch (Exception exception)
            {
                MarkPhase8LifecycleReceiptFailure(occurrenceKey);
                Debug.LogWarning($"[AED_P8][LIFECYCLE_INVALID] key={occurrenceKey} reason={exception.Message}");
            }
        }

        private void MarkPhase8LifecycleReceiptFailure(string occurrenceKey)
        {
            _aedv2Evidence.MarkIncomplete();
            if (occurrenceKey.StartsWith("p8:tool:", StringComparison.Ordinal))
                MarkPhase8InventoryEvidenceIncomplete("LIFECYCLE_RECEIPT_FAILED");
            else if (occurrenceKey.StartsWith("p8:minion:", StringComparison.Ordinal))
                _aedMinionEvidence.MarkIncomplete();
        }

        public bool TryRecordPhase8LobbyLoadout(NetworkObject playerObject, long tick)
        {
            if (!HasStateAuthority || !_telemetryMatchActive || playerObject == null ||
                !playerObject.IsValid || !playerObject.HasStateAuthority ||
                !playerObject.InputAuthority.IsRealPlayer ||
                !playerObject.TryGetComponent<LobbyPlayerState>(out var state) ||
                !state.IsGameplayPlayer || state.ToolId == 0)
                return false;
            if (!TryResolveVerifiedBackendUserId(playerObject.InputAuthority, out var ownerUserId) ||
                !Guid.TryParse(ownerUserId, out var ownerId) || ownerId == Guid.Empty)
            {
                _aedToolNoiseEvidence.MarkIncomplete();
                MarkPhase8InventoryEvidenceIncomplete("LOADOUT_OWNER_UNVERIFIED");
                return false;
            }
            var instanceId = $"loadout:{MatchId:D}:{playerObject.Id}";
            int? charges = state.ToolId is 2 or 4 ? state.TeamToolUsesRemaining : (int?)null;
            try
            {
                if (_aedPhase8Source.TryGetLatestTool(instanceId, out var existing))
                {
                    if (existing.Origin == "LobbyLoadout" && existing.ToolId == state.ToolId &&
                        existing.OwnerUserId == ownerUserId && existing.RemainingCharges == charges &&
                        existing.Location == "Player") return true;
                    throw new InvalidOperationException("AED_P8_LOADOUT_IDENTITY_CONFLICT");
                }
                var occurrenceKey = $"p8:tool:{instanceId}:loadout";
                _aedPhase8Source.RecordLobbyToolLoadout(instanceId, state.ToolId, ownerUserId, charges, tick, occurrenceKey);
                TryPublishPhase8Lifecycle(occurrenceKey, () => _telemetry.Phase8LifecycleAdapter.EmitToolLoadout(
                    occurrenceKey, DateTime.UtcNow, _currentTelemetryPhase, instanceId, state.ToolId,
                    ownerId, charges, out _, out _));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[AED_P8][TOOL_SOURCE] action=LOADOUT id={instanceId} owner={ownerUserId} tool={state.ToolId} tick={tick}");
#endif
                return true;
            }
            catch (Exception exception)
            {
                _aedToolNoiseEvidence.MarkIncomplete();
                MarkPhase8InventoryEvidenceIncomplete("LOADOUT_SOURCE_INVALID");
                Debug.LogWarning($"[AED_P8][TOOL_SOURCE_INVALID] action=LOADOUT player={playerObject.Id} reason={exception.Message}");
                return false;
            }
        }

        private void CaptureExistingPhase8LobbyLoadouts()
        {
            var runner = _bootstrap?.Runner;
            if (runner == null || !HasStateAuthority || !_telemetryMatchActive) return;
            foreach (var player in runner.ActivePlayers)
            {
                if (runner.TryGetPlayerObject(player, out var playerObject) && playerObject != null)
                    TryRecordPhase8LobbyLoadout(playerObject, runner.Tick.Raw);
            }
        }

        public bool TryRecordPhase8SceneToolSpawn(
            NetworkId instanceId, int toolId, long tick)
        {
            if (!HasStateAuthority || !_telemetryMatchActive) return false;
            try
            {
                var occurrenceKey = $"p8:tool:{instanceId}:scene-spawn";
                if (!_bootstrap.Runner.TryFindObject(instanceId, out var sceneTool) ||
                    !AEDV02SceneSource.TryLoad(out var sceneManifest, out _) ||
                    sceneManifest.Markers.SingleOrDefault(x => x.Id == AEDV02SceneSource.MarkerId(sceneTool.transform) &&
                        x.Kind == "TOOL" && x.AllowedToolIds.Contains(toolId)) is not { } marker)
                    throw new InvalidOperationException("AED_P8_SCENE_TOOL_MARKER_INVALID");
                int? charges = null;
                if (toolId is 2 or 4 && _bootstrap?.Runner != null && _bootstrap.Runner.TryFindObject(instanceId, out var toolObject))
                    charges = toolObject.TryGetComponent<NetworkTeamToolPickup>(out var pickup) ? pickup.RemainingUses :
                        toolObject.TryGetComponent<EchoProtocol.Tools.Scanner.NetworkToolPickup>(out var scannerPickup) ? scannerPickup.RemainingUses : (int?)null;
                _aedPhase8Source.RecordSceneToolSpawnWithCharges(
                    instanceId.ToString(), toolId, tick, occurrenceKey, charges, marker.Zone);
                TryPublishPhase8Lifecycle(
                    occurrenceKey,
                    () => _telemetry.Phase8LifecycleAdapter.EmitToolSpawnWithCharges(
                        occurrenceKey, DateTime.UtcNow, _currentTelemetryPhase,
                        instanceId.ToString(), toolId, marker.Zone, "ScenePlaced", out _, out _, charges));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[AED_P8][TOOL_SOURCE] action=SPAWN origin=ScenePlaced id={instanceId} tool={toolId} tick={tick}");
#endif
                return true;
            }
            catch (Exception exception)
            {
                _aedToolNoiseEvidence.MarkIncomplete();
                MarkPhase8InventoryEvidenceIncomplete("SCENE_SPAWN_INVALID");
                Debug.LogWarning($"[AED_P8][TOOL_SOURCE_INVALID] action=SCENE_SPAWN id={instanceId} reason={exception.Message}");
                return false;
            }
        }

        public void RecordPhase8ToolSpawn(
            NetworkId instanceId, int toolId, int zone, long tick)
        {
            if (!HasStateAuthority) return;
            int? charges = null;
            if (toolId is 2 or 4 && _bootstrap.Runner.TryFindObject(instanceId, out var toolObject))
                charges = toolObject.TryGetComponent<NetworkTeamToolPickup>(out var pickup) ? pickup.RemainingUses :
                    toolObject.TryGetComponent<EchoProtocol.Tools.Scanner.NetworkToolPickup>(out var scannerPickup) ? scannerPickup.RemainingUses : (int?)null;
            if (!_telemetryMatchActive)
            {
                if (!TryQueueInitialSpawn(() => PublishPhase8ToolSpawn(instanceId, toolId, zone, tick, charges)))
                    MarkPhase8InventoryEvidenceIncomplete("LIFECYCLE_BEFORE_TELEMETRY_MATCH_START");
                return;
            }
            PublishPhase8ToolSpawn(instanceId, toolId, zone, tick, charges);
        }

        private bool TryQueueInitialSpawn(Action publish)
        {
            if (_backendMatchStartedAtRealtime < 0 || _matchStartedAtUtc != default ||
                _pendingInitialSpawns.Count >= 128) return false;
            _pendingInitialSpawns.Add(publish);
            return true;
        }

        private void PublishPhase8ToolSpawn(NetworkId instanceId, int toolId, int zone, long tick, int? charges)
        {
            try
            {
                var occurrenceKey = $"p8:tool:{instanceId}:spawn";
                _aedPhase8Source.RecordToolSpawnWithCharges(
                    instanceId.ToString(), toolId, zone, tick,
                    occurrenceKey, charges);
                TryPublishPhase8Lifecycle(
                    occurrenceKey,
                    () => _telemetry.Phase8LifecycleAdapter.EmitToolSpawnWithCharges(
                        occurrenceKey, DateTime.UtcNow, _currentTelemetryPhase,
                        instanceId.ToString(), toolId, zone, "WorldSpawn", out _, out _, charges));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[AED_P8][TOOL_SOURCE] action=SPAWN id={instanceId} tool={toolId} zone={zone} tick={tick}");
#endif
            }
            catch (Exception ex)
            {
                _aedToolNoiseEvidence.MarkIncomplete();
                MarkPhase8InventoryEvidenceIncomplete("WORLD_SPAWN_INVALID");
                Debug.LogWarning($"[AED_P8][TOOL_SOURCE_INVALID] action=SPAWN id={instanceId} reason={ex.Message}");
            }
        }

        public void RecordPhase8ToolPickup(
            NetworkId instanceId, PlayerRef player, long tick, int? remainingCharges)
        {
            if (!HasStateAuthority) return;
            if (!_telemetryMatchActive)
            {
                MarkPhase8InventoryEvidenceIncomplete("LIFECYCLE_BEFORE_TELEMETRY_MATCH_START");
                Debug.LogWarning($"[AED_P8][TOOL_SOURCE_MISSED] action=PICKUP id={instanceId} reason=MATCH_NOT_ACTIVE");
                return;
            }
            if (!player.IsRealPlayer ||
                !TryResolveVerifiedBackendUserId(player, out var userId))
            {
                _aedToolNoiseEvidence.MarkIncomplete();
                MarkPhase8InventoryEvidenceIncomplete("PICKUP_OWNER_UNVERIFIED");
                Debug.LogWarning($"[AED_P8][TOOL_SOURCE_INVALID] action=PICKUP id={instanceId} reason=OWNER_UNVERIFIED");
                return;
            }
            try
            {
                var occurrenceKey = $"p8:tool:{instanceId}:pickup:{tick}";
                _aedPhase8Source.RecordToolTransition(
                    instanceId.ToString(), "Player", userId,
                    remainingCharges, tick,
                    occurrenceKey);
                if (!_aedPhase8Source.TryGetLatestTool(instanceId.ToString(), out var tool) ||
                    !Guid.TryParse(userId, out var ownerUserId))
                {
                    _aedToolNoiseEvidence.MarkIncomplete();
                    MarkPhase8InventoryEvidenceIncomplete("PICKUP_SOURCE_MISSING");
                    return;
                }
                TryPublishPhase8Lifecycle(
                    occurrenceKey,
                    () => _telemetry.Phase8LifecycleAdapter.EmitToolPickup(
                        occurrenceKey, DateTime.UtcNow, _currentTelemetryPhase,
                        tool.InstanceId, tool.ToolId, tool.Zone, ownerUserId,
                        tool.RemainingCharges, out _, out _));
                TryRecordV02ResourceAction(player, tool.ToolId, tool.InstanceId, "PICKUP", occurrenceKey, tick);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[AED_P8][TOOL_SOURCE] action=PICKUP id={instanceId} owner={userId} tick={tick}");
#endif
            }
            catch (Exception ex)
            {
                _aedToolNoiseEvidence.MarkIncomplete();
                MarkPhase8InventoryEvidenceIncomplete("PICKUP_SOURCE_INVALID");
                Debug.LogWarning($"[AED_P8][TOOL_SOURCE_INVALID] action=PICKUP id={instanceId} reason={ex.Message}");
            }
        }

        public void RecordPhase8ToolUse(PlayerRef player, int toolId, int remainingCharges, bool consumed, long tick)
        {
            if (!HasStateAuthority || !_telemetryMatchActive) return;
            if (!player.IsRealPlayer || !TryResolveVerifiedBackendUserId(player, out var userId) || !Guid.TryParse(userId, out var ownerId))
            { _aedToolNoiseEvidence.MarkIncomplete(); MarkPhase8InventoryEvidenceIncomplete("USE_OWNER_UNVERIFIED"); return; }
            try
            {
                if (!_aedPhase8Source.TryGetHeldTool(userId, out var held))
                    throw new InvalidOperationException("AED_P8_TOOL_USE_PARENT_MISSING");
                var instanceId = held.InstanceId;
                var action = consumed ? "consume" : "use";
                var occurrenceKey = $"p8:tool:{instanceId}:{action}:{tick}";
                var source = _aedPhase8Source.RecordToolUse(instanceId, userId, toolId, remainingCharges, consumed, tick, occurrenceKey);
                TryPublishPhase8Lifecycle(occurrenceKey, () => _telemetry.Phase8LifecycleAdapter.EmitToolUse(
                    occurrenceKey, DateTime.UtcNow, _currentTelemetryPhase, source.InstanceId, source.ToolId,
                    source.Zone, ownerId, remainingCharges, consumed, out _, out _));
                TryRecordV02ResourceAction(player, source.ToolId, source.InstanceId, consumed ? "CONSUME" : "USE", occurrenceKey, tick);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[AED_P8][TOOL_SOURCE] action={(consumed ? "CONSUME" : "USE")} id={instanceId} charges={remainingCharges} tick={tick}");
#endif
            }
            catch (Exception exception)
            {
                _aedToolNoiseEvidence.MarkIncomplete();
                MarkPhase8InventoryEvidenceIncomplete("USE_SOURCE_INVALID");
                Debug.LogWarning($"[AED_P8][TOOL_SOURCE_INVALID] action={(consumed ? "CONSUME" : "USE")} reason={exception.Message}");
            }
        }

        public void RecordPhase8ToolDrop(NetworkId newInstanceId, PlayerRef player, int toolId, int remainingUses, long tick)
        {
            if (!HasStateAuthority || !_telemetryMatchActive) return;
            if (!player.IsRealPlayer || !TryResolveVerifiedBackendUserId(player, out var userId) ||
                !Guid.TryParse(userId, out var ownerId))
            {
                _aedToolNoiseEvidence.MarkIncomplete();
                MarkPhase8InventoryEvidenceIncomplete("DROP_OWNER_UNVERIFIED");
                return;
            }
            try
            {
                var occurrenceKey = $"p8:tool:{newInstanceId}:drop:{tick}";
                int? charges = remainingUses > 0 ? remainingUses : (int?)null;
                _aedPhase8Source.RecordToolDrop(newInstanceId.ToString(), toolId, userId, charges, tick, occurrenceKey);
                if (!_aedPhase8Source.TryGetLatestTool(newInstanceId.ToString(), out var source) ||
                    string.IsNullOrWhiteSpace(source.ParentInstanceId))
                {
                    _aedToolNoiseEvidence.MarkIncomplete();
                    MarkPhase8InventoryEvidenceIncomplete("DROP_SOURCE_MISSING");
                    return;
                }
                TryPublishPhase8Lifecycle(occurrenceKey, () => _telemetry.Phase8LifecycleAdapter.EmitToolDrop(
                    occurrenceKey, DateTime.UtcNow, _currentTelemetryPhase, source.InstanceId,
                    source.ParentInstanceId, source.ToolId, source.Zone, ownerId,
                    source.RemainingCharges, out _, out _));
                TryRecordV02ResourceAction(player, source.ToolId, source.InstanceId, "DROP", occurrenceKey, tick);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[AED_P8][TOOL_SOURCE] action=DROP id={newInstanceId} parent={source.ParentInstanceId} owner={userId} tick={tick}");
#endif
            }
            catch (Exception exception)
            {
                _aedToolNoiseEvidence.MarkIncomplete();
                MarkPhase8InventoryEvidenceIncomplete("DROP_SOURCE_INVALID");
                Debug.LogWarning($"[AED_P8][TOOL_SOURCE_INVALID] action=DROP id={newInstanceId} reason={exception.Message}");
            }
        }

        public void RecordPhase8MinionSpawn(NetworkId minionId, int zone, long tick)
        {
            if (!HasStateAuthority) return;
            if (!_telemetryMatchActive)
            {
                if (!TryQueueInitialSpawn(() => RecordPhase8MinionSpawn(minionId, zone, tick)))
                    _aedMinionEvidence.MarkIncomplete();
                return;
            }
            try
            {
                var occurrenceKey = $"p8:minion:{minionId}:spawn";
                _aedPhase8Source.RecordMinionSpawn(
                    minionId.ToString(), zone, tick,
                    occurrenceKey);
                TryPublishPhase8Lifecycle(
                    occurrenceKey,
                    () => _telemetry.Phase8LifecycleAdapter.EmitMinionLifecycle(
                        occurrenceKey, DateTime.UtcNow, _currentTelemetryPhase,
                        minionId.ToString(), zone, "SPAWNED", out _, out _));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[AED_P8][MINION_SOURCE] action=SPAWN id={minionId} zone={zone} tick={tick}");
#endif
            }
            catch (Exception exception)
            {
                _aedMinionEvidence.MarkIncomplete();
                Debug.LogWarning($"[AED_P8][MINION_SOURCE_INVALID] action=SPAWN id={minionId} reason={exception.Message}");
            }
        }

        public void RecordPhase8MinionDespawn(NetworkId minionId, int zone, long tick)
        {
            if (!HasStateAuthority || !_telemetryMatchActive) return;
            try
            {
                var occurrenceKey = $"p8:minion:{minionId}:despawn";
                _aedPhase8Source.RecordMinionDespawn(
                    minionId.ToString(), zone, tick,
                    occurrenceKey);
                TryPublishPhase8Lifecycle(
                    occurrenceKey,
                    () => _telemetry.Phase8LifecycleAdapter.EmitMinionLifecycle(
                        occurrenceKey, DateTime.UtcNow, _currentTelemetryPhase,
                        minionId.ToString(), zone, "DESPAWNED", out _, out _));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[AED_P8][MINION_SOURCE] action=DESPAWN id={minionId} zone={zone} tick={tick}");
#endif
            }
            catch (Exception exception)
            {
                _aedMinionEvidence.MarkIncomplete();
                Debug.LogWarning($"[AED_P8][MINION_SOURCE_INVALID] action=DESPAWN id={minionId} reason={exception.Message}");
            }
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

        private bool _needsFreshLobbyBinding;
        public void RequireFreshLobbyBinding() => _needsFreshLobbyBinding = true;
        public bool RequiresFreshHostBinding =>
            _needsFreshLobbyBinding || !HasBinding
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
            _researchCaptureEnabled = true;

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
            if (_pendingAuthoritativeTelemetryMatchStart && Time.unscaledTime >= _nextTelemetryMatchStartAttempt)
            {
                _nextTelemetryMatchStartAttempt = Time.unscaledTime + 1f;
                TryBeginAuthoritativeTelemetryMatch();
            }
            SampleAEDv2PlayerObservation(matchState);
            TickV02Coordinator(matchState);
            TickV02WorldSource(matchState);
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
            _needsFreshLobbyBinding = false;
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
            _remoteFixedBalance = null;
            requestedScenarioResolutionMode =
                ReadScenarioResolutionMode(runner);
            requestedDifficulty =
                ReadMatchDifficulty(runner);
            ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings?.ConfigureAutomaticAdaptive(
                requestedScenarioResolutionMode == ScenarioResolutionMode.Adaptive);

            bool restoreAutomaticRevision = false;
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
                    var context = result.Data.data;

                    if (!Guid.TryParse(context.matchId, out var returnedMatchId)
                        || returnedMatchId != sessionMatchId)
                    {
                        Debug.LogError("[RemoteFixed] Takeover match identity mismatch.");
                        return false;
                    }

                    if (context.balanceSource == "REMOTE_FIXED")
                    {
                        var locked = await _api.GetLockedBalanceAsync(sessionMatchId);

                        if (!IsSuccessful(locked)
                            || !RemoteFixedBalanceSnapshot.TryDecodeLocked(
                                context,
                                locked.Data.data,
                                sessionMatchId,
                                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                                requestedDifficulty.ToString().ToUpperInvariant(),
                                out var recovered))
                        {
                            Debug.LogError(
                                "[RemoteFixed] Host Migration rejected: locked snapshot verification failed.");
                            return false;
                        }

                        _remoteFixedBalance = recovered;

                        Debug.Log(
                            $"[RemoteFixed] Host Migration verified: " +
                            $"match={sessionMatchId:D}, " +
                            $"version={context.balanceVersion}, " +
                            $"hash={context.balanceHash}");
                    }
                    else
                    {
                        if ((context.balanceSource != null &&
                             context.balanceSource != "LOCAL")
                            || !string.IsNullOrEmpty(context.balanceVersion)
                            || !string.IsNullOrEmpty(context.balanceHash)
                            || context.matchDataPolicy != "STANDARD")
                        {
                            Debug.LogError(
                                "[MatchAuthority] Invalid non-remote takeover context.");
                            return false;
                        }

                        _remoteFixedBalance = null;
                    }
                    HasAutomaticBalance = !string.IsNullOrEmpty(context.balanceBuildVersion);
                    if (HasAutomaticBalance)
                    {
                        requestedScenarioResolutionMode = context.resolutionMode == "ADAPTIVE"
                            ? ScenarioResolutionMode.Adaptive : ScenarioResolutionMode.Fixed;
                        restoreAutomaticRevision = requestedScenarioResolutionMode == ScenarioResolutionMode.Adaptive &&
                            string.Equals(context.status, "InMatch", StringComparison.OrdinalIgnoreCase);
                        ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings?.ConfigureAutomaticAdaptive(
                            requestedScenarioResolutionMode == ScenarioResolutionMode.Adaptive);
                    }

                    _migratedResultRoster.Clear();

                    foreach (var participant in
                             context.players ?? Array.Empty<MatchRosterPlayerDto>())
                    {
                        if (Guid.TryParse(participant.userId, out var participantId))
                            _migratedResultRoster[participantId] =
                                participant.disconnected;
                    }

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
                || !runner.SessionInfo.IsValid
                || MatchId != sessionMatchId
                || _bootstrap?.Runner != runner)
            {
                Debug.LogWarning("[MatchAuthority] Backend Host takeover succeeded, but the Fusion runner is no longer authoritative.");
                return false;
            }

            IsHostBinding = true;
            if (restoreAutomaticRevision)
            {
                // Keep applying confirmed gameplay while new decisions wait for reconciled evidence.
                V02DecisionsFrozen = true;
                var confirmed = await new AEDV02ApiService(AuthRuntime.EnsureExists().Client).ConfirmedRevision(sessionMatchId);
                var migratedState = NetworkMatchState.Instance;
                if (!IsSuccessful(confirmed) || MatchId != sessionMatchId || _bootstrap?.Runner != runner ||
                    migratedState == null || migratedState.Runner != runner ||
                    !migratedState.RestoreConfirmedAutomaticV02(this, confirmed.Data.data.revision))
                {
                    Debug.LogError("[AED_V02] Cannot restore confirmed automatic revision on the new Host.");
                    IsHostBinding = false;
                    return false;
                }
            }
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

            var runner = _bootstrap?.Runner;
            var mode = runner != null && runner.SessionInfo.IsValid
                ? ReadScenarioResolutionMode(runner) : requestedScenarioResolutionMode;
            var difficulty = runner != null && runner.SessionInfo.IsValid
                ? ReadMatchDifficulty(runner) : requestedDifficulty;
            ResetBinding();
            RequestScenarioResolutionMode(mode);
            RequestDifficulty(difficulty);

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

        public Dictionary<string, SessionProperty> BuildBindingSessionProperties() =>
            new Dictionary<string, SessionProperty>
            {
                [MatchIdSessionProperty] = MatchId.ToString("D")
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
                        if (MatchId != bindingMatchId
                            || !IsHostBinding
                            || playerState == null
                            || playerState.Object == null
                            || !playerState.Object.IsValid
                            || playerState.Object.InputAuthority
                                != inputAuthority)
                        {
                            return;
                        }

                        playerState.ApplyVerifiedBackendIdentity(
                            result.Data.data.userId, bindingMatchId);

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
                        && IsPlayerBound(actorNumber, playerState.BackendUserId.ToString()))
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
                var userId = expectedUserId.ToString("D");
                if (_telemetryMatchActive && _aedPhase8Source.TryGetHeldTool(userId, out _))
                    MarkPhase8InventoryEvidenceIncomplete("PLAYER_DISCONNECTED_WITH_HELD_TOOL");
                _aedPursuitEvidence.CensorTarget(userId, "PLAYER_DISCONNECTED", CurrentAuthorityTick);
                _aedMinionEvidence.CensorTarget(userId, "PLAYER_DISCONNECTED", CurrentAuthorityTick);

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

        public async Task<(bool Accepted, bool Adaptive, string Error)> PrepareAutomaticBalanceAsync(string map)
        {
            if (!HasBinding || !IsHostBinding) return (false, false, "Missing host binding.");
            var matchId = MatchId;
            var difficulty = requestedDifficulty.ToString().ToUpperInvariant();
            var result = await _api.StartV2Async(matchId, map, difficulty, prepare: true);
            if (!IsSuccessful(result)) return (false, false, Describe(result));
            var context = result.Data.data;
            if (context.balanceBuildVersion != Application.version)
                return (false, false, "Backend did not lock the balance for this build.");
            RemoteFixedBalanceSnapshot snapshot = null;
            if (context.balanceSource == "REMOTE_FIXED")
            {
                var locked = await _api.GetLockedBalanceAsync(matchId);
                if (!IsSuccessful(locked) || !RemoteFixedBalanceSnapshot.TryDecodeLocked(
                    context, locked.Data.data, matchId, map, difficulty, out snapshot))
                    return (false, false, "Locked Web balance could not be verified.");
            }
            if (MatchId != matchId || !IsHostBinding) return (false, false, "Host binding changed.");
            _remoteFixedBalance = snapshot;
            HasAutomaticBalance = true;
            return (true, context.resolutionMode == "ADAPTIVE", string.Empty);
        }

        public async Task<(bool Accepted, string Error)> StartMatchAsync(
            bool useRemoteFixed = false)
        {
            if (_backendEndRequestInProgress)
                return (false, "Previous match is still being finalized.");

            if (HasBinding && !IsHostBinding)
                return (false, "Backend match has already ended.");

            if (!IsHostBinding || !HasBinding)
                return (false, "Backend Host binding is unavailable.");

            var runner = _bootstrap?.Runner;
            if (runner == null || !runner.IsRunning ||
                !runner.IsServer || !runner.SessionInfo.IsValid)
                return (false, "Authoritative room is unavailable.");

            if (useRemoteFixed &&
                requestedScenarioResolutionMode != ScenarioResolutionMode.Fixed)
                return (false, "Remote Fixed requires FIXED mode.");

            var matchId = MatchId;
            var mapId = LobbyMapCatalog.SceneName(LobbyMapCatalog.Selected(runner));
            var difficulty = requestedDifficulty.ToString().ToUpperInvariant();
            bool automaticAdaptive = !Debug.isDebugBuild &&
                requestedScenarioResolutionMode == ScenarioResolutionMode.Adaptive;
            var result = useRemoteFixed || HasAutomaticBalance || automaticAdaptive
                ? await _api.StartV2Async(matchId, mapId, difficulty, automaticAdaptive)
                : await _api.StartAsync(matchId, mapId, difficulty);

            if (!IsSuccessful(result))
            {
                var error = Describe(result);
                Debug.LogWarning($"[MatchAuthority] Backend start rejected: {error}");
                return (false, error);
            }

            _remoteFixedBalance = null;
            if (result.Data.data.balanceSource == "REMOTE_FIXED")
            {
                string failure = null;
                RemoteFixedBalanceSnapshot snapshot = null;

                try
                {
                    var locked = await _api.GetLockedBalanceAsync(matchId);

                    if (!IsSuccessful(locked))
                    {
                        failure = $"Cannot fetch locked balance: {Describe(locked)}";
                    }
                    else if (!RemoteFixedBalanceSnapshot.TryDecodeLocked(
                        result.Data.data,
                        locked.Data.data,
                        matchId,
                        mapId,
                        difficulty,
                        out snapshot))
                    {
                        failure = "Locked Remote Fixed snapshot validation failed.";
                    }

                    if (failure == null &&
                        (MatchId != matchId || !IsHostBinding ||
                         _bootstrap?.Runner != runner || !runner.IsRunning || !runner.IsServer))
                    {
                        failure = "Host authority changed while loading balance.";
                    }
                }
                catch (Exception exception)
                {
                    failure = $"Remote Fixed initialization failed: {exception.Message}";
                }

                if (failure != null)
                {
                    if (MatchId == matchId && IsHostBinding)
                    {
                        try
                        {
                            await EndAsync("REMOTE_FIXED_BALANCE_INVALID");
                        }
                        catch (Exception exception)
                        {
                            Debug.LogWarning(
                                $"[MatchAuthority] Backend abort failed: {exception.Message}");
                        }
                    }

                    Debug.LogError($"[MatchAuthority] {failure}");
                    return (false, failure);
                }

                _remoteFixedBalance = snapshot;
                Debug.Log(
                    $"[RemoteFixed] Verified match={matchId:D}, " +
                    $"version={result.Data.data.balanceVersion}, " +
                    $"hash={result.Data.data.balanceHash}");
            }

            StartedRosterCount = result.Data.data.players?.Length ?? 0;
            _backendMatchStartedAtRealtime = Time.realtimeSinceStartup;

            RuntimeLog.Log(
                RuntimeLogCategory.MatchAuthority,
                $"[MatchAuthority] Backend confirmed match start. Match={matchId:D}.");
            return (true, string.Empty);
        }

        public void RecordObjectiveContribution(PlayerRef actor)
        {
            var runner = _bootstrap?.Runner;
            if (runner == null || !actor.IsRealPlayer) return;

            var actorNumber = runner.GetPlayerActorId(actor) ?? actor.PlayerId;
            if (_boundPlayers.ContainsKey(actorNumber))
            {
                _objectiveContributors.Add(actorNumber);
                if (runner.TryGetPlayerObject(actor, out var obj)
                    && obj.TryGetComponent<LobbyPlayerState>(out var state))
                {
                    state.HasObjectiveContribution = true;
                    NetworkMatchState.Instance?.RecordResultRosterPlayerAuthoritative(state);
                }
            }
        }

        public void ResetBinding()
        {
            _bindingGeneration++;
            var oldMatchId = MatchId;
            MatchId = Guid.Empty;
            _remoteFixedBalance = null;
            HasAutomaticBalance = false;
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
            StartedRosterCount = 0;
            _backendMatchStartedAtRealtime = -1f;
            _boundPlayers.Clear();
            _disconnectedActors.Clear();
            _objectiveContributors.Clear();
            _migratedResultRoster.Clear();
            _matchStartedAtUtc = default;
            _telemetryMatchActive = false;
            _pendingInitialSpawns.Clear();
            _phase8InventoryEvidenceInvalidated = false;
            _phase8DebugEvidenceInvalidated = false;
            _matchEndEmitted = false;
            _pendingMatchEnd = false;
            _pendingMatchEndOccurrenceKey = null;
            _pendingMatchEndOutcome = null;
            _pendingMatchEndSurvivorCount = 0;
            _pendingMatchEndReasonCode = null;
            _nextMatchEndRetryAt = 0f;
            _matchEndRetryWarningLogged = false;
            _pendingAuthoritativeTelemetryMatchStart = false;
            _nextTelemetryMatchStartAttempt = 0;
            _managedPreMatchContentWhitelistVersion = null;
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
            _aedPhase8Source.ResetMatch();
            ResetV02WorldSource();
            ResetV02Coordinator();
            AEDv2Authority.Reset(oldMatchId);
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
            RequestAuthoritativeTelemetryMatchStart();
        }

        private void RequestAuthoritativeTelemetryMatchStart()
        {
            if (_telemetryMatchActive) return;
            if (!_pendingAuthoritativeTelemetryMatchStart)
            {
                _runtimeNoise?.BeginMatch(MatchId);
                _pendingAuthoritativeTelemetryMatchStart = true;
            }
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

            // The authoritative scene can resolve its scenario before the lobby
            // publishes InMatch. Backend acceptance and this record already bind the match.
            if (_backendMatchStartedAtRealtime >= 0 && record.HasStateAuthority)
                RequestAuthoritativeTelemetryMatchStart();
            else
                TryBeginAuthoritativeTelemetryMatch();
        }

        internal void NotifyManagedPreMatchApplied(Guid matchId, string contentWhitelistVersion)
        {
            if (!HasStateAuthority || matchId == Guid.Empty || matchId != MatchId ||
                _backendMatchStartedAtRealtime < 0 || string.IsNullOrWhiteSpace(contentWhitelistVersion)) return;
            _managedPreMatchContentWhitelistVersion = contentWhitelistVersion;
            RequestAuthoritativeTelemetryMatchStart();
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

            var resolvedContentWhitelistVersion = _managedPreMatchContentWhitelistVersion;
            if (resolutionRecord != null && resolutionRecord.TargetMatchId == MatchId &&
                resolutionRecord.HasStateAuthority)
                resolvedContentWhitelistVersion = resolutionRecord.ContentWhitelistVersion;
            if (resolvedContentWhitelistVersion == null) return;

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

            // Retrying the same match must preserve its sequence and occurrence identity.
            if ((!sequenceAllocator.IsActive || sequenceAllocator.MatchId != MatchId) &&
                !_telemetry.TryBeginAuthoritativeMatch())
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
                applied.MapId + ":" + telemetryMapContentVersion,
                resolvedContentWhitelistVersion,
                _researchCaptureEnabled,
                out _,
                out var startFailure,
                experimentCondition:
                    activeExperimentCondition,
                experimentProtocolVersion:
                    activeExperimentProtocolVersion,
                gameMode: LobbyMapCatalog.TelemetryGameMode(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name),
                difficulty: Difficulty.ToString().ToUpperInvariant()))
            {
                Debug.LogWarning($"[Telemetry] MATCH_STARTED was not buffered: {startFailure}.");
                return;
            }

            _matchStartedAtUtc = occurredAtUtc;
            _telemetryMatchActive = true;
            Debug.Log($"[Telemetry] MATCH_STARTED buffered match={MatchId:D}.");
            _aedPhase8Source.BeginMatch(MatchId);
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
            ApplyPhase8DebugEvidenceInvalidation();
            ApplyPhase8InventoryEvidenceInvalidation();
            if (phaseStarted)
            {
                foreach (var publish in _pendingInitialSpawns)
                    publish();
                CaptureExistingPhase8LobbyLoadouts();
            }
            else if (_pendingInitialSpawns.Count != 0)
            {
                MarkPhase8InventoryEvidenceIncomplete("INITIAL_PHASE_NOT_BUFFERED");
            }
            _pendingInitialSpawns.Clear();
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
                MarkUnobservedCoreTransition(transition.State);
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
                    MarkUnobservedCoreTransition(transition.State);
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
                    && _aedObjectiveEvidence.HasRecordedCorePlacementOccurrence(occurrenceKey))
                    return;

                var accepted = _telemetry.ObjectiveAdapter.EmitCoreTransition(
                    occurrenceKey,
                    DateTime.UtcNow,
                    eventType,
                    userId,
                    coreId,
                    out var emittedEvent,
                    out _,
                    Snapshot(transition.Position));

                if (transition.State == NetworkItemState.Carried)
                    RecordMinionRecoveredItem(item.Object.Id, actor,
                        $"core-player-pickup:{occurrenceKey}",
                        _bootstrap?.Runner != null ? _bootstrap.Runner.Tick.Raw : 0L,
                        accepted ? emittedEvent?.Id.ToString("D") : null);

                if (transition.State == NetworkItemState.Placed && accepted
                    && emittedEvent != null
                    && emittedEvent.EventType == TelemetryEventTypes.CorePlaced
                    && emittedEvent.MatchId == MatchId
                    && emittedEvent.UserId == userId)
                {
                    var runner = _bootstrap?.Runner;
                    _aedObjectiveEvidence.RecordCorePlaced(
                        true, emittedEvent.Id, coreId,
                        item.PlacedSectorId.ToString(), item.PlacementSlot,
                        transition.Ordinal, occurrenceKey, userId.ToString("D"),
                        runner != null ? runner.Tick.Raw : 0L,
                        "FusionStateAuthority", _currentTelemetryPhase);
                }
                else if (transition.State == NetworkItemState.Placed)
                    MarkUnobservedCoreTransition(transition.State);
            }
            catch (Exception exception)
            {
                MarkUnobservedCoreTransition(transition.State);
                // Do not rethrow into NetworkPickupItem.TryPlace(): it has
                // already committed the Core pose and carrier state. Rethrowing
                // here would prevent NetworkSectorBox.PlacedCoreCount advancing.
                Debug.LogWarning(
                    $"P04O_TELEMETRY_NONBLOCKING_ERROR|item={item.Object.Id}|" +
                    $"state={transition.State}|kind={exception.GetType().Name}|" +
                    $"message={exception.Message}");
            }
        }

        private void MarkUnobservedCoreTransition(NetworkItemState state)
        {
            if (!_telemetryMatchActive) return;
            if (state == NetworkItemState.Placed)
            {
                _aedObjectiveEvidence.MarkIncomplete();
                _aedv2Evidence.MarkIncomplete();
            }
            else if (state == NetworkItemState.Carried)
                _aedMinionEvidence.MarkIncomplete();
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
                TrySealV02Capture(occurredAtUtc);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var pressure = LastFrozenAEDCurrentPressure;

                Debug.Log(
                    $"[AED_P8][PRESSURE_DIAG] " +
                    $"complete={pressure?.SourceComplete} " +
                    $"level={pressure?.Level} " +
                    $"reasons={string.Join(",", pressure?.ReasonCodes ?? Array.Empty<string>())} " +
                    $"pursuitIncomplete={_aedPursuitEvidence.IsIncomplete} " +
                    $"minionIncomplete={_aedMinionEvidence.IsIncomplete} " +
                    $"minionInvalid={_aedMinionEvidence.IsInvalid} " +
                    $"survivalUsable={_aedSurvivalEvidence.LastFrozen?.IsUsable}");
#endif
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
                TrySealV02Capture(occurredAtUtc);
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
            var matchSnapshot = NetworkMatchState.Instance;
            if (matchSnapshot != null)
            {
                for (int slot = 0; slot < 4; slot++)
                {
                    var entry = matchSnapshot.GetResultRosterEntry(slot);
                    if (!entry.Present || !Guid.TryParse(entry.BackendUserId.ToString(), out var userId)) continue;
                    bool connected = false;
                    foreach (var active in runner.ActivePlayers)
                        if (runner.TryGetPlayerObject(active, out var obj)
                            && obj.TryGetComponent<LobbyPlayerState>(out var state)
                            && state.BackendUserId == entry.BackendUserId && !state.Disconnected)
                            connected = true;
                    players.Add(new SubmitMatchResultPlayerDto
                    {
                        userId = userId.ToString("D"), disconnected = !connected,
                        survived = NetworkPlayerLifeStateRules.SurvivedMatch(entry.LifeStatus,
                            matchSnapshot.HuntReady, _pendingBackendOutcome == "WIN", !connected),
                        downedCount = Mathf.Max(0, entry.DownCount), reviveCount = Mathf.Max(0, entry.ReviveCount),
                        objectiveContribution = entry.HasObjectiveContribution ? 1 : 0
                    });
                }
            }
            foreach (var binding in _boundPlayers)
            {
                // Lobby binding history may contain users who never started.
                // A gameplay snapshot is the roster authority once available.
                if (matchSnapshot != null) continue;
                if (players.Exists(item => item.userId == binding.Value.ToString("D"))) continue;
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
                    var matchState = NetworkMatchState.Instance;
                    survived = NetworkPlayerLifeStateRules.SurvivedMatch(
                        lifeState.Status, matchState != null && matchState.HuntReady,
                        _pendingBackendOutcome == "WIN", disconnected);
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

            foreach (var participant in _migratedResultRoster)
                if (!players.Exists(item => item.userId == participant.Key.ToString("D")))
                    players.Add(new SubmitMatchResultPlayerDto { userId = participant.Key.ToString("D"),
                        disconnected = participant.Value });

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
              if (accepted)
              {
                  _aedv2Evidence.RecordAcceptedDown(occurrenceKey, userId.ToString("D"));
                  if (telemetryEvent != null) _v02DownEvents[userId] = telemetryEvent.Id;
              }
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
            if (accepted && _telemetry != null && _telemetry.IsInitialized)
            {
                _telemetry.TryFlushNow();
            }
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

        public bool TryGetAEDPhase8SourceReferences(uint completedPhaseOrdinal,
            out TelemetrySourceIdentity[] references)
        {
            references = Array.Empty<TelemetrySourceIdentity>();
            if (!HasStateAuthority || !_telemetryMatchActive ||
                _telemetry == null || !_telemetry.IsInitialized ||
                completedPhaseOrdinal == 0 || completedPhaseOrdinal > int.MaxValue)
                return false;
            return _telemetry.TryGetPhaseSourceReferences(
                (int)completedPhaseOrdinal, out references);
        }

        public bool TryGetAEDPhase8SourceSnapshot(uint completedPhaseOrdinal,
            out AEDPhase8AuthoritySourceSnapshotV1 snapshot)
        {
            snapshot = null;
            if (!HasStateAuthority || !_telemetryMatchActive || MatchId == Guid.Empty || completedPhaseOrdinal == 0 ||
                LastFrozenAEDv2Evidence == null || LastFrozenAEDv2Evidence.PhaseOrdinal != completedPhaseOrdinal)
                return false;
            return _aedPhase8Source.TryFreeze(MatchId, completedPhaseOrdinal, out snapshot);
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
            bool phaseSourceWindowValid = true;
            try
            {
                _aedPhase8Source.StartPhase(phaseOrdinal);
            }
            catch (Exception exception)
            {
                phaseSourceWindowValid = false;
                Debug.LogWarning($"[AED_P8][PHASE_WINDOW_INVALID] phase={phase} ordinal={phaseOrdinal} reason={exception.Message}");
            }
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
            ApplyPhase8DebugEvidenceInvalidation();
            ApplyPhase8InventoryEvidenceInvalidation();
            if (!phaseSourceWindowValid)
            {
                MarkPhase8InventoryEvidenceIncomplete("PHASE_WINDOW_INVALID");
                _aedMinionEvidence.MarkIncomplete();
            }
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
