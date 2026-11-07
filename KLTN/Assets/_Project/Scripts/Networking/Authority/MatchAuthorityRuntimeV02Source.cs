using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Minions;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.Auth;
using EchoProtocol.Tools.Scanner;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking.Authority
{
    public sealed partial class MatchAuthorityRuntime
    {
        public string V02CurrentSourcePhase => _currentTelemetryPhase;
        public bool V02CollectToolOutcomes => HasStateAuthority && _telemetryMatchActive && _researchCaptureEnabled &&
            RequestedScenarioResolutionMode == ScenarioResolutionMode.Adaptive && Difficulty == EchoProtocol.Gameplay.MatchDifficulty.Normal;
        private Guid _v02CaptureMatch;
        private string _v02CaptureContext;
        private long _v02NextWorldTick;
        private long _v02NextSourcePollTick;
        private bool _v02SourcePolling;
        private int _v02SourceGeneration;
        private readonly Queue<long> _v02WorldTicks = new Queue<long>();
        public AEDV02SourceResultDto LastVerifiedV02Source { get; private set; }
        public string V02SourceHoldReason { get; private set; }
        private bool _v02CarrierEvidenceInvalid;
        private bool _v02ExclusionEnqueued, _v02SealEnqueued;
        private int _v02CarrierOrdinal;
        private int _v02CabinetClueOrdinal;
        private readonly Dictionary<PlayerRef, (string Unit, long Tick)> _v02ObservedObjectives = new Dictionary<PlayerRef, (string, long)>();
        private readonly Dictionary<PlayerRef, (Guid EventId, AEDV02CausalImpact Impact)> _v02Impacts = new Dictionary<PlayerRef, (Guid, AEDV02CausalImpact)>();
        private readonly Dictionary<Guid, Guid> _v02DownEvents = new Dictionary<Guid, Guid>();
        private string V02CaptureExclusion => Phase8DebugEvidenceInvalidated ? "DEBUG_GAMEPLAY" :
            Phase8InventoryEvidenceInvalidated ? "INVENTORY_LINEAGE_INVALID" : _v02CarrierEvidenceInvalid ? "SOURCE_INCOMPLETE" : "NONE";
        private void TrySealV02Capture(DateTime occurredAtUtc)
        {
            var settings = ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings;
            if (_v02SealEnqueued || !_researchCaptureEnabled || !_telemetryMatchActive || !HasStateAuthority ||
                RequestedScenarioResolutionMode != ScenarioResolutionMode.Adaptive || Difficulty != EchoProtocol.Gameplay.MatchDifficulty.Normal ||
                settings == null || !settings.PacingV02GameplayEnabled && !settings.PacingV02ShadowEnabled || _telemetry.Phase8LifecycleAdapter == null) return;
            _v02SealEnqueued = _telemetry.Phase8LifecycleAdapter.EmitAEDV02Seal("v02:capture-seal:" + MatchId.ToString("D"),
                occurredAtUtc, _currentTelemetryPhase, V02CaptureExclusion, out _, out _);
            // A missing seal prevents historical learning; it must not prevent baseline match closure.
        }
        public bool TryReadCurrentV02World(out AEDV02WorldSample world)
        {
            world = null;
            return HasStateAuthority && NetworkMatchState.Instance != null && TryBuildV02World(NetworkMatchState.Instance, out world, out _);
        }
        public bool IsV02NeedCurrent(AEDV02SupportAction action)
        {
            if (action == null || action.UserId == Guid.Empty || action.CauseEventId == Guid.Empty || action.ToolId is not (1 or 2 or 3 or 4 or 6) ||
                action.Zone is < 1 or > 3 || action.Kind is not ("EMERGENCY" or "ORDINARY") || _bootstrap?.Runner == null || !TryReadCurrentV02World(out var world)) return false;
            var player = world.Players.SingleOrDefault(x => x.UserId == action.UserId);
            if (player == null) return false;
            if (action.Kind == "EMERGENCY") return player.LifeState == "DOWNED" && !player.IsReviving &&
                _v02DownEvents.TryGetValue(action.UserId, out var cause) && cause == action.CauseEventId;
            return player.LifeState == "ALIVE" && _v02Impacts.Values.Any(x => x.EventId == action.CauseEventId && x.Impact.UserId == action.UserId &&
                x.Impact.Kind == "MINION_TOOL_LOSS" && x.Impact.ToolId == action.ToolId && x.Impact.UntilTick >= world.Tick);
        }
        private void RecordV02CausalImpact(PlayerRef player, string entity, string kind, string occurrence, long tick, string toolInstance = null)
        {
            var settings = ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings;
            var runner = _bootstrap?.Runner;
            if (!HasStateAuthority || !_telemetryMatchActive || !_researchCaptureEnabled || runner == null ||
                RequestedScenarioResolutionMode != ScenarioResolutionMode.Adaptive || Difficulty != EchoProtocol.Gameplay.MatchDifficulty.Normal ||
                settings == null || !settings.PacingV02GameplayEnabled && !settings.PacingV02ShadowEnabled ||
                !TryResolveBackendUser(player, out var user) || !runner.TryGetPlayerObject(player, out var obj)) return;
            long until = tick;
            if (kind == "MINION_SLOW" && obj.TryGetComponent<NetworkPlayerMovement>(out var movement)) until = movement.V02SlowUntilTick;
            var impact = new AEDV02CausalImpact { UserId = user, Kind = kind, EntityId = entity, Tick = tick, UntilTick = Math.Max(tick, until) };
            if (_v02ObservedObjectives.TryGetValue(player, out var previous) && tick - previous.Tick <= runner.TickRate * 2)
                impact.ObjectiveUnitId = previous.Unit;
            if (kind == "MINION_TOOL_LOSS")
            {
                if (toolInstance == null || !_aedPhase8Source.TryGetLatestTool(toolInstance, out var tool)) { _v02CarrierEvidenceInvalid = true; return; }
                impact.ToolId = tool.ToolId; impact.ToolInstanceId = toolInstance;
                impact.UntilTick = tick + (long)(runner.TickRate * AEDV02PilotOpportunities.ResourceLossNeedSeconds);
            }
            if (!impact.IsValid() || _telemetry.Phase8LifecycleAdapter == null ||
                !_telemetry.Phase8LifecycleAdapter.EmitAEDV02Impact("v02:impact:" + kind + ":" + occurrence, DateTime.UtcNow,
                    _currentTelemetryPhase, AEDV02SourceJson.Impact(impact), out var emitted, out _))
            { _v02CarrierEvidenceInvalid = true; return; }
            _v02Impacts[player] = (emitted.Id, impact);
        }
        private void TryRecordV02ResourceAction(PlayerRef actor, int toolId, string toolInstanceId, string action,
            string lifecycleOccurrenceKey, long tick)
        {
            if (!V02CollectToolOutcomes || _telemetry.Phase8LifecycleAdapter == null ||
                !TryResolveBackendUser(actor, out var actorUserId) || !TryReadCurrentV02World(out var world)) return;
            var needs = world.Players.Where(player => player.RequiredToolId == toolId &&
                Guid.TryParse(player.ResourceCauseEventId, out var causeId) &&
                _v02Impacts.Values.Any(impact => impact.EventId == causeId && impact.Impact.Kind == "MINION_TOOL_LOSS" &&
                    impact.Impact.UserId == player.UserId && impact.Impact.ToolId == toolId && impact.Impact.UntilTick >= tick))
                .ToArray();
            if (needs.Length != 1) return;
            var need = needs[0];
            if (action != "PICKUP" && need.UserId != actorUserId) return;
            if (!Guid.TryParse(need.ResourceCauseEventId, out var causeEventId) ||
                !_v02Impacts.Values.Any(x => x.EventId == causeEventId && x.Impact.Tick <= tick && x.Impact.UntilTick >= tick))
                return;
            var resourceAction = new AEDV02ResourceManagementAction
            {
                CauseEventId = causeEventId,
                ActorUserId = actorUserId,
                Action = action,
                ToolInstanceId = toolInstanceId,
                LifecycleOccurrenceKey = lifecycleOccurrenceKey,
                ToolId = toolId,
                Tick = tick
            };
            if (!resourceAction.IsValid() || !_telemetry.Phase8LifecycleAdapter.EmitAEDV02ResourceAction(
                    "v02:resource-action:" + AEDV02Plan.Hash(causeEventId.ToString("D") + "|" + lifecycleOccurrenceKey),
                    DateTime.UtcNow, _currentTelemetryPhase, AEDV02SourceJson.ResourceManagementAction(resourceAction),
                    out _, out _))
            {
                _v02CarrierEvidenceInvalid = true;
                return;
            }
            _telemetry.TryFlushNow();
        }
        public bool RecordV02CabinetClue(AEDV02CabinetClueSource clue, out Guid eventId)
        {
            eventId = Guid.Empty;
            var settings = ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings;
            if (!HasStateAuthority || !_telemetryMatchActive || !_researchCaptureEnabled || RequestedScenarioResolutionMode != ScenarioResolutionMode.Adaptive ||
                Difficulty != EchoProtocol.Gameplay.MatchDifficulty.Normal || settings == null || !settings.PacingV02GameplayEnabled && !settings.PacingV02ShadowEnabled ||
                clue == null || !clue.IsValid() || !_boundPlayers.Values.Contains(clue.UserId) || _telemetry.Phase8LifecycleAdapter == null) return false;
            if (!_telemetry.Phase8LifecycleAdapter.EmitAEDV02CabinetClue("v02:cabinet-clue:" + ++_v02CabinetClueOrdinal, DateTime.UtcNow,
                _currentTelemetryPhase, AEDV02SourceJson.CabinetClue(clue), out var occurrence, out _)) return false;
            eventId = occurrence.Id; _v02NextWorldTick = 0; _v02NextSourcePollTick = _bootstrap.Runner.Tick.Raw + _bootstrap.Runner.TickRate * 2;
            _telemetry.TryFlushNow(); return true;
        }

        public void RecordV02ToolCarrier(NetworkInteractable item, NetworkId minion, bool stolen)
        {
            var settings = ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings;
            if (!HasStateAuthority || !_telemetryMatchActive || !_researchCaptureEnabled || RequestedScenarioResolutionMode != ScenarioResolutionMode.Adaptive ||
                Difficulty != EchoProtocol.Gameplay.MatchDifficulty.Normal || settings == null || !settings.PacingV02GameplayEnabled && !settings.PacingV02ShadowEnabled ||
                item == null || item.Runner != _bootstrap.Runner || item is not NetworkTeamToolPickup && item is not NetworkToolPickup) return;
            var carrier = new AEDV02ToolCarrierSource { ToolInstanceId = item.Object.Id.ToString(), MinionNetworkId = minion.ToString(),
                Action = stolen ? "STEAL" : "DROP", Tick = _bootstrap.Runner.Tick.Raw };
            if (!_telemetry.Phase8LifecycleAdapter.EmitAEDV02Carrier("v02:carrier:" + ++_v02CarrierOrdinal, DateTime.UtcNow, _currentTelemetryPhase,
                AEDV02SourceJson.Carrier(carrier), out _, out _)) _v02CarrierEvidenceInvalid = true;
        }

        private void TickV02WorldSource(NetworkMatchState matchState)
        {
            var settings = ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings;
            var runner = _bootstrap?.Runner;
            if (!_telemetryMatchActive || !HasStateAuthority || matchState == null || matchState.IsEnded || runner == null ||
                matchState.IsManagedFixedScenario(MatchId) ||
                RequestedScenarioResolutionMode != ScenarioResolutionMode.Adaptive || Difficulty != EchoProtocol.Gameplay.MatchDifficulty.Normal ||
                settings == null || !settings.PacingV02GameplayEnabled && !settings.PacingV02ShadowEnabled) return;
            long tick = runner.Tick.Raw;
            if (_v02CaptureMatch != MatchId)
            {
                _v02CaptureMatch = MatchId; _v02CaptureContext = null; _v02NextWorldTick = 0; _v02NextSourcePollTick = 0;
                _v02WorldTicks.Clear(); LastVerifiedV02Source = null;
                _v02ExclusionEnqueued = false; _v02SealEnqueued = false;
            }
            if (V02CaptureExclusion != "NONE")
            {
                if (!_v02ExclusionEnqueued && _researchCaptureEnabled && _telemetry.Phase8LifecycleAdapter != null)
                    _v02ExclusionEnqueued = _telemetry.Phase8LifecycleAdapter.EmitAEDV02Exclusion("v02:capture-excluded:" + MatchId.ToString("D"),
                        DateTime.UtcNow, _currentTelemetryPhase, V02CaptureExclusion, out _, out _);
                HoldV02Source("HOLD_SOURCE_CAPTURE_EXCLUDED", true); return;
            }
            if (tick < _v02NextWorldTick) return;
            _v02NextWorldTick = tick + runner.TickRate;
            if (!TryBuildV02World(matchState, out var sample, out var reason)) { HoldV02Source(reason, true); return; }
            string context = sample.PhaseOrdinal + ":" + sample.PlanRevision + ":" + sample.RosterIdentity;
            if (_v02CaptureContext != context)
            {
                _v02CaptureContext = context; _v02WorldTicks.Clear(); LastVerifiedV02Source = null; AEDv2Authority.InvalidateV02Source();
            }
            if (!_telemetry.Phase8LifecycleAdapter.EmitAEDV02World("v02:world:" + tick, DateTime.UtcNow, sample.PhaseName,
                AEDV02SourceJson.World(sample), out _, out _)) { HoldV02Source("HOLD_WORLD_SOURCE_ENQUEUE_FAILED"); return; }
            _v02WorldTicks.Enqueue(tick);
            while (_v02WorldTicks.Count != 0 && tick - _v02WorldTicks.Peek() >
                runner.TickRate * (_v02Configuration?.windowSeconds ?? AEDV02PilotPolicy.WindowSeconds)) _v02WorldTicks.Dequeue();
            _telemetry.TryFlushNow();
            if (_v02SourcePolling || tick < _v02NextSourcePollTick) return;
            var end = _v02WorldTicks.Where(x => tick - x >= 2 * runner.TickRate).DefaultIfEmpty(-1).Max();
            bool emergency = sample.Players.Any(x => x.LifeState == "DOWNED");
            if (end < 0 || end - _v02WorldTicks.Peek() < runner.TickRate * (emergency ? AEDV02PilotOpportunities.ScarcityMinimumSeconds
                : (_v02Configuration?.observationSeconds ?? AEDV02PilotPolicy.ObservationSeconds))) return;
            _v02NextSourcePollTick = tick + 5 * runner.TickRate;
            PollV02Source(MatchId, context, new AEDV02SourceRequestDto { phaseOrdinal = sample.PhaseOrdinal, startTick = _v02WorldTicks.Peek(),
                endTick = end, sceneManifestFingerprint = sample.SceneManifestFingerprint, allowEmergencyWindow = emergency });
        }

        private async void PollV02Source(Guid match, string context, AEDV02SourceRequestDto request)
        {
            _v02SourcePolling = true;
            int generation = _v02SourceGeneration;
            try
            {
                var api = new AEDV02ApiService(AuthRuntime.EnsureExists().Client);
                var response = await api.Sources(match, request);
                if (MatchId != match || !HasStateAuthority || context != _v02CaptureContext || generation != _v02SourceGeneration) return;
                if (response == null || !response.IsSuccess || response.Data == null || !response.Data.success || response.Data.data == null)
                { HoldV02Source(response?.Data?.message ?? response?.Message ?? "HOLD_BACKEND_SOURCE_NOT_VERIFIED"); return; }
                LastVerifiedV02Source = response.Data.data;
                // Shadow retains the verified diagnostic; only an approved policy envelope can feed gameplay pressure.
                if (!LastVerifiedV02Source.pressureComplete) AEDv2Authority.InvalidateV02Source();
                else if (AEDv2Authority.TryGetV02(match, out _, out _))
                    if (!AEDv2Authority.AcceptV02Source(match, LastVerifiedV02Source, AEDv2RosterIdentity,
                        _boundPlayers.Values, DateTime.UtcNow)) { HoldV02Source("HOLD_SOURCE_CURRENCY_CHANGED"); return; }
                foreach (var user in _boundPlayers.Values)
                    AEDv2Authority.V02StableTier(match, user, DateTime.UtcNow, _bootstrap.Runner.Tick.Raw / (double)_bootstrap.Runner.TickRate);
                V02SourceHoldReason = null;
                Debug.Log($"[AED_V02][SOURCE_VERIFIED] match={match:D} phase={request.phaseOrdinal} ticks={request.startTick}-{request.endTick} revision={LastVerifiedV02Source.planRevision} fingerprint={LastVerifiedV02Source.fingerprint} pressureComplete={LastVerifiedV02Source.pressureComplete} missing={string.Join(",", LastVerifiedV02Source.missingChannels ?? Array.Empty<string>())}");
            }
            catch (Exception ex) { if (MatchId == match) HoldV02Source("HOLD_SOURCE_REQUEST_FAILED:" + ex.GetType().Name); }
            finally { if (generation == _v02SourceGeneration) _v02SourcePolling = false; }
        }
        private void HoldV02Source(string reason, bool resetCoverage = false)
        {
            LastVerifiedV02Source = null; AEDv2Authority.InvalidateV02Source();
            if (resetCoverage) _v02WorldTicks.Clear();
            if (V02SourceHoldReason != reason) Debug.LogWarning($"[AED_V02][SOURCE_HOLD] match={MatchId:D} reason={reason}");
            V02SourceHoldReason = reason;
        }
        private void ResetV02WorldSource()
        {
            EchoProtocol.TeamTools.TeamToolWorldSpawn.ResetV02(_v02CaptureMatch);
            _v02SourceGeneration++; _v02CaptureMatch = Guid.Empty; _v02CaptureContext = null; _v02NextWorldTick = 0;
            _v02NextSourcePollTick = 0; _v02SourcePolling = false; _v02WorldTicks.Clear(); LastVerifiedV02Source = null;
            V02SourceHoldReason = null;
            _v02CarrierEvidenceInvalid = false; _v02CarrierOrdinal = 0;
            _v02ExclusionEnqueued = false; _v02SealEnqueued = false;
            _v02CabinetClueOrdinal = 0;
            _v02ObservedObjectives.Clear(); _v02Impacts.Clear(); _v02DownEvents.Clear();
        }

        private bool TryBuildV02World(NetworkMatchState matchState, out AEDV02WorldSample sample, out string reason)
        {
            sample = null; reason = "HOLD_WORLD_CENSUS_INCOMPLETE";
            var runner = _bootstrap.Runner;
            if (!_researchCaptureEnabled || _v02CarrierEvidenceInvalid || _telemetry.Phase8LifecycleAdapter == null || Phase8InventoryEvidenceInvalidated || Phase8DebugEvidenceInvalidated ||
                !AEDV02SceneSource.TryLoad(out var manifest, out var sceneFingerprint)) return false;
            if (!UnityEngine.SceneManagement.SceneManager.GetSceneByPath(manifest.ScenePath).isLoaded) { reason = "HOLD_SCENE_CONTENT_MISMATCH"; return false; }
            int logical = AEDV02PhaseMap.LogicalPhase(_currentTelemetryPhase);
            if (logical == 0 || _boundPlayers.Count == 0) return false;
            var stalkers = FindObjectsByType<StalkerFusionRuntime>(FindObjectsInactive.Exclude).Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid).ToArray();
            if (stalkers.Length == 0 || stalkers.Any(x => !x.Object.HasStateAuthority)) return false;
            AEDV02Plan plan; int revision;
            if (!AEDv2Authority.TryGetV02(MatchId, out plan, out revision))
            {
                plan = AEDV02Plan.Normal(); revision = 0;
                foreach (var key in Enum.GetValues(typeof(AEDV02Key)).Cast<AEDV02Key>().Where(x => (int)x < 6))
                {
                    if (!stalkers[0].TryReadV02ConsumerValue(key, out var value)) return false;
                    try { plan = plan.With(key, AEDV02Plan.AllowedValues(key).Single(x => Math.Abs(x - value) < .000001)); }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { reason = "HOLD_UNREGISTERED_RUNTIME_TUNING"; return false; }
                }
            }
            else if (stalkers.Any(x => x.V02AppliedRevision != revision || x.V02ConsumerMatchId.ToString() != MatchId.ToString("D")))
            { reason = "HOLD_CONSUMER_NOT_OBSERVED"; return false; }
            var stalkerSources = new List<AEDV02ThreatSource>();
            foreach (var stalker in stalkers)
            {
                var observed = Threat(stalker.Object, stalker.HasActiveSpecialEncounter ? "SPECIAL" :
                    stalker.ReplicatedState == StalkerState.DETECT ? "DETECTION" : stalker.ReplicatedState.ToString(), stalker.TargetPlayer, Math.Min(logical, 3));
                observed.ConsumerValues = new double[6];
                if (stalker.TryReadV02JammerBlock(out var blocker, out var progress))
                { observed.JammerBlockerId = blocker; observed.JammerProgress = progress; }
                if (stalker.TryReadV02NoiseDiversion(out var noiseId, out var destination))
                {
                    observed.InvestigationNoiseId = noiseId;
                    observed.DestinationX = destination.x; observed.DestinationY = destination.y; observed.DestinationZ = destination.z;
                }
                for (int key = 0; key < observed.ConsumerValues.Length; key++)
                    if (!stalker.TryReadV02ConsumerValue((AEDV02Key)key, out observed.ConsumerValues[key]) ||
                        Math.Abs(observed.ConsumerValues[key] - plan.Get((AEDV02Key)key)) >= .000001)
                    { reason = "HOLD_CONSUMER_NOT_OBSERVED"; return false; }
                stalkerSources.Add(observed);
            }
            var players = new List<AEDV02PlayerSource>(); var tools = new List<AEDV02ToolSource>();
            var scans = new List<AEDV02ScanSource>();
            AEDV02ScanCandidate[] scannables = null;
            foreach (var player in runner.ActivePlayers)
            {
                if (!TryResolveVerifiedBackendUserId(player, out var identity) || !Guid.TryParse(identity, out var user) ||
                    !runner.TryGetPlayerObject(player, out var obj) || !obj.TryGetComponent<NetworkPlayerLifeState>(out var life) ||
                    !obj.TryGetComponent<LobbyPlayerState>(out var lobby) || !lobby.IsGameplayPlayer || !obj.TryGetComponent<NetworkPlayerMovement>(out var movement)) return false;
                _aedPhase8Source.TryGetHeldTool(identity, out var held);
                if ((held?.ToolId ?? 0) != lobby.ToolId) return false;
                var position = obj.transform.position;
                if (!matchState.TryGetV02ObjectiveSource(player, out var objective)) { reason = "HOLD_OBJECTIVE_SOURCE_REQUIRED"; return false; }
                if (lobby.CarriedCoreId.IsValid) objective = "core:" + lobby.CarriedCoreId;
                var playerSource = new AEDV02PlayerSource { UserId = user, LifeState = life.IsEliminated ? "ELIMINATED" : life.IsDowned ? "DOWNED" : "ALIVE",
                    IsCaught = life.IsCaught, IsReviving = life.IsReviveInProgress,
                    ReviverUserId = life.IsReviveInProgress && TryResolveBackendUser(life.Reviver, out var reviver) ? reviver : (Guid?)null,
                    ObjectiveUnitId = objective, ObjectiveState = objective.Length == 0 ? "NONE" : "OPERATING",
                    HeldToolInstanceId = held?.InstanceId ?? string.Empty, HideSpotId = movement.CurrentHideSpotId.ToString(),
                    SlowUntilTick = movement.V02SlowUntilTick, X = position.x, Y = position.y, Z = position.z };
                var interactor = obj.GetComponent<NetworkPlayerInteractor>();
                playerSource.StabilizerUntilTick = interactor != null ? interactor.V02StabilizerUntilTick : 0;
                playerSource.StabilizerActivationOrdinal = interactor != null ? interactor.V02StabilizerActivationOrdinal : 0;
                playerSource.StabilizerBuffed = lobby.IsStabilizerBuffed;
                if (objective.Length != 0) _v02ObservedObjectives[player] = (objective, runner.Tick.Raw);
                if (_v02Impacts.TryGetValue(player, out var blocked))
                {
                    var impact = blocked.Impact;
                    bool constrained = life.IsDowned || life.IsCaught || movement.V02SlowUntilTick > runner.Tick.Raw ||
                        impact.Kind == "MINION_CORE_DROP" && FindObjectsByType<CreepMinionRuntime>(FindObjectsInactive.Exclude)
                            .Any(x => x.Runner == runner && x.Object != null && x.Object.IsValid && x.Object.Id.ToString() == impact.EntityId &&
                                x.TargetPlayer == player && x.State is CreepMinionState.Track or CreepMinionState.Harass);
                    if (objective.Length == 0 && impact.ObjectiveUnitId.Length != 0 && constrained &&
                        !players.Any(x => x.ObjectiveUnitId == impact.ObjectiveUnitId && x.ObjectiveState == "OPERATING"))
                    {
                        playerSource.ObjectiveState = "THREAT_INTERRUPTED"; playerSource.ObjectiveUnitId = impact.ObjectiveUnitId;
                        playerSource.ObjectiveCauseEntityId = impact.EntityId; playerSource.ObjectiveCauseEventId = blocked.EventId.ToString("D");
                    }
                    if (impact.Kind == "MINION_TOOL_LOSS" && runner.Tick.Raw <= impact.UntilTick)
                    { playerSource.RequiredToolId = impact.ToolId; playerSource.ResourceCauseEventId = blocked.EventId.ToString("D"); }
                }
                players.Add(playerSource);
                var scanner = obj.GetComponent<NetworkFieldScanner>();
                if (scanner == null) { reason = "HOLD_SCANNER_WORLD_CENSUS_REQUIRED"; return false; }
                if (scannables == null) scannables = scanner.ReadV02WorldTargets();
                if (scanner != null && scanner.V02LastScan is { } scan && scan.MatchId == MatchId && scan.PhaseName == _currentTelemetryPhase)
                { scan.UserId = user; scans.Add(scan); }
                if (held != null) tools.Add(Tool(held, position, string.Empty));
            }
            if (!players.Select(x => x.UserId).ToHashSet().SetEquals(_boundPlayers.Values)) return false;
            foreach (var pickup in FindObjectsByType<NetworkTeamToolPickup>(FindObjectsInactive.Exclude).Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid && x.IsAvailableWorldSource))
            {
                if (!pickup.Object.HasStateAuthority || !_aedPhase8Source.TryGetLatestTool(pickup.Object.Id.ToString(), out var source) || source.Location != "World") return false;
                if (source.ToolId is 2 or 4 && source.RemainingCharges != pickup.RemainingUses) return false;
                var tool = Tool(source, pickup.V02WorldSourcePosition, pickup.V02SourceMarkerId ?? AEDV02SceneSource.MarkerId(pickup.transform));
                tool.CarrierMinionId = pickup.MonsterToolCarrierId.IsValid ? pickup.MonsterToolCarrierId.ToString() : string.Empty;
                tools.Add(tool);
            }
            foreach (var pickup in FindObjectsByType<NetworkToolPickup>(FindObjectsInactive.Exclude).Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid && !x.IsPickedUp))
            {
                if (!pickup.Object.HasStateAuthority || !_aedPhase8Source.TryGetLatestTool(pickup.Object.Id.ToString(), out var source) || source.Location != "World") return false;
                if (source.ToolId is 2 or 4 && source.RemainingCharges != pickup.RemainingUses) return false;
                var tool = Tool(source, pickup.V02WorldSourcePosition, pickup.V02SourceMarkerId ?? AEDV02SceneSource.MarkerId(pickup.transform));
                tool.CarrierMinionId = pickup.MonsterToolCarrierId.IsValid ? pickup.MonsterToolCarrierId.ToString() : string.Empty;
                tools.Add(tool);
            }
            if (!tools.Select(x => x.InstanceId).ToHashSet().SetEquals(_aedPhase8Source.CurrentLiveTools.Select(x => x.InstanceId))) return false;
            var minions = FindObjectsByType<CreepMinionRuntime>(FindObjectsInactive.Exclude).Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid).ToArray();
            if (!AEDV02SceneSource.TryGetDoorSources(runner, matchState, out var doors, out reason))
                return false;
            var jammers = new List<AEDV02JammerSource>();
            foreach (var jammer in FindObjectsByType<NetworkDoorJammer>(FindObjectsInactive.Exclude)
                .Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid && x.V02Occurrence.Length != 0 && x.V02DeploymentMatch == MatchId))
            {
                if (!jammer.Object.HasStateAuthority || !players.Any(x => x.UserId == jammer.V02DeployerUser)) return false;
                string door = string.Empty;
                if (jammer.DoorId.IsValid)
                {
                    if (!runner.TryFindObject(jammer.DoorId, out var doorObject)) return false;
                    door = AEDV02SceneSource.MarkerId(doorObject.transform);
                }
                var p = jammer.transform.position;
                jammers.Add(new AEDV02JammerSource { MatchId = MatchId, PhaseName = jammer.V02DeploymentPhase,
                    NetworkId = jammer.Object.Id.ToString(), UserId = jammer.V02DeployerUser,
                    Occurrence = jammer.V02Occurrence, DeploymentTick = jammer.V02DeploymentTick, DoorId = door,
                    Active = jammer.IsActive, ColliderBlocking = jammer.V02ColliderBlocking, BreakSeconds = jammer.BreakDurationSeconds,
                    X = p.x, Y = p.y, Z = p.z });
            }
            foreach (var interrupted in players.Where(x => x.ObjectiveState == "THREAT_INTERRUPTED" &&
                players.Any(other => other.UserId != x.UserId && other.ObjectiveUnitId == x.ObjectiveUnitId && other.ObjectiveState == "OPERATING")))
            { interrupted.ObjectiveState = "NONE"; interrupted.ObjectiveUnitId = string.Empty; interrupted.ObjectiveCauseEntityId = string.Empty; interrupted.ObjectiveCauseEventId = string.Empty; }
            sample = new AEDV02WorldSample { OpportunityContractVersion = AEDV02PilotOpportunities.Version,
                ResolutionMode = "Adaptive", Difficulty = "Normal", Tick = runner.Tick.Raw, TickRate = runner.TickRate,
                PhaseOrdinal = (int)V02EvidencePhaseOrdinal, PhaseName = _currentTelemetryPhase, LogicalPhase = logical, Zone = Math.Min(logical, 3),
                PlanRevision = revision, PlanFingerprint = plan.Fingerprint(), RosterIdentity = CurrentRosterIdentity(), SceneManifestFingerprint = sceneFingerprint,
                Players = players.ToArray(), Tools = tools.ToArray(), Doors = doors, Jammers = jammers.ToArray(), Scans = scans.ToArray(), Scannables = scannables,
                Supply = EchoProtocol.TeamTools.TeamToolWorldSpawn.ReadV02Supply(runner, MatchId),
                Stalkers = stalkerSources.ToArray(),
                Minions = minions.Select(x => Threat(x.Object, x.IsDying ? "DYING" : x.State == CreepMinionState.Roam ? "IDLE" : x.State.ToString().ToUpperInvariant(), x.TargetPlayer, (int)x.Zone)).ToArray() };
            if (!sample.IsValid()) return false;
            var stabilizers = new List<NetworkPlayerInteractor>();
            foreach (var player in runner.ActivePlayers)
                if (runner.TryGetPlayerObject(player, out var playerObject) && playerObject.TryGetComponent<NetworkPlayerInteractor>(out var interactor) && interactor.IsCoreStabilizerActive)
                    stabilizers.Add(interactor);
            foreach (var player in sample.Players)
            {
                bool covered = player.LifeState == "ALIVE" && !player.IsCaught && stabilizers.Any(x =>
                    x.IsCoreStabilizerCovering(new Vector3((float)player.X, (float)player.Y, (float)player.Z)));
                if (covered != player.StabilizerBuffed)
                { reason = "HOLD_STABILIZER_BUFF_NOT_RECONCILED"; sample = null; return false; }
            }
            return true;
        }
        private static AEDV02ToolSource Tool(AEDPhase8ToolSourceV1 source, Vector3 position, string marker) => new AEDV02ToolSource
        {
            InstanceId = source.InstanceId, ToolId = source.ToolId, Zone = source.Zone, Origin = source.Origin, Location = source.Location,
            OwnerUserId = Guid.TryParse(source.OwnerUserId, out var owner) ? owner : (Guid?)null, RemainingCharges = source.RemainingCharges,
            MarkerId = marker, X = position.x, Y = position.y, Z = position.z
        };
        private AEDV02ThreatSource Threat(NetworkObject obj, string state, PlayerRef target, int zone)
        {
            Guid? user = state is "DETECTION" or "CHASE" or "ATTACK" or "SPECIAL" or "TRACK" or "HARASS" && target.IsRealPlayer &&
                TryResolveVerifiedBackendUserId(target, out var identity) && Guid.TryParse(identity, out var resolved) ? resolved : (Guid?)null;
            var id = obj.Id.ToString();
            string episode = string.Empty;
            if (user.HasValue)
                if (!_aedPursuitEvidence.TryGetActiveEpisodeId(id, user.Value.ToString("D"), out episode))
                    _aedMinionEvidence.TryGetActiveEpisodeId(id, user.Value.ToString("D"), out episode);
            var position = obj.transform.position;
            return new AEDV02ThreatSource { NetworkId = id, State = state, TargetUserId = user, EpisodeId = episode ?? string.Empty, Zone = zone,
                X = position.x, Y = position.y, Z = position.z };
        }
    }
}
