using System;
using System.Linq;
using System.Threading.Tasks;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.Auth;
using EchoProtocol.Gameplay;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking
{
    public sealed partial class NetworkMatchState
    {
        [Networked] public int AEDV02PlanRevision { get; private set; }
        [Networked] public double AEDV02ResolvedChaseSpeed { get; private set; }
        [Networked] private NetworkBool AEDV02HasRelativeValues { get; set; }
        [Networked] public int AEDV02SupplySpawnedMask { get; private set; }
        [Networked] public int AEDV02SupplyMaterializedMask { get; private set; }
        [Networked, Capacity(12)] private NetworkArray<double> AEDV02ResolvedValues => default;

        internal bool TryReadV02ResolvedValue(Guid matchId, AEDV02Plan plan, AEDV02Key key, out double value)
        {
            value = 0;
            if (Object == null || !Object.IsValid || !AEDV02HasRelativeValues || AEDV02PlanRevision == 0 ||
                AEDV02MatchId.ToString() != matchId.ToString("D") || AEDV02PlanFingerprint.ToString() != plan.Fingerprint())
                return false;
            value = AEDV02ResolvedValues[(int)key];
            return true;
        }

        internal void RecordV02SupplySpawn(int zone, bool optional)
        {
            int bit = 1 << (zone - 1);
            AEDV02SupplySpawnedMask |= bit;
            if (optional) AEDV02SupplyMaterializedMask |= bit;
        }
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
        private float V02SupplySpacing => RemoteFixedBalanceSnapshot.GameplayValue("toolSpawnSpacing", _teamToolSpawnMinimumSpacing);

        private void TickV02Supply()
        {
            var authority = MatchAuthorityRuntime.Instance;
            if (authority == null || authority.HasAutomaticBalance && !AEDv2Authority.HasRelativeV02 || Status != NetworkMatchStatus.Running ||
                ScenarioConfigAuthorityRuntime.Instance?.RuntimeSettings?.PacingV02GameplayEnabled != true ||
                !TryReadV02Plan(authority.MatchId, out var plan, out _)) return;
            int zone = AEDV02PhaseMap.LogicalPhase(PhaseName(CurrentPhase));
            if (zone < 1 || zone > 3) return;
            if (zone != _v02SupplyZone) { _v02SupplyZone = zone; _v02NextSupplyAttempt = 0; }
            if (Runner.Tick.Raw < _v02NextSupplyAttempt) return;
            _v02NextSupplyAttempt = Runner.Tick.Raw + Runner.TickRate * 3;
            if (EchoProtocol.TeamTools.TeamToolWorldSpawn.TrySpawnV02Zone(Runner, _teamToolPickupCatalog,
                authority.MatchId, plan, zone, V02SupplySpacing)) TeamToolWorldSpawnInitialized = true;
        }
        private void LogV02ReplicatedPlan()
        {
            var authority = MatchAuthorityRuntime.Instance;
            if (authority == null || AEDV02PlanRevision <= 0 || _v02LoggedMatch == authority.MatchId && _v02LoggedRevision == AEDV02PlanRevision ||
                !TryReadV02Plan(authority.MatchId, out var plan, out var revision)) return;
            double observedChase = 0;
            double[] observedStalker = null;
            double[] observedResolved = null;
            if (!Object.HasStateAuthority && AEDV02ResolvedChaseSpeed > 0)
            {
                var stalker = FindObjectsByType<StalkerFusionRuntime>().FirstOrDefault(x => x.Runner == Runner &&
                    x.V02ConsumerRevision == revision && x.V02ConsumerMatchId.ToString() == authority.MatchId.ToString("D") &&
                    x.V02ConsumerFingerprint.ToString() == AEDV02PlanFingerprint.ToString());
                if (stalker == null ||
                    !stalker.TryReadReplicatedV02ConsumerValue(AEDV02Key.ChaseSpeed, out observedChase) ||
                    Math.Abs(observedChase - AEDV02ResolvedChaseSpeed) >= .0001) return;
                if (AEDV02HasRelativeValues)
                {
                    observedStalker = new double[6];
                    observedResolved = new double[12];
                    for (int index = 0; index < observedResolved.Length; index++)
                    {
                        observedResolved[index] = AEDV02ResolvedValues[index];
                        if (index < observedStalker.Length &&
                            (!stalker.TryReadReplicatedV02ConsumerValue((AEDV02Key)index, out observedStalker[index]) ||
                             Math.Abs(observedStalker[index] - observedResolved[index]) >= .0001)) return;
                    }
                }
            }
            _v02LoggedMatch = authority.MatchId; _v02LoggedRevision = revision;
            Debug.Log($"[AED_V02][FUSION_CONFIG] role={(Object.HasStateAuthority ? "Host" : "Client")} match={authority.MatchId:D} version={AEDV02Plan.CatalogVersion} revision={revision} fingerprint={plan.Fingerprint()} action={AEDV02ActionFingerprint} roster={AEDV02RosterIdentity} scene={AEDV02SceneFingerprint}");
            if (!Object.HasStateAuthority && Guid.TryParse(AEDV02DecisionId.ToString(), out var decision))
                _ = ReportV02ClientObservationAsync(authority.MatchId, decision, revision, plan.Fingerprint(), observedChase, observedStalker, observedResolved);
        }

        private static async Task ReportV02ClientObservationAsync(Guid match, Guid decision, int revision, string fingerprint, double observedChase,
            double[] observedStalker, double[] observedResolved)
        {
            try
            {
                var api = new AEDV02ApiService(AuthRuntime.EnsureExists().Client);
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var result = await api.ClientObserved(match, decision, revision, fingerprint, observedChase, observedStalker, observedResolved);
                    if (result?.IsSuccess == true) return;
                    if (attempt < 2) await Task.Delay(1000 * (attempt + 1));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AED_V02][CLIENT_OBSERVATION] {ex.GetType().Name}");
            }
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
            AEDV02ResolvedChaseSpeed = 0;
            AEDV02HasRelativeValues = false;
            AEDV02SupplySpawnedMask = AEDV02SupplyMaterializedMask = 0;
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

        internal bool RestoreConfirmedAutomaticV02(MatchAuthorityRuntime authority, AEDV02DecisionResultDto approved)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority || !authority.HasAutomaticBalance) return false;
            if (approved == null)
            {
                AEDv2Authority.Reset(authority.MatchId);
                ResetV02ReplicatedState();
                return true;
            }
            if (approved.applyStatus != "APPLIED" || approved.approvalStatus != "APPROVED" ||
                approved.matchId != authority.MatchId.ToString("D") || approved.planRevision <= 0 ||
                !Guid.TryParse(approved.decisionId, out var decisionId) || approved.stalkerValues?.Length != 6)
                return false;
            AEDV02Plan plan;
            try { plan = new AEDV02Plan(approved.planValues); }
            catch (ArgumentException) { return false; }
            bool relative = approved.resolvedValues != null;
            if (plan.Fingerprint() != approved.planFingerprint ||
                (relative ? !MatchesAutomaticV02Approval(authority, plan, approved, approved.initialOptionalBudget) :
                    Enumerable.Range(0, 6).Any(index => !AEDV02PilotPolicy.Finite(approved.stalkerValues[index]) ||
                        Math.Abs(approved.stalkerValues[index] - authority.V02StalkerValue(plan, (AEDV02Key)index)) >= .0001)))
                return false;

            AEDv2Authority.RestoreConfirmedAutomaticV02(authority.MatchId, decisionId, plan, approved);
            var values = plan.ToArray();
            for (int i = 0; i < values.Length; i++) AEDV02Values.Set(i, values[i]);
            AEDV02MatchId = approved.matchId; AEDV02DecisionId = approved.decisionId;
            AEDV02PlanFingerprint = approved.planFingerprint; AEDV02RosterIdentity = approved.rosterIdentity;
            AEDV02SceneFingerprint = approved.sceneManifestFingerprint; AEDV02PlanRevision = approved.planRevision;
            AEDV02ResolvedChaseSpeed = approved.stalkerValues[(int)AEDV02Key.ChaseSpeed];
            AEDV02HasRelativeValues = relative;
            if (relative)
                for (int i = 0; i < 12; i++) AEDV02ResolvedValues.Set(i, approved.resolvedValues[i]);
            AEDV02ActionFingerprint = approved.actionFingerprint ?? string.Empty;
            AEDV02WindowId = approved.windowId; AEDV02DecisionPoint = approved.decisionPoint; AEDV02DecisionPhaseOrdinal = approved.phaseOrdinal;
            foreach (var stalker in FindObjectsByType<StalkerFusionRuntime>().Where(x => x.Runner == Runner))
                stalker.RefreshAutomaticV02AfterTakeover();
            return true;
        }

        private static bool MatchesAutomaticV02Approval(MatchAuthorityRuntime authority, AEDV02Plan plan,
            AEDV02DecisionResultDto approval, int initial)
        {
            if (approval.stalkerValues?.Length != 6 || approval.resolvedValues?.Length != 12 ||
                approval.initialOptionalBudget != initial ||
                !authority.TryResolveV02ToolMask(plan.Get(AEDV02Key.OptionalToolBudget), initial, out var mask) ||
                mask != approval.optionalToolMask) return false;
            return Enumerable.Range(0, 12).All(index =>
            {
                var key = (AEDV02Key)index;
                if (!authority.TryResolveV02Value(key, plan.Get(key), out var value)) return false;
                if (key == AEDV02Key.OptionalToolBudget) value = AEDV02Plan.OptionalCount(mask);
                return AEDV02PilotPolicy.Finite(approval.resolvedValues[index]) &&
                    Math.Abs(approval.resolvedValues[index] - value) < .0001 &&
                    (index >= 6 || Math.Abs(approval.stalkerValues[index] - value) < .0001);
            });
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
                MatchAuthorityRuntime.Instance.MatchId, grade, V02SupplySpacing, out var placements)) return false;
            proof = placements.Select(x => new AEDV02ProposedReservationDto { zone = x.Zone, toolId = x.ToolId, markerId = x.MarkerId }).ToArray(); return true;
        }
        public bool PreviewV02Support(AEDV02ResourceNeedDto need, int zone, out AEDV02SupportActionDto action) =>
            EchoProtocol.TeamTools.TeamToolWorldSpawn.PreviewV02Support(Runner, _teamToolPickupCatalog, MatchAuthorityRuntime.Instance.MatchId,
                need, zone, V02SupplySpacing, out action);
        public bool MaterializeV02Support(Guid decision, AEDV02SupportActionDto action, out NetworkId instance) =>
            EchoProtocol.TeamTools.TeamToolWorldSpawn.TrySpawnV02Support(Runner, _teamToolPickupCatalog, MatchAuthorityRuntime.Instance.MatchId,
                decision, action.ToModel(), V02SupplySpacing, out instance);

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
            if (authority.HasAutomaticBalance)
            {
                AEDV02Plan proposed;
                try { proposed = new AEDV02Plan(request.planValues); }
                catch (ArgumentException) { return false; }
                if (!MatchesAutomaticV02Approval(authority, proposed, approval,
                    request.decisionPoint == "PRE_MATCH" ? (int)proposed.Get(AEDV02Key.OptionalToolBudget) : AEDv2Authority.V02InitialOptionalBudget))
                { reason = "HOLD_RESOLVED_BALANCE_MISMATCH"; return false; }
            }
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
                int initialBudget = authority.HasAutomaticBalance ? AEDv2Authority.V02InitialOptionalBudget :
                    EchoProtocol.TeamTools.TeamToolWorldSpawn.ReadV02Supply(Runner, authority.MatchId)?.InitialOptionalBudget ?? ScenarioSupportItemBudget;
                int population = FindObjectsByType<EchoProtocol.AI.Minions.CreepMinionRuntime>()
                    .Count(x => x.Runner == Runner && x.Zone == EchoProtocol.AI.Stalker.Spatial.RegionSemanticZone.Zone03);
                if (authority.HasAutomaticBalance)
                    population = Math.Max(0, population - (int)authority.V02Baseline(AEDV02Key.Zone3AdaptiveMinionCap));
                if (!proposed.HasWorldPressureSlot(initialBudget, population, authority.HasAutomaticBalance))
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
                    DateTime.UtcNow - ended.ToUniversalTime() > TimeSpan.FromSeconds(authority.V02EvidenceMaxAge)) return false;
            }
            if (!AEDv2Authority.CommitV02(authority.MatchId, request, approval, authority.AEDv2RosterIdentity, true, true, true,
                preMatch || safe, DateTime.UtcNow, out reason, authority.V02Configuration)) return false;
            if (!AEDv2Authority.TryGetV02(authority.MatchId, out var plan, out var revision)) return false;
            if (AEDV02PlanRevision == revision) return true;
            var values = plan.ToArray();
            for (int i = 0; i < 12; i++) AEDV02Values.Set(i, values[i]);
            AEDV02MatchId = authority.MatchId.ToString("D"); AEDV02DecisionId = approval.decisionId;
            AEDV02PlanFingerprint = plan.Fingerprint(); AEDV02RosterIdentity = approval.rosterIdentity;
            AEDV02SceneFingerprint = approval.sceneManifestFingerprint; AEDV02PlanRevision = revision;
            AEDV02ResolvedChaseSpeed = authority.HasAutomaticBalance ? approval.stalkerValues[(int)AEDV02Key.ChaseSpeed] : 0;
            AEDV02HasRelativeValues = authority.HasAutomaticBalance;
            if (authority.HasAutomaticBalance)
                for (int i = 0; i < 12; i++) AEDV02ResolvedValues.Set(i, approval.resolvedValues[i]);
            AEDV02ActionFingerprint = approval.actionFingerprint ?? string.Empty;
            AEDV02WindowId = approval.windowId; AEDV02DecisionPoint = approval.decisionPoint; AEDV02DecisionPhaseOrdinal = approval.phaseOrdinal;
            if (!preMatch) foreach (var stalker in FindObjectsByType<StalkerFusionRuntime>().Where(x => x.Runner == Runner))
                stalker.ApplyV02AtSafeWindow(authority.MatchId);
            Debug.Log($"[AED_V02][HOST_COMMIT] match={authority.MatchId:D} decision={approval.decisionId} revision={revision} fingerprint={plan.Fingerprint()} point={request.decisionPoint} window={request.windowSequence}");
            return true;
        }
    }
}
