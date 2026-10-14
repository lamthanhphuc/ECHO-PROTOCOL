using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Minions;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.Auth;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking.Authority
{
    public sealed partial class MatchAuthorityRuntime
    {
        private Guid _v02CaptureMatch;
        private string _v02CaptureContext;
        private long _v02NextWorldTick;
        private long _v02NextSourcePollTick;
        private bool _v02SourcePolling;
        private int _v02SourceGeneration;
        private readonly Queue<long> _v02WorldTicks = new Queue<long>();
        public AEDV02SourceResultDto LastVerifiedV02Source { get; private set; }
        public string V02SourceHoldReason { get; private set; }

        private void TickV02WorldSource(NetworkMatchState matchState)
        {
            var settings = ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings;
            var runner = _bootstrap?.Runner;
            if (!_telemetryMatchActive || !HasStateAuthority || matchState == null || matchState.IsEnded || runner == null ||
                RequestedScenarioResolutionMode != ScenarioResolutionMode.Adaptive || Difficulty != EchoProtocol.Gameplay.MatchDifficulty.Normal ||
                settings == null || !settings.PacingV02GameplayEnabled && !settings.PacingV02ShadowEnabled) return;
            long tick = runner.Tick.Raw;
            if (_v02CaptureMatch != MatchId)
            {
                _v02CaptureMatch = MatchId; _v02CaptureContext = null; _v02NextWorldTick = 0; _v02NextSourcePollTick = 0;
                _v02WorldTicks.Clear(); LastVerifiedV02Source = null;
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
            while (_v02WorldTicks.Count != 0 && tick - _v02WorldTicks.Peek() > runner.TickRate * AEDV02PilotPolicy.WindowSeconds) _v02WorldTicks.Dequeue();
            _telemetry.TryFlushNow();
            if (_v02SourcePolling || tick < _v02NextSourcePollTick) return;
            var end = _v02WorldTicks.Where(x => tick - x >= 2 * runner.TickRate).DefaultIfEmpty(-1).Max();
            if (end < 0 || end - _v02WorldTicks.Peek() < runner.TickRate * AEDV02PilotPolicy.ObservationSeconds) return;
            _v02NextSourcePollTick = tick + 5 * runner.TickRate;
            PollV02Source(MatchId, context, new AEDV02SourceRequestDto { phaseOrdinal = sample.PhaseOrdinal, startTick = _v02WorldTicks.Peek(),
                endTick = end, sceneManifestFingerprint = sample.SceneManifestFingerprint });
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
            _v02SourceGeneration++; _v02CaptureMatch = Guid.Empty; _v02CaptureContext = null; _v02NextWorldTick = 0;
            _v02NextSourcePollTick = 0; _v02SourcePolling = false; _v02WorldTicks.Clear(); LastVerifiedV02Source = null;
            V02SourceHoldReason = null;
        }

        private bool TryBuildV02World(NetworkMatchState matchState, out AEDV02WorldSample sample, out string reason)
        {
            sample = null; reason = "HOLD_WORLD_CENSUS_INCOMPLETE";
            var runner = _bootstrap.Runner;
            if (!_researchCaptureEnabled || _telemetry.Phase8LifecycleAdapter == null || Phase8InventoryEvidenceInvalidated || Phase8DebugEvidenceInvalidated ||
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
            else if (stalkers.Any(x => x.V02AppliedRevision != revision)) { reason = "HOLD_CONSUMER_NOT_OBSERVED"; return false; }
            var players = new List<AEDV02PlayerSource>(); var tools = new List<AEDV02ToolSource>();
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
                players.Add(new AEDV02PlayerSource { UserId = user, LifeState = life.IsEliminated ? "ELIMINATED" : life.IsDowned ? "DOWNED" : "ALIVE",
                    IsCaught = life.IsCaught, IsReviving = life.IsReviveInProgress,
                    ObjectiveUnitId = objective, ObjectiveState = objective.Length == 0 ? "NONE" : "OPERATING",
                    HeldToolInstanceId = held?.InstanceId ?? string.Empty, HideSpotId = movement.CurrentHideSpotId.ToString(), X = position.x, Y = position.y, Z = position.z });
                if (held != null) tools.Add(Tool(held, position, string.Empty));
            }
            if (!players.Select(x => x.UserId).ToHashSet().SetEquals(_boundPlayers.Values)) return false;
            foreach (var pickup in FindObjectsByType<NetworkTeamToolPickup>(FindObjectsInactive.Exclude).Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid && x.IsAvailableWorldSource))
            {
                if (!pickup.Object.HasStateAuthority || !_aedPhase8Source.TryGetLatestTool(pickup.Object.Id.ToString(), out var source) || source.Location != "World") return false;
                if (pickup.MonsterToolCarrierId.IsValid) { reason = "HOLD_MINION_TOOL_TRANSFER_SOURCE_REQUIRED"; return false; }
                if (source.ToolId is 2 or 4 && source.RemainingCharges != pickup.RemainingUses) return false;
                tools.Add(Tool(source, pickup.transform.position, pickup.V02SourceMarkerId ?? AEDV02SceneSource.MarkerId(pickup.transform)));
            }
            if (!tools.Select(x => x.InstanceId).ToHashSet().SetEquals(_aedPhase8Source.CurrentLiveTools.Select(x => x.InstanceId))) return false;
            var minions = FindObjectsByType<CreepMinionRuntime>(FindObjectsInactive.Exclude).Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid).ToArray();
            var doors = new List<AEDV02DoorSource>();
            foreach (var door in FindObjectsByType<NetworkBehaviour>(FindObjectsInactive.Exclude).Where(x => x is INetworkDoorStateProvider && x.Runner == runner))
            {
                if (door.Object == null || !door.Object.IsValid || !door.Object.HasStateAuthority) return false;
                bool broken = door is NetworkSlidingDoor sliding && sliding.IsBroken || door is NetworkDoor normal && normal.IsBroken;
                bool jammed = door is NetworkSlidingDoor slidingDoor && slidingDoor.HasActiveJammer;
                var state = ((INetworkDoorStateProvider)door).State;
                doors.Add(new AEDV02DoorSource { DoorId = AEDV02SceneSource.MarkerId(door.transform), State = jammed ? "LOCKED" : broken ? "BROKEN" :
                    state == NetworkDoorState.Open ? "OPEN" : state == NetworkDoorState.Locked ? "LOCKED" : "CLOSED_UNLOCKED" });
            }
            sample = new AEDV02WorldSample { ResolutionMode = "Adaptive", Difficulty = "Normal", Tick = runner.Tick.Raw, TickRate = runner.TickRate,
                PhaseOrdinal = (int)V02EvidencePhaseOrdinal, PhaseName = _currentTelemetryPhase, LogicalPhase = logical, Zone = Math.Min(logical, 3),
                PlanRevision = revision, PlanFingerprint = plan.Fingerprint(), RosterIdentity = CurrentRosterIdentity(), SceneManifestFingerprint = sceneFingerprint,
                Players = players.ToArray(), Tools = tools.ToArray(), Doors = doors.ToArray(),
                Stalkers = stalkers.Select(x => Threat(x.Object, x.ReplicatedState == StalkerState.DETECT ? "DETECTION" : x.ReplicatedState.ToString(), x.TargetPlayer, Math.Min(logical, 3))).ToArray(),
                Minions = minions.Select(x => Threat(x.Object, x.IsDying ? "DYING" : x.State == CreepMinionState.Roam ? "IDLE" : x.State.ToString().ToUpperInvariant(), x.TargetPlayer, (int)x.Zone)).ToArray() };
            return sample.IsValid();
        }
        private static AEDV02ToolSource Tool(AEDPhase8ToolSourceV1 source, Vector3 position, string marker) => new AEDV02ToolSource
        {
            InstanceId = source.InstanceId, ToolId = source.ToolId, Zone = source.Zone, Origin = source.Origin, Location = source.Location,
            OwnerUserId = Guid.TryParse(source.OwnerUserId, out var owner) ? owner : (Guid?)null, RemainingCharges = source.RemainingCharges,
            MarkerId = marker, X = position.x, Y = position.y, Z = position.z
        };
        private AEDV02ThreatSource Threat(NetworkObject obj, string state, PlayerRef target, int zone)
        {
            Guid? user = state is "CHASE" or "ATTACK" or "TRACK" or "HARASS" && target.IsRealPlayer &&
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
