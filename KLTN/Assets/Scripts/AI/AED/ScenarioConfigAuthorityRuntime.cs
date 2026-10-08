using System;
using System.Globalization;
using System.IO;
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
                experimentCondition ?? string.Empty);
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
                            phaseContext ?? string.Empty, experimentCondition ?? string.Empty)
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
