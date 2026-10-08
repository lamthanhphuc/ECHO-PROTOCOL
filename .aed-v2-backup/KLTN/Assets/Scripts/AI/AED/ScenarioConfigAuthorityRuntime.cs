using System;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common.Profile;
using EchoProtocol.Diagnostics;
using UnityEngine;

namespace EchoProtocol.AI.AED
{
    public sealed class ScenarioConfigAuthorityRuntime : MonoBehaviour
    {
        private static ScenarioConfigAuthorityRuntime _instance;

        [SerializeField] private AEDRuntimeSettings runtimeSettings;

        private readonly ScenarioResolutionEngine _engine = new ScenarioResolutionEngine();
        private AdaptiveDecisionLocalAuditStore _auditStore;

        public static ScenarioConfigAuthorityRuntime Instance => _instance;
        public ScenarioConfig CurrentAppliedScenarioConfig { get; private set; }
        public AdaptiveDecision LastAdaptiveDecision { get; private set; }
        public AEDDebugSnapshot LastDebugSnapshot { get; private set; }
        public ScenarioResolutionRecord LastResolutionRecord
        {
            get;
            private set;
        }

        public ScenarioResolutionMode CurrentScenarioResolutionMode { get; private set; } = ScenarioResolutionMode.Fixed;

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
            var request = new ScenarioResolutionRequest(
                CreateDecisionId(matchId, decisionPoint, phaseContext, phaseOrdinal),
                matchId,
                mode,
                decisionPoint,
                phaseContext ?? string.Empty,
                experimentCondition ?? string.Empty);

            AEDPolicyConfig policyConfig = null;
            AEDEvidencePolicy evidencePolicy = null;
            AdaptiveParameterRegistry registry = null;
            AdaptiveInputSnapshot snapshot = null;
            AdaptiveInputCurrencyValidation currency = null;
            var adaptiveInputUnavailableReason =
                string.Empty;

            if (mode == ScenarioResolutionMode.Adaptive)
            {
                if (runtimeSettings != null)
                {
                    runtimeSettings.TryBuildPolicyConfig(out policyConfig, out _);
                    runtimeSettings.TryBuildEvidencePolicy(out evidencePolicy, out _);
                    runtimeSettings.TryBuildParameterRegistry(out registry, out _);
                }

                AdaptiveInputSnapshotRuntime.TryGetSnapshot(
                    request,
                    out snapshot,
                    out currency,
                    out adaptiveInputUnavailableReason);
            }

            var result = _engine.Resolve(
                new ScenarioResolutionEngineInput(
                    request,
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
            CurrentAppliedScenarioConfig = config ?? throw new ArgumentNullException(nameof(config));
            ScenarioConfigRuntimeRegistry.Apply(matchId, config);
        }

        public void ResetForMatch(Guid oldMatchId)
        {
            if (oldMatchId != Guid.Empty) ScenarioConfigRuntimeRegistry.Clear(oldMatchId);
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
