using System;
using System.Linq;
using System.Collections.Generic;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using EchoProtocol.Player;
using EchoProtocol.AI.Minions;
using EchoProtocol.AI.Stalker.Special;
using EchoProtocol.UI.HUD;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker.Networking
{
    public sealed partial class StalkerFusionRuntime
    {
        private Guid _cabinetClueEvent;
        private AEDV02CabinetClueSource _cabinetClue;
        private double _cabinetClueAt;
        [Networked] public NetworkString<_64> CabinetEncounterId { get; private set; }
        [Networked] public int CabinetPhaseValue { get; private set; }
        private HUDCabinetWarning _cabinetWarning;
        private string _cabinetPresentedId;
        private bool _cabinetCueAcknowledged;
        private string _cabinetHold;
        private long _cabinetNextAttemptTick;
        private void ResetCabinetNetwork()
        {
            _cabinetClue = null; _cabinetClueEvent = Guid.Empty; _cabinetClueAt = 0; _cabinetHold = null;
            _cabinetWarning?.Dispose(); _cabinetWarning = null; _cabinetPresentedId = null; _cabinetCueAcknowledged = false;
            _cabinetNextAttemptTick = 0;
            if (Object != null && Object.IsValid && Object.HasStateAuthority) { CabinetEncounterId = default; CabinetPhaseValue = 0; }
        }
        private void TickCabinetCandidate(AiSimulationStep step)
        {
            if (specialEncounterRuntime == null || _cabinetClue == null || _cabinetClueEvent == Guid.Empty || specialEncounterRuntime.IsActive) return;
            if (Runner.Tick.Raw < _cabinetNextAttemptTick) return;
            _cabinetNextAttemptTick = Runner.Tick.Raw + Runner.TickRate;
            var clue = _cabinetClue; var eventId = _cabinetClueEvent;
            if (step.Time.Seconds - _cabinetClueAt > AEDV02PilotPolicy.AmbushClueAgeSeconds) return;
            if (!TryCabinetContext(clue, eventId, out var pressure, out var clients, out var roots, out var movement)) return;
            if (!global::HidingSpot.TryResolveByStableId(ulong.Parse(clue.HideSpotId), out var spot) ||
                !TryCabinetEntry(spot, roots, out var entry, out var inspectPoint))
            { _cabinetNextAttemptTick = Runner.Tick.Raw + (long)(Runner.TickRate * AEDV02PilotPolicy.AmbushBackoffSeconds); CabinetHold("HOLD_CABINET_ROUTE"); return; }
            var match = MatchAuthorityRuntime.Instance.MatchId; var ordinal = MatchAuthorityRuntime.Instance.V02EvidencePhaseOrdinal;
            var planRevision = MatchAuthorityRuntime.Instance.LastVerifiedV02Source.planRevision;
            long nextRouteCheck = 0; AEDV02CabinetPhase checkedPhase = AEDV02CabinetPhase.Idle;
            bool Revalidate()
            {
                if (MatchAuthorityRuntime.Instance?.MatchId != match || MatchAuthorityRuntime.Instance.V02EvidencePhaseOrdinal != ordinal ||
                    MatchAuthorityRuntime.Instance.LastVerifiedV02Source?.planRevision != planRevision ||
                    !TryCabinetContext(clue, eventId, out _, out _, out var currentRoots, out var currentMovement) ||
                    currentRoots.Any(x => Vector3.Distance(x.position, entry) < 7f)) return false;
                var phase = specialEncounterRuntime.Cabinet.Phase;
                if (phase != checkedPhase || Runner.Tick.Raw >= nextRouteCheck)
                {
                    checkedPhase = phase; nextRouteCheck = Runner.Tick.Raw + Runner.TickRate;
                    return CabinetRouteStillValid(spot, entry, inspectPoint, currentMovement);
                }
                return true;
            }
            var adapter = new StalkerUnityHideSpotAdapter { RunnerScope = Runner };
            var investigation = new StalkerHidingInvestigation(new StalkerHideSpotMemory(), adapter);
            investigation.Begin(new StalkerHideSpotCandidate(spot.StableId, spot.HidePoint.position, inspectPoint, true));
            AEDV02CabinetInspection Inspect()
            {
                if (!Revalidate() || movement.BlocksAEDV02HideForceExit()) return AEDV02CabinetInspection.Protected;
                var result = investigation.Tick(transform.position, Runner.DeltaTime,
                    new AiSimulationTime(Runner.Tick.Raw, Runner.Tick.Raw / (double)Runner.TickRate), controller.V02CabinetInspectionConfig);
                return result.Status switch
                {
                    StalkerHidingInvestigationTickStatus.Inspecting or StalkerHidingInvestigationTickStatus.MovingToSpot => AEDV02CabinetInspection.Pending,
                    StalkerHidingInvestigationTickStatus.Occupied => AEDV02CabinetInspection.Pulled,
                    StalkerHidingInvestigationTickStatus.Protected => AEDV02CabinetInspection.Protected,
                    StalkerHidingInvestigationTickStatus.Empty => AEDV02CabinetInspection.Empty,
                    _ => AEDV02CabinetInspection.Failed
                };
            }
            if (specialEncounterRuntime.TryBeginCabinet(match, clue.UserId, spot.StableId,
                MatchAuthorityRuntime.Instance.LastVerifiedV02Source.zone, step.Time.Seconds, _cabinetClueAt, pressure,
                MatchAuthorityRuntime.Instance.LastVerifiedV02Source.fingerprint, AEDv2Authority.V02PositiveTokens(match), clients,
                entry, inspectPoint, roots, Revalidate, Inspect)) _cabinetHold = null;
        }
        private bool TryCabinetContext(AEDV02CabinetClueSource clue, Guid eventId, out AEDV02PressureSnapshot pressure,
            out Guid[] clients, out List<Transform> roots, out NetworkPlayerMovement subject)
        {
            pressure = default; clients = Array.Empty<Guid>(); roots = new List<Transform>(); subject = null;
            var authority = MatchAuthorityRuntime.Instance; var settings = ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings;
            if (authority == null || !authority.HasStateAuthority || Object == null || !Object.IsValid || !Object.HasStateAuthority ||
                Runner == null || !Runner.IsServer || settings?.PacingV02GameplayEnabled != true || !settings.CabinetAmbushEnabled ||
                authority.RequestedScenarioResolutionMode != ScenarioResolutionMode.Adaptive || authority.Difficulty != EchoProtocol.Gameplay.MatchDifficulty.Normal ||
                !AEDv2Authority.TryGetV02(authority.MatchId, out var plan, out var revision) || plan.Get(AEDV02Key.CabinetAmbushEnabled) != 1 ||
                AEDv2Authority.V02PositiveTokens(authority.MatchId) != 0) { CabinetHold("HOLD_CABINET_POLICY"); return false; }
            var source = authority.LastVerifiedV02Source;
            if (source?.teamPressureStatus != "Verified" || source.opportunityContractVersion != AEDV02PilotOpportunities.Version ||
                source.teamPressureTier is < 3 or > 4) { CabinetHold("HOLD_TEAM_PRESSURE_UNSUPPORTED"); return false; }
            if (source == null || !source.pressureComplete || source.logicalPhase is < 1 or > 2 || source.phaseOrdinal != authority.V02EvidencePhaseOrdinal ||
                source.rosterIdentity != authority.AEDv2RosterIdentity || source.planRevision != revision || source.planFingerprint != plan.Fingerprint() ||
                source.sceneManifestFingerprint != clue.SceneManifestFingerprint || source.cabinetClueEventIds == null || !source.cabinetClueEventIds.Contains(eventId.ToString("D")) ||
                Runner.Tick.Raw / (double)Runner.TickRate - clue.Tick / (double)Runner.TickRate > AEDV02PilotPolicy.AmbushClueAgeSeconds ||
                !AEDv2Authority.TryGetV02Pressure(authority.MatchId, clue.UserId, DateTime.UtcNow, out pressure) || pressure.Tier is < 0 or > 1)
            { CabinetHold("HOLD_CABINET_SOURCE_INCOMPLETE"); return false; }
            var users = new List<Guid>();
            foreach (var player in Runner.ActivePlayers)
            {
                if (!authority.TryResolveVerifiedBackendUserId(player, out var identity) || !Guid.TryParse(identity, out var user) ||
                    !Runner.TryGetPlayerObject(player, out var obj) || !obj.TryGetComponent<NetworkPlayerLifeState>(out var life) ||
                    life.IsDowned || life.IsCaught || life.IsReviveInProgress || life.IsEliminated ||
                    !obj.TryGetComponent<NetworkPlayerMovement>(out var movement) || !obj.TryGetComponent<LobbyPlayerState>(out var lobby) || lobby.IsStabilizerBuffed ||
                    !AEDv2Authority.TryGetV02Pressure(authority.MatchId, user, DateTime.UtcNow, out var observed) || observed.Tier >= 3 ||
                    !NetworkMatchState.Instance.TryGetV02ObjectiveSource(player, out var objective) || objective.Length != 0)
                { CabinetHold("HOLD_CABINET_PLAYER_SAFETY"); return false; }
                users.Add(user); roots.Add(obj.transform); if (user == clue.UserId) subject = movement;
            }
            if (users.Count != authority.BoundPlayerCount || subject == null || !subject.IsHidden || subject.CurrentHideSpotId.ToString() != clue.HideSpotId ||
                subject.BlocksAEDV02HideForceExit() || source.recentDownUsers?.Length > 0 ||
                FindObjectsByType<CreepMinionRuntime>().Any(x => x.Runner == Runner && x.State is CreepMinionState.Track or CreepMinionState.Harass) ||
                FindObjectsByType<StalkerFusionRuntime>().Any(x => x.Runner == Runner && x != this))
            { CabinetHold("HOLD_CABINET_ENCOUNTER_SAFETY"); return false; }
            clients = users.ToArray(); return true;
        }
        private void CabinetHold(string reason)
        {
            if (_cabinetHold == reason) return;
            _cabinetHold = reason; Debug.Log($"[AED_V02][CABINET_HOLD] match={MatchAuthorityRuntime.Instance?.MatchId} reason={reason}");
        }
        private bool TryCabinetEntry(global::HidingSpot spot, List<Transform> players, out Vector3 entry, out Vector3 inspect)
        {
            entry = inspect = default;
            if (spot.InspectPoint == null || !NavMesh.SamplePosition(spot.InspectPoint.position, out var floor, 1.5f, NavMesh.AllAreas) ||
                !AEDV02SceneSource.TryLoad(out var manifest, out _)) return false;
            inspect = floor.position; var target = inspect;
            var candidates = manifest.Markers.Where(x => x.Kind == "AMBUSH_ENTRY").Select(x => new Vector3((float)x.X, (float)x.Y, (float)x.Z))
                .Concat(manifest.NavCells.Select(x => new Vector3((float)((x.A[0] + x.B[0] + x.C[0]) / 3),
                    (float)((x.A[1] + x.B[1] + x.C[1]) / 3), (float)((x.A[2] + x.B[2] + x.C[2]) / 3))))
                .Where(x => Vector3.Distance(x, target) <= 12 && players.All(p => Vector3.Distance(p.position, x) >= 7))
                .OrderBy(x => (x - target).sqrMagnitude);
            foreach (var candidate in candidates)
                if (controller.IsPositionInsidePatrolZone(candidate) && CabinetRouteStillValid(spot, candidate, inspect, null)) { entry = candidate; return true; }
            return false;
        }
        private bool CabinetRouteStillValid(global::HidingSpot spot, Vector3 entry, Vector3 inspect, NetworkPlayerMovement subject)
        {
            if (spot == null || spot.ExitPoint == null || !AEDV02SceneSource.TryLoad(out var manifest, out _) ||
                !AEDV02SceneSource.TryGetDoorSources(Runner, NetworkMatchState.Instance, out var doors)) return false;
            var marker = manifest.Markers.SingleOrDefault(x => x.Kind == "CABINET" && x.HideSpotId == spot.StableId.ToString());
            if (marker == null) return false;
            bool Reach(Vector3 a, Vector3 b) => manifest.HasReachableGeometry(new[] { (double)a.x, a.y, a.z }, new[] { (double)b.x, b.y, b.z }, doors);
            var path = new NavMeshPath();
            if (!Reach(entry, inspect) || !NavMesh.CalculatePath(entry, inspect, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            float length = 0; for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            if (length < 8 || length > 12) return false;
            var zoneEntry = manifest.Markers.Single(x => x.Id == "Z" + marker.Zone + "_ENTRY");
            var exit = spot.ExitPoint.position; var destination = new Vector3((float)zoneEntry.X, (float)zoneEntry.Y, (float)zoneEntry.Z);
            if (!Reach(exit, destination) || !NavMesh.CalculatePath(exit, destination, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            float separation = (_navigationAgent != null ? _navigationAgent.radius : 0) + (subject != null && subject.TryGetComponent<CharacterController>(out var body) ? body.radius : 0);
            for (int i = 1; i < path.corners.Length; i++)
            {
                var a = path.corners[i - 1]; var delta = path.corners[i] - a;
                float t = delta.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(entry - a, delta) / delta.sqrMagnitude) : 0;
                if (Vector3.Distance(a + delta * t, entry) <= separation) return false;
            }
            return true;
        }
        private void PublishCabinetState()
        {
            if (specialEncounterRuntime == null || Runner == null || !Runner.IsRunning || Object == null || !Object.IsValid || !Object.HasStateAuthority) return;
            var encounter = specialEncounterRuntime.Cabinet;
            if (CabinetPhaseValue != (int)encounter.Phase)
            {
                var authority = MatchAuthorityRuntime.Instance;
                string hidden = "unknown", protectedExit = "unknown";
                foreach (var player in Runner.ActivePlayers)
                    if (authority != null && authority.TryResolveVerifiedBackendUserId(player, out var identity) &&
                        Guid.TryParse(identity, out var user) && user == encounter.PlayerId && Runner.TryGetPlayerObject(player, out var obj) &&
                        obj.TryGetComponent<NetworkPlayerMovement>(out var movement))
                    { hidden = movement.IsHidden.ToString(); protectedExit = movement.BlocksAEDV02HideForceExit().ToString(); break; }
                AEDV02Plan plan = null; int revision = 0;
                if (authority != null) AEDv2Authority.TryGetV02(authority.MatchId, out plan, out revision);
                Debug.Log($"[AED_V02][CABINET_WORLD_STATE] match={authority?.MatchId} encounter={encounter.EncounterId:D} phase={encounter.Phase} tick={Runner.Tick.Raw} phaseOrdinal={authority?.V02EvidencePhaseOrdinal} revision={revision} fingerprint={plan?.Fingerprint() ?? "unknown"} policy={AEDV02PilotPolicy.Version} source={encounter.SourceFingerprint} clue={_cabinetClueEvent:D} spot={encounter.SpotId} player={encounter.PlayerId:D} hidden={hidden} protectedExit={protectedExit} reason={encounter.Reason}");
            }
            CabinetEncounterId = specialEncounterRuntime.Cabinet.IsActive ? specialEncounterRuntime.Cabinet.EncounterId.ToString("D") : string.Empty;
            CabinetPhaseValue = (int)specialEncounterRuntime.Cabinet.Phase;
        }
        private void RenderCabinetWarning()
        {
            string id = CabinetEncounterId.ToString();
            if (string.IsNullOrEmpty(id) || CabinetPhaseValue is not ((int)AEDV02CabinetPhase.CuePending or (int)AEDV02CabinetPhase.WarningLead))
            { _cabinetWarning?.Dispose(); _cabinetWarning = null; _cabinetPresentedId = null; _cabinetCueAcknowledged = false; return; }
            if (_cabinetPresentedId != id)
            {
                _cabinetWarning?.Dispose(); _cabinetPresentedId = id; _cabinetCueAcknowledged = false;
                _cabinetWarning = new HUDCabinetWarning(transform, GetComponentInChildren<EchoProtocol.AI.Stalker.Presentation.StalkerAudioController>(true)?.CabinetWarningClip);
            }
            if (_cabinetCueAcknowledged) return;
            var status = _cabinetWarning.Present();
            if (status == CabinetCueStatus.Pending) return;
            _cabinetCueAcknowledged = true;
            RpcCabinetCueReady(id, status == CabinetCueStatus.Ready, status == CabinetCueStatus.Ready);
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcCabinetCueReady(string encounter, NetworkBool audioAvailable, NetworkBool visualPresented, RpcInfo info = default)
        {
            var authority = MatchAuthorityRuntime.Instance;
            var actor = info.Source.IsRealPlayer ? info.Source : Runner.LocalPlayer;
            if (!Object.HasStateAuthority || authority == null || !Guid.TryParse(encounter, out var id) || encounter != CabinetEncounterId.ToString() ||
                !authority.TryResolveVerifiedBackendUserId(actor, out var identity) || !Guid.TryParse(identity, out var user)) return;
            specialEncounterRuntime.AcknowledgeCabinetCue(id, user, audioAvailable, visualPresented, Runner.Tick.Raw / (double)Runner.TickRate);
            Debug.Log($"[AED_V02][CABINET_CUE_ACK] encounter={id:D} user={user:D} audio={(bool)audioAvailable} visual={(bool)visualPresented}");
        }
        public void ObserveCabinetEntry(NetworkPlayerMovement movement, global::HidingSpot spot)
        {
            var authority = MatchAuthorityRuntime.Instance;
            var settings = ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings;
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority || authority == null || !authority.HasStateAuthority ||
                settings == null || !settings.PacingV02GameplayEnabled && !settings.PacingV02ShadowEnabled || movement == null || movement.Runner != Runner ||
                movement.Object == null || !movement.Object.HasStateAuthority || movement.IsHidden || spot == null || visionSensor?.VisionOrigin == null ||
                !movement.TryGetComponent<NetworkPlayerLifeState>(out var life) || life.IsCaught || life.IsDowned || life.IsEliminated ||
                !authority.TryResolveVerifiedBackendUserId(movement.Object.InputAuthority, out var userId) || !Guid.TryParse(userId, out var user) ||
                !AEDV02SceneSource.TryLoad(out var manifest, out var fingerprint)) return;
            var marker = manifest.Markers.SingleOrDefault(x => x.Kind == "CABINET" && x.HideSpotId == spot.StableId.ToString());
            if (marker == null || marker.Zone != AEDV02PhaseMap.LogicalPhase(authority.V02EvidencePhaseName)) return;
            var interaction = movement.GetComponent<global::PlayerInteraction>();
            var colliders = spot.GetComponentsInChildren<Collider>();
            var playerPosition = movement.transform.position;
            if (interaction == null || interaction.InteractionDistance <= 0 ||
                !colliders.Any(x => x.enabled && Vector3.Distance(x.ClosestPoint(playerPosition), playerPosition) <= interaction.InteractionDistance)) return;
            var identity = movement.GetComponent<PlayerRuntimeIdentity>();
            if (identity == null || !identity.IsBound || !visionSensor.TryEvaluateV02Candidate(identity.VisionTargetPoint, identity.EntityRoot,
                ResolveV02PerceptionTier(identity.PlayerId), out var observed)) return;
            controller.RecordV02SeenHideEntry(spot.StableId, identity.PlayerId);
            var origin = visionSensor.VisionOrigin.position;
            var target = observed.ObservedPosition;
            var clue = new AEDV02CabinetClueSource { UserId = user, StalkerNetworkId = Object.Id.ToString(), HideSpotId = spot.StableId.ToString(),
                SceneMarkerId = marker.Id, SceneManifestFingerprint = fingerprint, Tick = Runner.Tick.Raw,
                OriginX = origin.x, OriginY = origin.y, OriginZ = origin.z, TargetX = target.x, TargetY = target.y, TargetZ = target.z, ObservedDistance = observed.Distance,
                PlayerX = playerPosition.x, PlayerY = playerPosition.y, PlayerZ = playerPosition.z };
            if (!authority.RecordV02CabinetClue(clue, out var eventId)) return;
            _cabinetClue = clue; _cabinetClueEvent = eventId; _cabinetClueAt = Runner.Tick.Raw / (double)Runner.TickRate;
            _cabinetNextAttemptTick = 0;
        }
    }
}
