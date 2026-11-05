using System;
using System.Collections.Generic;
using System.Linq;
using EchoProtocol.AI.Common.Profile;

namespace EchoProtocol.AI.Common.AED
{
    // v2 is an opt-in extension; v1.1 canonical decisions and persisted schemas remain unchanged.
    public enum AEDv2Key
    {
        SupportBonus = 0,
        ReviveBonusPerZone = 1,
        DetectionAcquireSeconds = 2,
        DetectionForgetSeconds = 3,
        ChaseSpeed = 4,
        SearchSeconds = 5,
        HearingMultiplier = 6,
        SeekPlayersAfterSeconds = 7,
        CoreCarrierAfterSeconds = 8,
        SpecialCooldownSeconds = 9,
        DoorBreakSeconds = 10,
        PatrolSpeed = 11,
        PostChaseCooldownSeconds = 12,
        PostAttackCooldownSeconds = 13,
        SameRoomCooldownSeconds = 14,
        JumpEntryReuseSeconds = 15,
        PostSpecialCooldownSeconds = 16,
        ObjectiveNoiseInvestigationEnabled = 17,
        Zone1MinionCap = 18,
        Zone2MinionCap = 19,
        CoreCarrierPressureEnabled = 20
    }

    public enum AEDv2Axis { Resource, Detection, Pursuit, Encounter }

    public sealed class AEDv2ParameterSpec
    {
        public AEDv2ParameterSpec(
            AEDv2Key key, AEDv2Axis axis, double baseline, double relief,
            double pressure, bool reliefOnly = false, bool preMatchOnly = false)
        {
            if (!Finite(baseline) || !Finite(relief) || !Finite(pressure))
                throw new ArgumentOutOfRangeException(nameof(baseline));
            Key = key;
            Axis = axis;
            Baseline = baseline;
            Relief = relief;
            Pressure = pressure;
            ReliefOnly = reliefOnly;
            PreMatchOnly = preMatchOnly;
        }
        public AEDv2Key Key { get; }
        public AEDv2Axis Axis { get; }
        public double Baseline { get; }
        public double Relief { get; }
        public double Pressure { get; }
        public bool ReliefOnly { get; }
        public bool PreMatchOnly { get; }

        public bool HasTarget(AdaptationIntent intent) =>
            intent == AdaptationIntent.Relieve ? Relief != Baseline :
            intent == AdaptationIntent.IncreasePressure && !ReliefOnly && Pressure != Baseline;

        public double Target(AdaptationIntent intent) =>
            intent == AdaptationIntent.Relieve ? Relief :
            intent == AdaptationIntent.IncreasePressure ? Pressure : Baseline;

        public bool IsRegistered(double value) =>
            Finite(value) && (value == Baseline || value == Relief || value == Pressure);

        private static bool Finite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public static class AEDv2Catalog
    {
        // Candidate levels for initial test only. Bound values must be tuned against pilot data.
        // Combat hitbox, windup, recovery, flashlight semantics, LOS, teleport rules and
        // escape-route legality are deliberately absent from the adaptive whitelist.
        private static readonly AEDv2ParameterSpec[] Rules =
        {
            new AEDv2ParameterSpec(AEDv2Key.SupportBonus, AEDv2Axis.Resource, 0, 2, 0, true, true),
            new AEDv2ParameterSpec(AEDv2Key.ReviveBonusPerZone, AEDv2Axis.Resource, 0, 1, 0, true),
            new AEDv2ParameterSpec(AEDv2Key.DetectionAcquireSeconds, AEDv2Axis.Detection, 1.25, 1.50, 1.00),
            new AEDv2ParameterSpec(AEDv2Key.DetectionForgetSeconds, AEDv2Axis.Detection, 25, 20, 30),
            new AEDv2ParameterSpec(AEDv2Key.ChaseSpeed, AEDv2Axis.Pursuit, 7.5, 7.0, 8.0),
            new AEDv2ParameterSpec(AEDv2Key.SearchSeconds, AEDv2Axis.Pursuit, 2.25, 1.5, 3),
            new AEDv2ParameterSpec(AEDv2Key.HearingMultiplier, AEDv2Axis.Detection, 1, 0.85, 1.15),
            new AEDv2ParameterSpec(AEDv2Key.SeekPlayersAfterSeconds, AEDv2Axis.Pursuit, 120, 150, 90),
            new AEDv2ParameterSpec(AEDv2Key.CoreCarrierAfterSeconds, AEDv2Axis.Pursuit, 25, 30, 20),
            new AEDv2ParameterSpec(AEDv2Key.SpecialCooldownSeconds, AEDv2Axis.Encounter, 420, 540, 360),
            new AEDv2ParameterSpec(AEDv2Key.DoorBreakSeconds, AEDv2Axis.Pursuit, 4.5, 5.5, 3.5),
            new AEDv2ParameterSpec(AEDv2Key.PatrolSpeed, AEDv2Axis.Pursuit, 6.5, 6, 7),
            new AEDv2ParameterSpec(AEDv2Key.PostChaseCooldownSeconds, AEDv2Axis.Encounter, 18, 28, 12),
            new AEDv2ParameterSpec(AEDv2Key.PostAttackCooldownSeconds, AEDv2Axis.Encounter, 22, 32, 15),
            new AEDv2ParameterSpec(AEDv2Key.SameRoomCooldownSeconds, AEDv2Axis.Encounter, 15, 22, 10),
            new AEDv2ParameterSpec(AEDv2Key.JumpEntryReuseSeconds, AEDv2Axis.Encounter, 120, 180, 90),
            new AEDv2ParameterSpec(AEDv2Key.PostSpecialCooldownSeconds, AEDv2Axis.Encounter, 20, 30, 15),
            // Keep objective noise and core-carrier investigation independently tunable.
            new AEDv2ParameterSpec(AEDv2Key.ObjectiveNoiseInvestigationEnabled, AEDv2Axis.Encounter, 1, 0, 1, true),
            // The actual Normal Zone2 cap (2) exceeds Hard (1). Thus minion pressure is DISABLED.
            new AEDv2ParameterSpec(AEDv2Key.Zone1MinionCap, AEDv2Axis.Encounter, 1, 0, 1, true, true),
            new AEDv2ParameterSpec(AEDv2Key.Zone2MinionCap, AEDv2Axis.Encounter, 2, 1, 2, true, true),
            new AEDv2ParameterSpec(AEDv2Key.CoreCarrierPressureEnabled, AEDv2Axis.Encounter, 1, 0, 1, true)
        };

        static AEDv2Catalog()
        {
            if (Rules.Length != Enum.GetValues(typeof(AEDv2Key)).Length)
                throw new InvalidOperationException("AED_V2_CATALOG_INCOMPLETE");
            for (var i = 0; i < Rules.Length; i++)
                if ((int)Rules[i].Key != i)
                    throw new InvalidOperationException("AED_V2_CATALOG_KEY_ORDER_INVALID");
        }

        public static IReadOnlyList<AEDv2ParameterSpec> All => Array.AsReadOnly(Rules);

        public static AEDv2ParameterSpec Find(AEDv2Key key)
        {
            var index = (int)key;
            if (index < 0 || index >= Rules.Length || Rules[index].Key != key)
                throw new ArgumentOutOfRangeException(nameof(key));
            return Rules[index];
        }
    }

    public sealed class AEDv2Plan
    {
        private readonly double[] _values;
        private AEDv2Plan(double[] values) { _values = (double[])values.Clone(); }

        public static AEDv2Plan Normal()
        {
            var rules = AEDv2Catalog.All;
            var values = new double[rules.Count];
            for (int i = 0; i < rules.Count; i++) values[i] = rules[i].Baseline;
            return new AEDv2Plan(values);
        }

        public double Get(AEDv2Key key)
        {
            AEDv2Catalog.Find(key);
            return _values[(int)key];
        }

        public int SupportBonus => (int)Get(AEDv2Key.SupportBonus);
        public int ReviveBonus => (int)Get(AEDv2Key.ReviveBonusPerZone);

        public AEDv2Plan With(AEDv2Key key, double value)
        {
            if (!AEDv2Catalog.Find(key).IsRegistered(value))
                throw new ArgumentOutOfRangeException(nameof(value), "AED_V2_VALUE_NOT_REGISTERED");
            var copy = (double[])_values.Clone();
            copy[(int)key] = value;
            return new AEDv2Plan(copy);
        }

        public bool TrySingleBoundedChange(out AEDv2Key changedKey)
        {
            changedKey = default;
            var changes = 0;
            foreach (var spec in AEDv2Catalog.All)
            {
                var actual = Get(spec.Key);
                if (!spec.IsRegistered(actual)) return false;
                if (actual == spec.Baseline) continue;
                changedKey = spec.Key;
                changes++;
            }
            return changes <= 1;
        }

        public bool IsBounded()
        {
            foreach (var spec in AEDv2Catalog.All)
                if (!spec.IsRegistered(Get(spec.Key))) return false;
            return true;
        }

        public string Fingerprint()
        {
            var parts = new System.Text.StringBuilder("AED_DIFFICULTY_V2|NORMAL");
            foreach (var spec in AEDv2Catalog.All)
                parts.Append('|').Append(spec.Key).Append('=')
                    .Append(Get(spec.Key).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            return ScenarioConfigFingerprint.DeterministicGuid(parts.ToString()).ToString("N");
        }


    }

    public sealed class AEDv2Decision
    {
        public AEDv2Decision(Guid decisionId, bool eligible, bool changed, string reason,
            AdaptationIntent intent, AEDv2Key? key, AEDv2Plan plan)
        {
            DecisionId = decisionId;
            Eligible = eligible;
            Changed = changed;
            Reason = reason ?? string.Empty;
            Intent = intent;
            Key = key;
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }
        public Guid DecisionId { get; }
        public bool Eligible { get; }
        public bool Changed { get; }
        public string Reason { get; }
        public AdaptationIntent Intent { get; }
        public AEDv2Key? Key { get; }
        public AEDv2Plan Plan { get; }
    }

    public static class AEDv2Policy
    {
        public static AEDv2Decision Evaluate(
            ScenarioResolutionRequest request,
            AdaptiveInputSnapshot snapshot,
            AEDInputGateResult gate,
            AEDPolicyConfig policy)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var normal = AEDv2Plan.Normal();
            if (request.ResolutionMode != ScenarioResolutionMode.Adaptive)
                return Hold(request, normal, "AED_V2_FIXED_MODE");
            if (request.DecisionPoint == ScenarioDecisionPoint.PreMatch)
                return Hold(request, normal, "AED_V2_FIRST_PHASE_BASELINE");
            if (request.DecisionPoint != ScenarioDecisionPoint.PreMatch)
                return Hold(request, normal, "AED_V2_BOUNDARY_HOLD_NO_CURRENT_MATCH_EVIDENCE");
            if (gate == null || gate.Status != AEDInputGateStatus.Eligible ||
                snapshot?.RosterProfileSummary == null || policy == null || !policy.IsValid)
                return Hold(request, normal, "AED_V2_INPUT_INELIGIBLE");

            var survival = snapshot.RosterProfileSummary.Survival.MeanObservedScore;
            var noise = snapshot.RosterProfileSummary.Noise.MeanObservedScore;
            if (!survival.HasValue || !noise.HasValue)
                return Hold(request, normal, "AED_V2_SCORE_MISSING");

            var s = policy.SurvivalThresholds.Classify(survival.Value);
            var n = policy.NoiseThresholds.Classify(noise.Value);

            var roster = snapshot.RosterProfileSummary;

            bool Ready(
                RosterDimensionSummary dimension,
                Func<PlayerProfileSnapshot, PlayerDimensionSnapshot> select)
            {
                return dimension != null
                    && dimension.AggregationStatus == RosterAggregationStatus.Available
                    && dimension.ObservedActiveCount == snapshot.TeamSize
                    && dimension.MeanObservedScore.HasValue
                    && snapshot.PlayerProfileSnapshots.Count == snapshot.TeamSize
                    && snapshot.PlayerProfileSnapshots.All(player =>
                    {
                        var d = select(player);
                        return d != null
                            && d.Status == PlayerDimensionStatus.Active
                            && d.Score.HasValue
                            && d.SampleCount >= 2;
                    });
            }

            var objectiveReady = Ready(
                roster.Objective, p => p.Objective);
            var toolReady = Ready(
                roster.ToolUsage, p => p.ToolUsage);

            var objectiveHigh = objectiveReady
                && roster.Objective.MeanObservedScore.Value >= 70d;
            var toolHigh = toolReady
                && roster.ToolUsage.MeanObservedScore.Value >= 70d;

            var intent = AdaptationIntent.Hold;
            var key = AEDv2Key.ChaseSpeed;

            if (s == ScoreBand.Low)
            {
                intent = AdaptationIntent.Relieve;
                key = AEDv2Key.SupportBonus;
            }
            else if (n == ScoreBand.Low)
            {
                intent = AdaptationIntent.Relieve;
                key = AEDv2Key.DetectionAcquireSeconds;
            }
            else if (s == ScoreBand.High && n == ScoreBand.High)
            {
                intent = AdaptationIntent.IncreasePressure;

                if (objectiveHigh && toolHigh)
                    key = AEDv2Key.SpecialCooldownSeconds;
                else if (objectiveHigh)
                    key = AEDv2Key.PatrolSpeed;
                else
                    key = AEDv2Key.ChaseSpeed;
            }

            if (intent == AdaptationIntent.Hold)
                return Hold(request, normal, "AED_V2_HOLD");

            return EvaluateKey(request, gate, intent, key);
        }

        public static AEDv2Decision EvaluateKey(
            ScenarioResolutionRequest request, AEDInputGateResult gate,
            AdaptationIntent intent, AEDv2Key key)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var normal = AEDv2Plan.Normal();
            if (request.ResolutionMode != ScenarioResolutionMode.Adaptive ||
                request.DecisionPoint != ScenarioDecisionPoint.PreMatch ||
                gate == null || gate.Status != AEDInputGateStatus.Eligible)
                return Hold(request, normal, "AED_V2_GATE_OR_TIMING_REJECTED");
            var spec = AEDv2Catalog.Find(key);
            if (!spec.HasTarget(intent))
                return Hold(request, normal, "AED_V2_KEY_OR_DIRECTION_REJECTED");
            var candidate = normal.With(key, spec.Target(intent));
            if (!candidate.TrySingleBoundedChange(out var changed) || changed != key)
                return Hold(request, normal, "AED_V2_FAIRNESS_REJECTED");
            return new AEDv2Decision(request.ResolutionId, true, true, "AED_V2_VALID",
                intent, key, candidate);
        }

        private static AEDv2Decision Hold(ScenarioResolutionRequest request, AEDv2Plan normal, string reason)
            => new AEDv2Decision(request.ResolutionId, false, false, reason,
                AdaptationIntent.Hold, null, normal);
    }
}
