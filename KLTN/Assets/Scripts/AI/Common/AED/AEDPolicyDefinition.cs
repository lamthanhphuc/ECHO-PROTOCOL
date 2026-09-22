using System;
using System.Collections.Generic;

namespace EchoProtocol.AI.Common.AED
{
    public sealed class AEDPolicyRuleDefinition
    {
        public AEDPolicyRuleDefinition(int priority, string ruleId, AdaptationIntent intent, ScenarioDecisionPoint decisionPoint)
        {
            Priority = priority;
            RuleId = ScenarioConfig.RequireText(ruleId, nameof(ruleId));
            Intent = intent;
            DecisionPoint = decisionPoint;
        }

        public int Priority { get; }
        public string RuleId { get; }
        public AdaptationIntent Intent { get; }
        public ScenarioDecisionPoint DecisionPoint { get; }
    }

    public static class AEDPolicyDefinition
    {
        public const string PolicySemanticId = "AED_SCENARIO_POLICY_V1_1";
        public const string NextHigherRegisteredValueStrategyId = "NEXT_HIGHER_REGISTERED_VALUE";

        private static readonly AEDPolicyRuleDefinition[] Rules =
        {
            new AEDPolicyRuleDefinition(10, "AED-PRE-010-SURVIVAL-LOW-RELIEVE", AdaptationIntent.Relieve, ScenarioDecisionPoint.PreMatch),
            new AEDPolicyRuleDefinition(20, "AED-PRE-020-NOISE-LOW-RELIEVE", AdaptationIntent.Relieve, ScenarioDecisionPoint.PreMatch),
            new AEDPolicyRuleDefinition(30, "AED-PRE-030-BOTH-HIGH-INCREASE", AdaptationIntent.IncreasePressure, ScenarioDecisionPoint.PreMatch),
            new AEDPolicyRuleDefinition(40, "AED-PRE-040-MIXED-HOLD", AdaptationIntent.Hold, ScenarioDecisionPoint.PreMatch),
            new AEDPolicyRuleDefinition(100, "AED-BND-100-HOLD", AdaptationIntent.Hold, ScenarioDecisionPoint.AllowedPhaseBoundary),
            new AEDPolicyRuleDefinition(110, "AED-FH-110-HOLD", AdaptationIntent.Hold, ScenarioDecisionPoint.FinalHuntSetup)
        };

        private static readonly ScenarioConfigKey[] ActiveKeys =
        {
            ScenarioConfigKey.SupportItemBudget,
            ScenarioConfigKey.ChaseSpeed
        };

        public static IReadOnlyList<AEDPolicyRuleDefinition> CanonicalRules => Rules;
        public static IReadOnlyList<ScenarioConfigKey> PolicyActiveKeys => ActiveKeys;

        public static bool IsPolicyActiveKey(ScenarioConfigKey key)
        {
            for (var i = 0; i < ActiveKeys.Length; i++)
            {
                if (ActiveKeys[i] == key) return true;
            }

            return false;
        }

        public static AEDPolicyRuleDefinition GetRule(string ruleId)
        {
            for (var i = 0; i < Rules.Length; i++)
            {
                if (string.Equals(Rules[i].RuleId, ruleId, StringComparison.Ordinal))
                {
                    return Rules[i];
                }
            }

            return null;
        }
    }
}
