using System;
using System.Linq;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.Gameplay;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public sealed partial class NetworkMatchState
    {
        [Networked] public int AEDV02PlanRevision { get; private set; }
        [Networked] public NetworkString<_64> AEDV02PlanFingerprint { get; private set; }
        [Networked] public NetworkString<_64> AEDV02RosterIdentity { get; private set; }
        [Networked] public NetworkString<_64> AEDV02SceneFingerprint { get; private set; }
        [Networked] public NetworkString<_64> AEDV02MatchId { get; private set; }
        [Networked] public NetworkString<_64> AEDV02DecisionId { get; private set; }
        [Networked] public NetworkString<_64> AEDV02ActionFingerprint { get; private set; }
        [Networked] public NetworkString<_64> AEDV02WindowId { get; private set; }
        [Networked] public NetworkString<_32> AEDV02DecisionPoint { get; private set; }
        [Networked] public int AEDV02DecisionPhaseOrdinal { get; private set; }
        [Networked, Capacity(12)] private NetworkArray<double> AEDV02Values => default;
        private bool _v02BoundaryPending;
        private string _v02BoundaryTarget;
        private Guid _v02LoggedMatch;
        private int _v02LoggedRevision;
        private long _v02NextSupplyAttempt;
        private int _v02SupplyZone;
        private void TickV02Supply()
        {
            var authority = MatchAuthorityRuntime.Instance;
            if (authority == null || Status != NetworkMatchStatus.Running ||
                ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings?.PacingV02GameplayEnabled != true ||
                !TryReadV02Plan(authority.MatchId, out var plan, out _)) return;
            int zone = AEDV02PhaseMap.LogicalPhase(authority.V02EvidencePhaseName);
            if (zone < 1 || zone > 3) return;
            if (zone != _v02SupplyZone) { _v02SupplyZone = zone; _v02NextSupplyAttempt = 0; }
            if (Runner.Tick.Raw < _v02NextSupplyAttempt) return;
            _v02NextSupplyAttempt = Runner.Tick.Raw + Runner.TickRate * 3;
            if (EchoProtocol.TeamTools.TeamToolWorldSpawn.TrySpawnV02Zone(Runner, _teamToolPickupCatalog,
                authority.MatchId, plan, zone, _teamToolSpawnMinimumSpacing)) TeamToolWorldSpawnInitialized = true;
        }
        private void LogV02ReplicatedPlan()
        {
            var authority = MatchAuthorityRuntime.Instance;
            if (authority == null || AEDV02PlanRevision <= 0 || _v02LoggedMatch == authority.MatchId && _v02LoggedRevision == AEDV02PlanRevision ||
                !TryReadV02Plan(authority.MatchId, out var plan, out var revision)) return;
            _v02LoggedMatch = authority.MatchId; _v02LoggedRevision = revision;
            Debug.Log($"[AED_V02][FUSION_CONFIG] role={(Object.HasStateAuthority ? "Host" : "Client")} match={authority.MatchId:D} version={AEDV02Plan.CatalogVersion} revision={revision} fingerprint={plan.Fingerprint()} action={AEDV02ActionFingerprint} roster={AEDV02RosterIdentity} scene={AEDV02SceneFingerprint}");
        }

        public bool TryGetV02ObjectiveSource(PlayerRef player, out string unit)
        {
            unit = string.Empty;
            Component objective = null;
            var zone2 = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance;
            var zone3 = Zone3MissionDirector.Instance;
            if (RelayA1Operator == player) objective = zone2?.RelayA1;
            else if (RelayA2Operator == player) objective = zone2?.RelayA2;
            else if (RelayB1Operator == player) objective = zone2?.RelayB1;
            else if (RelayB2Operator == player) objective = zone2?.RelayB2;
            else if (SecurityHoldOperator == player || SecurityHoldOperator2 == player || SecurityHoldOperator3 == player || SecurityHoldOperator4 == player)
                objective = zone2?.SecurityTerminal;
            else if (Zone3ChargeOperator == player) objective = zone3?.ChargeStation;
            else if (Zone3Pusher == player || Zone3RefuelOperator == player) objective = zone3?.Convoy;
            else return true;
            if (objective == null) return false;
            unit = AEDV02SceneSource.MarkerId(objective.transform); return true;
        }

        private void ResetV02ReplicatedState()
        {
            AEDV02PlanRevision = 0; AEDV02PlanFingerprint = default; AEDV02RosterIdentity = default;
            _v02BoundaryPending = false; _v02BoundaryTarget = null;
            _v02LoggedMatch = Guid.Empty; _v02LoggedRevision = 0;
            _v02NextSupplyAttempt = 0; _v02SupplyZone = 0;
            AEDV02SceneFingerprint = default; AEDV02MatchId = default; AEDV02DecisionId = default;
            AEDV02ActionFingerprint = default;
            AEDV02WindowId = default; AEDV02DecisionPoint = default; AEDV02DecisionPhaseOrdinal = 0;
            for (int i = 0; i < 12; i++) AEDV02Values.Set(i, 0);
        }

        public bool TryReadV02Plan(Guid matchId, out AEDV02Plan plan, out int revision)
        {
            plan = null; revision = 0;
            if (Object == null || !Object.IsValid || AEDV02PlanRevision <= 0 || AEDV02MatchId.ToString() != matchId.ToString("D")) return false;
            try { plan = new AEDV02Plan(AEDV02Values.ToArray()); } catch (ArgumentException) { return false; }
            if (plan.Fingerprint() != AEDV02PlanFingerprint.ToString()) { plan = null; return false; }
            revision = AEDV02PlanRevision; return true;
        }

        public bool IsV02SafeWindow() => IsV02Window(false);
        public bool IsV02SupportWindow() => IsV02Window(true);
        private bool IsV02Window(bool emergency)
        {
            if (Runner == null || Object == null || !Object.IsValid || !Object.HasStateAuthority || Status != NetworkMatchStatus.Running ||
                FindObjectsByType<EchoProtocol.AI.Minions.CreepMinionRuntime>().Any(x => x.Runner == Runner &&
                    x.State is EchoProtocol.AI.Minions.CreepMinionState.Track or EchoProtocol.AI.Minions.CreepMinionState.Harass) ||
                IsSecurityHoldRunning || IsZone3Charging) return false;
            var authority = MatchAuthorityRuntime.Instance;
            if (authority == null || Runner.ActivePlayers.Count() != authority.BoundPlayerCount || RelayA1Operator.IsRealPlayer || RelayA2Operator.IsRealPlayer ||
                RelayB1Operator.IsRealPlayer || RelayB2Operator.IsRealPlayer || Zone3Pusher.IsRealPlayer || Zone3RefuelOperator.IsRealPlayer) return false;
            var stalkers = FindObjectsByType<StalkerFusionRuntime>().Where(x => x.Runner == Runner).ToArray();
            if (stalkers.Length == 0 || stalkers.Any(x => !x.IsV02SafeForApply)) return false;
            foreach (var player in Runner.ActivePlayers)
                if (!Runner.TryGetPlayerObject(player, out var obj) || !obj.TryGetComponent<NetworkPlayerLifeState>(out var life) ||
                    !emergency && (life.IsDowned || life.IsEliminated) || life.IsCaught || life.IsReviveInProgress) return false;
            return true;
        }
        public bool PreviewV02ResourceGrade(int grade, out AEDV02ProposedReservationDto[] proof)
        {
            proof = null;
            if (!EchoProtocol.TeamTools.TeamToolWorldSpawn.PreviewV02ResourceGrade(Runner, _teamToolPickupCatalog,
                MatchAuthorityRuntime.Instance.MatchId, grade, _teamToolSpawnMinimumSpacing, out var placements)) return false;
            proof = placements.Select(x => new AEDV02ProposedReservationDto { zone = x.Zone, toolId = x.ToolId, markerId = x.MarkerId }).ToArray(); return true;
        }
        public bool PreviewV02Support(AEDV02ResourceNeedDto need, int zone, out AEDV02SupportActionDto action) =>
            EchoProtocol.TeamTools.TeamToolWorldSpawn.PreviewV02Support(Runner, _teamToolPickupCatalog, MatchAuthorityRuntime.Instance.MatchId,
                need, zone, _teamToolSpawnMinimumSpacing, out action);
        public bool MaterializeV02Support(Guid decision, AEDV02SupportActionDto action, out NetworkId instance) =>
            EchoProtocol.TeamTools.TeamToolWorldSpawn.TrySpawnV02Support(Runner, _teamToolPickupCatalog, MatchAuthorityRuntime.Instance.MatchId,
                decision, action.ToModel(), _teamToolSpawnMinimumSpacing, out instance);

        public bool TryCommitV02Authoritative(AEDV02DecisionRequestDto request, AEDV02DecisionResultDto approval, out string reason)
        {
            reason = "ABORT_AMBUSH_REVALIDATION";
            var authority = MatchAuthorityRuntime.Instance;
            var settings = ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings;
            var snapshot = BackendAdaptiveInputSnapshotProvider.Current;
            if (authority == null || request == null || approval == null || Runner == null || Object == null || !Object.IsValid ||
                !Object.HasStateAuthority || !authority.HasStateAuthority || settings?.PacingV02GameplayEnabled != true ||
                authority.RequestedScenarioResolutionMode != ScenarioResolutionMode.Adaptive || authority.Difficulty != MatchDifficulty.Normal ||
                snapshot == null || request.snapshotId != snapshot.SnapshotId.ToString("D") || request.snapshotFingerprint != snapshot.SnapshotFingerprint ||
                request.expectedPlanRevision != AEDV02PlanRevision) return false;
            if (authority.Phase8DebugEvidenceInvalidated || authority.Phase8InventoryEvidenceInvalidated)
            { reason = "HOLD_SOURCE_CAPTURE_EXCLUDED"; return false; }
            if (!AEDv2GameplayBridge.IsNormalCompatible()) { reason = "HOLD_FIXED_BASELINE_DRIFT"; return false; }
            if (request.planValues == null) return false;
            if (request.planValues.Length != 12)
            { reason = "HOLD_RESOURCE_PLACEMENT_SOURCE_REQUIRED"; return false; }
            bool preMatch = request.decisionPoint == "PRE_MATCH";
            bool emergency = request.support?.kind == "EMERGENCY";
            bool safe = emergency ? IsV02SupportWindow() : IsV02SafeWindow();
            if (preMatch ? TeamToolWorldSpawnInitialized || AEDV02PlanRevision != 0
                : request.phaseOrdinal != authority.V02EvidencePhaseOrdinal ||
                    (request.decisionPoint == "PHASE_BOUNDARY" ? !_v02BoundaryPending || request.targetPhaseName != _v02BoundaryTarget
                        : request.targetPhaseName != authority.V02EvidencePhaseName) || !safe) return false;
            if (!preMatch)
            {
                var proposed = new AEDV02Plan(request.planValues);
                int initialBudget = EchoProtocol.TeamTools.TeamToolWorldSpawn.ReadV02Supply(Runner, authority.MatchId)?.InitialOptionalBudget ?? ScenarioSupportItemBudget;
                int population = FindObjectsByType<EchoProtocol.AI.Minions.CreepMinionRuntime>()
                    .Count(x => x.Runner == Runner && x.Zone == EchoProtocol.AI.Stalker.Spatial.RegionSemanticZone.Zone03);
                if (!proposed.HasWorldPressureSlot(initialBudget, population))
                { reason = "HOLD_PRESSURE_HANDOFF_WORLD_NOT_REVERTED"; return false; }
                if (TryReadV02Plan(authority.MatchId, out var currentPlan, out _) &&
                    request.planValues[(int)AEDV02Key.OptionalToolBudget] != currentPlan.Get(AEDV02Key.OptionalToolBudget) &&
                    !EchoProtocol.TeamTools.TeamToolWorldSpawn.CanSetV02SupplyBudget(Runner, authority.MatchId, (int)request.planValues[(int)AEDV02Key.OptionalToolBudget]))
                { reason = "HOLD_OPTIONAL_ALREADY_MATERIALIZED"; return false; }
                if (currentPlan != null && proposed.Get(AEDV02Key.ResourceGrade) != currentPlan.Get(AEDV02Key.ResourceGrade))
                {
                    if (request.proposedReservations == null || request.proposedReservations.Any(x => x == null) ||
                        !PreviewV02ResourceGrade((int)proposed.Get(AEDV02Key.ResourceGrade), out var actual) ||
                        AEDV02ProposedReservation.Fingerprint(actual.Select(x => x.ToModel())) !=
                        AEDV02ProposedReservation.Fingerprint(request.proposedReservations.Select(x => x.ToModel())))
                    { reason = "HOLD_RESOURCE_PLACEMENT_SOURCE_REQUIRED"; return false; }
                }
                if (request.support != null && (!request.support.ToModel().IsValid() || !authority.IsV02NeedCurrent(request.support.ToModel()))) return false;
                var source = authority.LastVerifiedV02Source;
                if (source == null || source.fingerprint != request.sourceFingerprint || source.planRevision != AEDV02PlanRevision ||
                    source.planFingerprint != request.previousPlanFingerprint || source.rosterIdentity != authority.AEDv2RosterIdentity ||
                    source.sceneManifestFingerprint != request.sceneManifestFingerprint || source.startTick != request.startTick || source.endTick != request.endTick ||
                    source.endTick > Runner.Tick.Raw || !DateTime.TryParse(source.sourceEndedAtUtc, out var ended) ||
                    DateTime.UtcNow - ended.ToUniversalTime() > TimeSpan.FromSeconds(AEDV02PilotPolicy.EvidenceMaxAgeSeconds)) return false;
            }
            if (!AEDv2Authority.CommitV02(authority.MatchId, request, approval, authority.AEDv2RosterIdentity, true, true, true,
                preMatch || safe, DateTime.UtcNow, out reason)) return false;
            if (!AEDv2Authority.TryGetV02(authority.MatchId, out var plan, out var revision)) return false;
            if (AEDV02PlanRevision == revision) return true;
            var values = plan.ToArray();
            for (int i = 0; i < 12; i++) AEDV02Values.Set(i, values[i]);
            AEDV02MatchId = authority.MatchId.ToString("D"); AEDV02DecisionId = approval.decisionId;
            AEDV02PlanFingerprint = plan.Fingerprint(); AEDV02RosterIdentity = approval.rosterIdentity;
            AEDV02SceneFingerprint = approval.sceneManifestFingerprint; AEDV02PlanRevision = revision;
            AEDV02ActionFingerprint = approval.actionFingerprint ?? string.Empty;
            AEDV02WindowId = approval.windowId; AEDV02DecisionPoint = approval.decisionPoint; AEDV02DecisionPhaseOrdinal = approval.phaseOrdinal;
            if (!preMatch) foreach (var stalker in FindObjectsByType<StalkerFusionRuntime>().Where(x => x.Runner == Runner))
                stalker.ApplyV02AtSafeWindow(authority.MatchId);
            Debug.Log($"[AED_V02][HOST_COMMIT] match={authority.MatchId:D} decision={approval.decisionId} revision={revision} fingerprint={plan.Fingerprint()} point={request.decisionPoint} window={request.windowSequence}");
            return true;
        }
    }
}
