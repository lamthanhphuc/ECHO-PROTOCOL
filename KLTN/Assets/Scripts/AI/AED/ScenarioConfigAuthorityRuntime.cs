using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common.Profile;
using EchoProtocol.Diagnostics;
using UnityEngine;

namespace EchoProtocol.AI.AED
{
    public sealed class ScenarioConfigAuthorityRuntime : MonoBehaviour
    {
        [Serializable]
        private sealed class V2AuditEntry
        {
            public string matchId;
            public string decisionId;
            public string decisionPoint;
            public string status;
            public string reason;
            public string changedKey;
            public string previousValue;
            public string appliedValue;
            public string planFingerprint;
            public string evidenceFingerprint;
            public uint revision;
            public string occurredAtUtc;
        }
        private static ScenarioConfigAuthorityRuntime _instance;

        [SerializeField] private AEDRuntimeSettings runtimeSettings;

        private readonly ScenarioResolutionEngine _engine = new ScenarioResolutionEngine();
        private AdaptiveDecisionLocalAuditStore _auditStore;
        private Guid _backendPreMatchId;
        private AEDResolvePreMatchData _backendPreMatchDecision;
        private ScenarioConfig _backendPreMatchConfig;

        public static ScenarioConfigAuthorityRuntime Instance => _instance;
        public AEDRuntimeSettings RuntimeSettings => runtimeSettings;
        public ScenarioConfig CurrentAppliedScenarioConfig { get; private set; }
        public AdaptiveDecision LastAdaptiveDecision { get; private set; }
        public AEDDebugSnapshot LastDebugSnapshot { get; private set; }
        public ScenarioResolutionRecord LastResolutionRecord
        {
            get;
            private set;
        }

        public ScenarioResolutionMode CurrentScenarioResolutionMode { get; private set; } = ScenarioResolutionMode.Fixed;
        public ScenarioDecisionPoint LastDecisionPoint { get; private set; } = ScenarioDecisionPoint.PreMatch;

        public static ScenarioConfigAuthorityRuntime EnsureExists()
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ScenarioConfigAuthorityRuntime>();
            }

            if (_instance == null)
            {
                _instance = new GameObject("ScenarioConfigAuthorityRuntime")
                    .AddComponent<ScenarioConfigAuthorityRuntime>();
            }

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
            if (runtimeSettings == null)
                runtimeSettings = Resources.Load<AEDRuntimeSettings>("AED/AEDRuntimeSettings");
            _auditStore ??= new AdaptiveDecisionLocalAuditStore(
                System.IO.Path.Combine(Application.persistentDataPath, "aed-scenario-resolution.jsonl"));
        }

        public bool TryStageBackendPreMatch(
            Guid matchId,
            AEDResolvePreMatchData decision)
        {
            ClearStagedBackendPreMatch(Guid.Empty);

            bool managedFixed = decision?.IsManaged == true && decision.usedFixedFallback;
            var expectedId = CreateDecisionId(matchId, ScenarioDecisionPoint.PreMatch, "PRE_MATCH", 0);
            if (matchId == Guid.Empty ||
                decision?.config == null ||
                decision.matchId != matchId.ToString("D") ||
                !Guid.TryParse(decision.decisionId, out var decisionId) ||
                (decisionId != expectedId && !(managedFixed && decisionId == AEDV02Plan.FixedFallbackDecisionId(expectedId))) ||
                (managedFixed ? decision.resolutionResult != "FIXED_FALLBACK" || decision.config.configSource != "FIXED"
                    : decision.resolutionResult != "ADAPTIVE_CONFIG_ISSUED" || decision.usedFixedFallback ||
                      decision.candidateValidationStatus != "VALID" || decision.config.configSource != "ADAPTIVE") ||
                decision.decisionStatus != "COMMITTED" ||
                decision.config.unityCompatibilityVersion != (Application.version == "0.1.1" ? "0.1.0" : Application.version))
            {
                return false;
            }

            var c = decision.config;
            var baseline = FixedDirector.CreateFixedBaseline();

            if (string.IsNullOrWhiteSpace(c.scenarioConfigId) ||
                string.IsNullOrWhiteSpace(c.scenarioConfigVersion) ||
                c.scenarioConfigVersion.Length > 64 ||
                string.IsNullOrWhiteSpace(c.contentFingerprint) ||
                c.contentFingerprint.Length != 64 ||
                !c.contentFingerprint.All(Uri.IsHexDigit) ||
                !string.Equals(c.contentFingerprint, ComputeBackendContentFingerprint(c),
                    StringComparison.OrdinalIgnoreCase) ||
                c.policyVersion != baseline.PolicyVersion ||
                c.mapId != baseline.MapId ||
                c.monsterType != baseline.MonsterType ||
                c.objectiveSpawnSetId != baseline.ObjectiveSpawnSetId ||
                c.routeModifier != baseline.RouteModifier ||
                (!decision.IsManaged && (c.fallbackConfigId != baseline.FallbackConfigId ||
                    c.fallbackConfigVersion != FixedDirector.FixedBaselineScenarioVersion)) ||
                (managedFixed && (c.fallbackConfigId != c.scenarioConfigId || c.fallbackConfigVersion != c.scenarioConfigVersion)) ||
                c.contentWhitelistVersion != FixedDirector.FixedBaselineContentWhitelistVersion ||
                c.detectionFillRate != baseline.MonsterParameters.DetectionFillRate ||
                c.detectionDecayRate != baseline.MonsterParameters.DetectionDecayRate ||
                c.searchDuration != baseline.MonsterParameters.SearchDuration ||
                c.escapeDoorTimerSeconds != baseline.FinalHuntParameters.EscapeDoorTimerSeconds ||
                c.supportItemBudget < 0 ||
                !double.IsFinite(c.chaseSpeed))
            {
                return false;
            }

            if (runtimeSettings == null ||
                !runtimeSettings.TryBuildParameterRegistry(out var registry, out _) ||
                !registry.TryGetRule(
                    ScenarioConfigKey.SupportItemBudget, out var supportRule) ||
                !registry.TryGetRule(
                    ScenarioConfigKey.ChaseSpeed, out var chaseRule))
            {
                return false;
            }

            bool supportChanged = c.supportItemBudget != baseline.SupportItemBudget;
            bool chaseChanged = c.chaseSpeed != baseline.MonsterParameters.ChaseSpeed;

            if (!decision.IsManaged && (supportChanged == chaseChanged ||
                !supportRule.CandidateValues.Contains((double)c.supportItemBudget) ||
                !chaseRule.CandidateValues.Contains(c.chaseSpeed) ||
                (supportChanged && c.supportItemBudget != supportRule.CandidateValues
                    .Where(value => value > baseline.SupportItemBudget)
                    .DefaultIfEmpty(double.NaN).Min()) ||
                (chaseChanged && c.chaseSpeed != chaseRule.CandidateValues
                    .Where(value => value > baseline.MonsterParameters.ChaseSpeed)
                    .DefaultIfEmpty(double.NaN).Min())))
            {
                return false;
            }
            if (decision.IsManaged && (c.supportItemBudget > 2 || !managedFixed &&
                (decision.preMatchRule == "AED-PRE-030-BOTH-HIGH-INCREASE"
                    ? c.chaseSpeed is not (8d or 10d)
                    : c.supportItemBudget < 1 || c.chaseSpeed is not (7d or 7.5d or 8d or 9d))))
                return false;
            if (managedFixed && c.chaseSpeed is not (7d or 7.5d or 8d or 9d)) return false;

            try
            {
                var config = new ScenarioConfig(
                    c.scenarioConfigVersion,
                    c.policyVersion,
                    managedFixed ? ScenarioConfigSource.Fixed : ScenarioConfigSource.Adaptive,
                    c.mapId,
                    c.monsterType,
                    c.objectiveSpawnSetId,
                    c.supportItemBudget,
                    new ScenarioMonsterParameters(
                        c.detectionFillRate,
                        c.detectionDecayRate,
                        c.chaseSpeed,
                        c.searchDuration),
                    c.routeModifier,
                    new ScenarioFinalHuntParameters(c.escapeDoorTimerSeconds),
                    c.fallbackConfigId);

                if (!AEDGameplayContentContract.TryValidate(config, out _))
                    return false;

                _backendPreMatchId = matchId;
                _backendPreMatchDecision = decision;
                _backendPreMatchConfig = config;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static string ComputeBackendContentFingerprint(AEDBackendScenarioConfigDto c)
        {
            string[] parts =
            {
                c.scenarioConfigId, c.scenarioConfigVersion, c.schemaVersion,
                c.policyVersion, c.configSource, c.mapId, c.monsterType,
                c.objectiveSpawnSetId,
                c.supportItemBudget.ToString(CultureInfo.InvariantCulture),
                c.detectionFillRate.ToString("R", CultureInfo.InvariantCulture),
                c.detectionDecayRate.ToString("R", CultureInfo.InvariantCulture),
                c.chaseSpeed.ToString("R", CultureInfo.InvariantCulture),
                c.searchDuration.ToString("R", CultureInfo.InvariantCulture),
                c.routeModifier,
                c.escapeDoorTimerSeconds.ToString("R", CultureInfo.InvariantCulture),
                c.fallbackConfigId, c.fallbackConfigVersion,
                c.contentWhitelistVersion, c.unityCompatibilityVersion
            };
            var payload = string.Join("|", parts.Select(x => x ?? "<null>"));
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        public bool TryGetStagedBackendPreMatch(
            Guid matchId,
            out AEDResolvePreMatchData decision,
            out ScenarioConfig config)
        {
            decision = null;
            config = null;

            if (matchId == Guid.Empty ||
                matchId != _backendPreMatchId ||
                _backendPreMatchDecision == null ||
                _backendPreMatchConfig == null)
            {
                return false;
            }

            decision = _backendPreMatchDecision;
            config = _backendPreMatchConfig;
            return true;
        }

        public void ClearStagedBackendPreMatch(Guid matchId)
        {
            if (matchId != Guid.Empty && matchId != _backendPreMatchId)
                return;

            _backendPreMatchId = Guid.Empty;
            _backendPreMatchDecision = null;
            _backendPreMatchConfig = null;
        }

        public async Task<bool> ConfirmStagedBackendPreMatchAsync(
            Guid matchId,
            ScenarioConfig appliedConfig)
        {
            if (!TryGetStagedBackendPreMatch(
                    matchId, out var decision, out var stagedConfig) ||
                appliedConfig == null ||
                appliedConfig.ConfigSource != stagedConfig.ConfigSource ||
                appliedConfig.ScenarioConfigVersion != stagedConfig.ScenarioConfigVersion ||
                ScenarioConfigFingerprint.Compute(appliedConfig) !=
                    ScenarioConfigFingerprint.Compute(stagedConfig) ||
                decision.config == null ||
                !string.Equals(decision.config.contentFingerprint,
                    ComputeBackendContentFingerprint(decision.config),
                    StringComparison.OrdinalIgnoreCase) ||
                !Guid.TryParse(decision.decisionId, out var decisionId))
            {
                return false;
            }

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (await new AEDSnapshotApiService()
                            .ConfirmScenarioAppliedAsync(
                                matchId,
                                decisionId,
                                decision.config,
                                CancellationToken.None))
                    {
                        Debug.Log(
                            $"[AED_P8][PREMATCH_APPLIED] " +
                            $"match={matchId:D} " +
                            $"config={decision.config.scenarioConfigId} " +
                            $"version={decision.config.scenarioConfigVersion} " +
                            $"budget={stagedConfig.SupportItemBudget}");

                        ClearStagedBackendPreMatch(matchId);
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[AED_P8][PREMATCH_RECEIPT] " +
                        exception.GetType().Name);
                }

                if (attempt < 2)
                    await Task.Delay(1000 * (attempt + 1));
            }

            Debug.LogError(
                $"[AED_P8][PREMATCH_RECEIPT_FAILED] match={matchId:D}");
            return false;
        }

        public ScenarioResolutionEngineResult Resolve(
            Guid matchId,
            ScenarioResolutionMode mode,
            ScenarioDecisionPoint decisionPoint,
            string phaseContext,
            uint phaseOrdinal,
            string experimentCondition)
        {
            if (matchId == Guid.Empty)
            {
                matchId = ScenarioConfigFingerprint.DeterministicGuid("dev-match:" + phaseContext);
            }

            CurrentScenarioResolutionMode = mode;
            LastDecisionPoint = decisionPoint;
            var request = new ScenarioResolutionRequest(
                CreateDecisionId(matchId, decisionPoint, phaseContext, phaseOrdinal),
                matchId,
                mode,
                decisionPoint,
                phaseContext ?? string.Empty,
                experimentCondition ?? string.Empty,
                EchoProtocol.Networking.LobbyMapCatalog.TelemetryMapId(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
            var extendedRequested = runtimeSettings != null
                && (runtimeSettings.ExtendedPolicyShadowEnabled
                    || runtimeSettings.ExtendedPolicyGameplayEnabled);

            AEDPolicyConfig policyConfig = null;
            AEDEvidencePolicy evidencePolicy = null;
            AdaptiveParameterRegistry registry = null;
            AdaptiveInputSnapshot snapshot = null;
            AdaptiveInputCurrencyValidation currency = null;
            var adaptiveInputUnavailableReason =
                string.Empty;

            if (mode == ScenarioResolutionMode.Adaptive)
            {
                if (runtimeSettings == null)
                {
                    adaptiveInputUnavailableReason = "AED_RUNTIME_SETTINGS_MISSING";
                }
                else
                {
                    runtimeSettings.TryBuildPolicyConfig(out policyConfig, out _);
                    runtimeSettings.TryBuildEvidencePolicy(out evidencePolicy, out _);
                    runtimeSettings.TryBuildParameterRegistry(out registry, out _);
                    AdaptiveInputSnapshotRuntime.TryGetSnapshot(
                        request, out snapshot, out currency, out adaptiveInputUnavailableReason);
                }
            }

            if (mode == ScenarioResolutionMode.Adaptive && extendedRequested)
            {
                AEDv2Authority.Stage(
                    request, snapshot, policyConfig, evidencePolicy, currency,
                    runtimeSettings.ExtendedPolicyShadowEnabled,
                    runtimeSettings.ExtendedPolicyGameplayEnabled);
            }

            var result = _engine.Resolve(
                new ScenarioResolutionEngineInput(
                    extendedRequested && mode == ScenarioResolutionMode.Adaptive
                        ? new ScenarioResolutionRequest(request.ResolutionId, matchId,
                            ScenarioResolutionMode.Fixed, decisionPoint,
                            phaseContext ?? string.Empty, experimentCondition ?? string.Empty,
                            request.MapId)
                        : request,
                    CurrentAppliedScenarioConfig,
                    snapshot,
                    currency,
                    policyConfig,
                    evidencePolicy,
                    registry,
                    adaptiveInputUnavailableReason));

            return result;
        }

        public void FinalizeResolution(
            Guid matchId,
            ScenarioResolutionEngineResult result,
            bool hasStateAuthority)
        {
            if (result == null)
            {
                throw new ArgumentNullException(
                    nameof(result));
            }

            if (matchId != result.Context.Request.TargetMatchId)
            {
                throw new InvalidOperationException(
                    "Finalized match id does not match " +
                    "the resolution target match.");
            }

            var extendedApplied = AEDv2Authority.Commit(
                matchId,
                result.Context.Request.ResolutionId,
                hasStateAuthority,
                result.IsPrecommitRejected ||
                result.CommitDisposition != ScenarioResolutionCommitDisposition.NewDecision);
            if (extendedApplied)
                _ = ConfirmV2ApplyAsync(matchId, AEDv2Authority.LastAppliedDecisionId,
                    AEDv2Authority.LastAppliedPlanFingerprint);
            if (result.Context.Request.DecisionPoint == ScenarioDecisionPoint.PreMatch
                && AEDv2Authority.LastProposal != null)
            {
                var extended = AEDv2Authority.LastProposal;
                RuntimeLog.Log(RuntimeLogCategory.Aed,
                    $"[AED_V2][DECISION] id={extended.DecisionId:D} " +
                    $"key={extended.Key?.ToString() ?? "NONE"} intent={extended.Intent} " +
                    $"reason={extended.Reason} applied={extendedApplied} " +
                    $"plan={extended.Plan.Fingerprint()}");
            }
            var v2 = result.Context.Request.DecisionPoint == ScenarioDecisionPoint.PreMatch
                ? AEDv2Authority.LastProposal : null;
            AuditV2(matchId, result.Context.Request.ResolutionId,
                result.Context.Request.DecisionPoint,
                result.IsPrecommitRejected ? "Reject"
                    : extendedApplied ? "Applied"
                    : v2?.Changed == true ? "ShadowOnly"
                    : v2 == null ? "Fixed" : "HOLD",
                result.IsPrecommitRejected ? result.GuardReasonCode
                    : v2?.Reason ?? string.Empty,
                v2?.Key, v2?.Key.HasValue == true
                    ? AEDv2Catalog.Find(v2.Key.Value).Baseline : (double?)null,
                v2?.Key.HasValue == true ? v2.Plan.Get(v2.Key.Value) : (double?)null,
                v2?.Plan.Fingerprint() ?? AEDv2Plan.Normal().Fingerprint(), string.Empty);

            var record =
                ScenarioResolutionRecord.Create(
                    result,
                    CurrentAppliedScenarioConfig,
                    DateTime.UtcNow,
                    hasStateAuthority);

            LastAdaptiveDecision =
                result.AttemptedDecision;

            LastResolutionRecord =
                record;

            LastDebugSnapshot =
                new AEDDebugSnapshot(
                    record);

            // Every finalized attempt is useful audit evidence:
            // NEW, duplicate, conflict and precommit reject.
            _auditStore?.TryAppend(
                record);

            switch (result.CommitDisposition)
            {
                case ScenarioResolutionCommitDisposition.NewDecision:
                    RuntimeLog.Log(
                        RuntimeLogCategory.Aed,
                        $"[AED] decision={record.DecisionId:D} " +
                        $"result={record.Result?.ToString() ?? "none"} " +
                        $"reason={record.ReasonCode} " +
                        $"fallback={record.FallbackAction} " +
                        $"config={record.ResultingScenarioConfigVersion}");
                    break;

                case ScenarioResolutionCommitDisposition.DuplicateNoOp:
                    RuntimeLog.Log(
                        RuntimeLogCategory.Aed,
                        $"[AED] duplicate decision no-op " +
                        $"decision={record.DecisionId:D}");
                    break;

                case ScenarioResolutionCommitDisposition.DecisionIdentityConflict:
                    Debug.LogWarning(
                        $"[AED] decision identity conflict " +
                        $"decision={record.DecisionId:D} " +
                        $"fingerprint={record.DecisionSemanticFingerprint}");
                    break;

                case ScenarioResolutionCommitDisposition.PrecommitRejected:
                    Debug.LogWarning(
                        $"[AED] precommit rejected " +
                        $"decision={record.DecisionId:D} " +
                        $"reason={record.ReasonCode}");
                    break;
            }
        }

        public void Apply(Guid matchId, ScenarioConfig config)
        {
            if (!AEDGameplayContentContract.TryValidate(config, out var reason))
                throw new InvalidOperationException(reason);
            CurrentAppliedScenarioConfig = config;
            ScenarioConfigRuntimeRegistry.Apply(matchId, config);
        }

        public void AuditV2(Guid matchId, Guid decisionId,
            ScenarioDecisionPoint point, string status, string reason,
            AEDv2Key? key, double? previousValue, double? appliedValue,
            string planFingerprint, string evidenceFingerprint)
        {
            try
            {
                var entry = new V2AuditEntry
                {
                    matchId = matchId.ToString("D"),
                    decisionId = decisionId.ToString("D"),
                    decisionPoint = point.ToString(),
                    status = status,
                    reason = reason ?? string.Empty,
                    changedKey = key?.ToString() ?? string.Empty,
                    previousValue = previousValue?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
                    appliedValue = appliedValue?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
                    planFingerprint = planFingerprint ?? string.Empty,
                    evidenceFingerprint = evidenceFingerprint ?? string.Empty,
                    revision = AEDv2Authority.Revision,
                    occurredAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                };
                File.AppendAllText(Path.Combine(Application.persistentDataPath,
                    "aed-v2-decisions.jsonl"), JsonUtility.ToJson(entry) + "\n");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AED_V2] audit write failed: " + exception.GetType().Name);
            }
        }

        private static async Task ConfirmV2ApplyAsync(Guid matchId, Guid decisionId,
            string fingerprint)
        {
            try
            {
                if (!await new AEDSnapshotApiService().ConfirmPlanAppliedAsync(matchId,
                        decisionId, fingerprint, CancellationToken.None))
                    Debug.LogWarning("[AED_V2] backend apply receipt was not confirmed.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AED_V2] backend apply receipt failed: " + exception.GetType().Name);
            }
        }

        public void ResetForMatch(Guid oldMatchId)
        {
            if (oldMatchId != Guid.Empty) ScenarioConfigRuntimeRegistry.Clear(oldMatchId);
            AEDv2Authority.Reset(oldMatchId);
            ClearStagedBackendPreMatch(oldMatchId);
            CurrentAppliedScenarioConfig = null;
            LastAdaptiveDecision = null;
            LastDebugSnapshot = null;
            LastResolutionRecord = null;
            CurrentScenarioResolutionMode = ScenarioResolutionMode.Fixed;
            _engine.ClearLedger();
        }

        public static Guid CreateDecisionId(
            Guid matchId,
            ScenarioDecisionPoint decisionPoint,
            string phaseContext,
            uint phaseOrdinal)
        {
            return ScenarioConfigFingerprint.DeterministicGuid(
                matchId.ToString("N")
                + "|"
                + decisionPoint
                + "|"
                + (phaseContext ?? string.Empty)
                + "|"
                + phaseOrdinal);
        }
    }
}
