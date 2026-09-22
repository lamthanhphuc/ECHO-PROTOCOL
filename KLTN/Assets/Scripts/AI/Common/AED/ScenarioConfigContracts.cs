using System;

namespace EchoProtocol.AI.Common.AED
{
    public enum ScenarioResolutionMode { Fixed, Adaptive }
    public enum ScenarioDecisionPoint { PreMatch, AllowedPhaseBoundary, FinalHuntSetup }
    public enum ScenarioConfigSource { Fixed, Adaptive }
    public enum ScenarioConfigBaseKind { PreMatchResolvedBase, AppliedScenarioConfig }
    public enum CandidateValidationStatus { NotEvaluated, Valid, Invalid }
    public enum AdaptiveDecisionResult { Applied, NoChange, FixedFallback }
    public enum AdaptationIntent { Relieve, Hold, IncreasePressure }
    public enum ScenarioFallbackAction { None, FullFixedConfig, KeepLastValidConfig }
    public enum PolicyNoChangeReason { None, HoldRule, ValueLimitReached, KeyNotApplicable }
    public enum PressureAxis { None, DetectionPressure, ChasePressure, SearchPressure }

    public enum ScenarioConfigKey
    {
        ObjectiveSpawnSetId,
        SupportItemBudget,
        DetectionFillRate,
        DetectionDecayRate,
        ChaseSpeed,
        SearchDuration,
        RouteModifier,
        EscapeDoorTimer
    }

    public sealed class ScenarioMonsterParameters
    {
        public ScenarioMonsterParameters(
            double detectionFillRate,
            double detectionDecayRate,
            double chaseSpeed,
            double searchDuration)
        {
            RequireFinite(detectionFillRate, nameof(detectionFillRate));
            RequireFinite(detectionDecayRate, nameof(detectionDecayRate));
            RequireFinite(chaseSpeed, nameof(chaseSpeed));
            RequireFinite(searchDuration, nameof(searchDuration));

            DetectionFillRate = detectionFillRate;
            DetectionDecayRate = detectionDecayRate;
            ChaseSpeed = chaseSpeed;
            SearchDuration = searchDuration;
        }

        public double DetectionFillRate { get; }
        public double DetectionDecayRate { get; }
        public double ChaseSpeed { get; }
        public double SearchDuration { get; }

        internal static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "Scenario numeric values must be finite.");
            }
        }
    }

    public sealed class ScenarioFinalHuntParameters
    {
        public ScenarioFinalHuntParameters(double escapeDoorTimerSeconds)
        {
            ScenarioMonsterParameters.RequireFinite(escapeDoorTimerSeconds, nameof(escapeDoorTimerSeconds));
            EscapeDoorTimerSeconds = escapeDoorTimerSeconds;
        }

        public double EscapeDoorTimerSeconds { get; }
    }

    public sealed class ScenarioConfig
    {
        public ScenarioConfig(
            string scenarioConfigVersion,
            string policyVersion,
            ScenarioConfigSource configSource,
            string mapId,
            string monsterType,
            string objectiveSpawnSetId,
            int supportItemBudget,
            ScenarioMonsterParameters monsterParameters,
            string routeModifier,
            ScenarioFinalHuntParameters finalHuntParameters,
            string fallbackConfigId)
        {
            ScenarioConfigVersion = RequireText(scenarioConfigVersion, nameof(scenarioConfigVersion));
            PolicyVersion = RequireText(policyVersion, nameof(policyVersion));
            ConfigSource = configSource;
            MapId = RequireText(mapId, nameof(mapId));
            MonsterType = RequireText(monsterType, nameof(monsterType));
            ObjectiveSpawnSetId = RequireText(objectiveSpawnSetId, nameof(objectiveSpawnSetId));
            if (supportItemBudget < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(supportItemBudget));
            }

            SupportItemBudget = supportItemBudget;
            MonsterParameters = monsterParameters ?? throw new ArgumentNullException(nameof(monsterParameters));
            RouteModifier = RequireText(routeModifier, nameof(routeModifier));
            FinalHuntParameters = finalHuntParameters ?? throw new ArgumentNullException(nameof(finalHuntParameters));
            FallbackConfigId = RequireText(fallbackConfigId, nameof(fallbackConfigId));
        }

        public string ScenarioConfigVersion { get; }
        public string PolicyVersion { get; }
        public ScenarioConfigSource ConfigSource { get; }
        public string MapId { get; }
        public string MonsterType { get; }
        public string ObjectiveSpawnSetId { get; }
        public int SupportItemBudget { get; }
        public ScenarioMonsterParameters MonsterParameters { get; }
        public string RouteModifier { get; }
        public ScenarioFinalHuntParameters FinalHuntParameters { get; }
        public string FallbackConfigId { get; }

        public ScenarioConfig WithVersionAndSource(string version, ScenarioConfigSource source)
        {
            return new ScenarioConfig(
                version,
                PolicyVersion,
                source,
                MapId,
                MonsterType,
                ObjectiveSpawnSetId,
                SupportItemBudget,
                MonsterParameters,
                RouteModifier,
                FinalHuntParameters,
                FallbackConfigId);
        }

        internal static string RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", name);
            }

            return value;
        }
    }

    public sealed class ScenarioConfigBaseRef
    {
        public ScenarioConfigBaseRef(
            string baseConfigId,
            string baseScenarioConfigVersion,
            string baseContentFingerprint,
            ScenarioConfigBaseKind baseKind)
        {
            BaseConfigId = ScenarioConfig.RequireText(baseConfigId, nameof(baseConfigId));
            BaseScenarioConfigVersion = ScenarioConfig.RequireText(baseScenarioConfigVersion, nameof(baseScenarioConfigVersion));
            BaseContentFingerprint = ScenarioConfig.RequireText(baseContentFingerprint, nameof(baseContentFingerprint));
            BaseKind = baseKind;
        }

        public string BaseConfigId { get; }
        public string BaseScenarioConfigVersion { get; }
        public string BaseContentFingerprint { get; }
        public ScenarioConfigBaseKind BaseKind { get; }
    }

    public sealed class ScenarioResolutionRequest
    {
        public ScenarioResolutionRequest(
            Guid resolutionId,
            Guid targetMatchId,
            ScenarioResolutionMode resolutionMode,
            ScenarioDecisionPoint decisionPoint,
            string phaseContext,
            string experimentCondition)
        {
            if (resolutionId == Guid.Empty) throw new ArgumentException("Resolution id is required.", nameof(resolutionId));
            if (targetMatchId == Guid.Empty) throw new ArgumentException("Target match id is required.", nameof(targetMatchId));

            ResolutionId = resolutionId;
            TargetMatchId = targetMatchId;
            ResolutionMode = resolutionMode;
            DecisionPoint = decisionPoint;
            PhaseContext = phaseContext ?? string.Empty;
            ExperimentCondition = experimentCondition ?? string.Empty;
        }

        public Guid ResolutionId { get; }
        public Guid TargetMatchId { get; }
        public ScenarioResolutionMode ResolutionMode { get; }
        public ScenarioDecisionPoint DecisionPoint { get; }
        public string PhaseContext { get; }
        public string ExperimentCondition { get; }
    }
}
