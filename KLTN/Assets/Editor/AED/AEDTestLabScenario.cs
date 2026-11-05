using System;
using System.Linq;
using EchoProtocol.AI.Common.AED;

namespace EchoProtocol.Editor.AED
{
    public enum LabAction { ProposeRelief, ValidateManualPlan }

    [Serializable]
    public sealed class AEDTestLabScenario
    {
        public int schemaVersion = 1;
        public string name = "High pressure / Stalker relief";
        public bool adaptive = true, completeEvidence = true, safeWindow = true, recentDown, finalPhase;
        public double evidenceAgeSeconds;
        public int zone = 1, initialOptionalBudget, liveZone3Minions;
        public AEDV02DecisionPoint decisionPoint = AEDV02DecisionPoint.SAFE_WINDOW;
        public LabAction action;
        public double[] pressure = { .9, .9, .9, .9, .9 };
        public double[] before = AEDV02Plan.Normal().ToArray();
        public double[] proposed = AEDV02Plan.Normal().ToArray();
    }

    [Serializable]
    public sealed class AEDTestLabResult
    {
        public string scope = "LOCAL SIMULATION ONLY - no Backend approval, world effect or receipt";
        public string scenario, reason;
        public AEDTestLabScenario input;
        public bool accepted;
        public double score;
        public int tier;
        public double[] before, proposed, after;
    }

    public static class AEDTestLabEvaluator
    {
        public static AEDTestLabResult Evaluate(AEDTestLabScenario input)
        {
            Validate(input);
            var previous = new AEDV02Plan(input.before);
            var result = new AEDTestLabResult
            {
                scenario = input.name,
                input = UnityEngine.JsonUtility.FromJson<AEDTestLabScenario>(UnityEngine.JsonUtility.ToJson(input)),
                before = previous.ToArray(), after = previous.ToArray(),
                proposed = previous.ToArray(), tier = -1
            };
            var weights = new[] { .30, .20, .25, .15, .10 };
            result.score = input.pressure.Select((x, i) => x * weights[i]).Sum();
            if (input.completeEvidence && input.evidenceAgeSeconds <= AEDV02PilotPolicy.EvidenceMaxAgeSeconds)
                result.tier = AEDV02PilotPolicy.Tier(result.score);
            if (!input.adaptive) return Hold(result, "HOLD_FIXED");
            if (!input.completeEvidence) return Hold(result, "HOLD_INSUFFICIENT_EVIDENCE");
            if (input.evidenceAgeSeconds > AEDV02PilotPolicy.EvidenceMaxAgeSeconds) return Hold(result, "HOLD_STALE_EVIDENCE");
            if (!input.safeWindow) return Hold(result, "HOLD_SAFE_WINDOW_CLOSED");

            AEDV02Plan next;
            if (input.action == LabAction.ProposeRelief)
            {
                if (!previous.TryProposeRelief(new[] { input.pressure }, new[] { result.score }, input.recentDown, input.zone, out next))
                    return Hold(result, "HOLD_NO_RELIEF_CANDIDATE");
            }
            else next = new AEDV02Plan(input.proposed);
            result.proposed = next.ToArray();
            if (next.Fingerprint() == previous.Fingerprint()) return Hold(result, "HOLD_NO_CHANGE");
            if (!next.TryValidateChange(previous, input.decisionPoint, input.finalPhase, input.initialOptionalBudget, out _, out var reason))
                return Hold(result, reason);
            if (!next.HasWorldPressureSlot(input.initialOptionalBudget, input.liveZone3Minions))
                return Hold(result, "HOLD_PRESSURE_HANDOFF_WORLD_NOT_REVERTED");
            result.accepted = true; result.reason = "LOCAL_CONTRACT_VALID_NOT_BACKEND_APPROVED";
            result.after = next.ToArray();
            return result;
        }

        public static void Validate(AEDTestLabScenario input)
        {
            if (input == null || input.schemaVersion != 1 || string.IsNullOrWhiteSpace(input.name) ||
                input.zone < 1 || input.zone > 3 || input.initialOptionalBudget < 0 || input.initialOptionalBudget > 2 ||
                input.liveZone3Minions < 0 || !AEDV02PilotPolicy.Finite(input.evidenceAgeSeconds) || input.evidenceAgeSeconds < 0 ||
                !Enum.IsDefined(typeof(LabAction), input.action) || !Enum.IsDefined(typeof(AEDV02DecisionPoint), input.decisionPoint) ||
                input.pressure == null || input.pressure.Length != 5 ||
                input.pressure.Any(x => !AEDV02PilotPolicy.Finite(x) || x < 0 || x > 1))
                throw new ArgumentException("Invalid scenario: schema 1, zone 1-3, optional budget 0-2, finite pressure channels 0-1 and nonnegative evidence age required.");
            _ = new AEDV02Plan(input.before); _ = new AEDV02Plan(input.proposed);
        }

        private static AEDTestLabResult Hold(AEDTestLabResult result, string reason)
        { result.reason = reason; return result; }
    }

    public static class AEDTestLabPresets
    {
        public static readonly string[] Names = { "Stalker relief", "Minion relief", "Low pressure / Hold", "Incomplete evidence", "Stale evidence", "Fixed regression", "Final Phase blocks increase", "Zone 3 local cap proposal", "Stacked pressure rejected", "Resource relief proposal", "Optional budget exceeded", "World handoff blocked" };

        public static AEDTestLabScenario Create(int index)
        {
            if (index < 0 || index >= Names.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var s = new AEDTestLabScenario { name = Names[index] };
            switch (index)
            {
                case 1: s.pressure = new[] { 0d, 1, 1, 1, 1 }; s.recentDown = true; s.zone = 2; break;
                case 2: s.pressure = new double[5]; break;
                case 3: s.completeEvidence = false; break;
                case 4: s.evidenceAgeSeconds = 11; break;
                case 5: s.adaptive = false; break;
                case 6: s.finalPhase = true; s.action = LabAction.ValidateManualPlan; s.proposed[4] = 8; break;
                case 7: s.zone = 3; s.action = LabAction.ValidateManualPlan; s.proposed[8] = 1; break;
                case 8: s.action = LabAction.ValidateManualPlan; s.before[4] = 8; s.proposed[4] = 8; s.proposed[8] = 1; break;
                case 9: s.action = LabAction.ValidateManualPlan; s.proposed[10] = 1; break;
                case 10: s.action = LabAction.ValidateManualPlan; s.proposed[9] = 1; break;
                case 11: s.action = LabAction.ValidateManualPlan; s.proposed[4] = 8; s.liveZone3Minions = 1; break;
            }
            return s;
        }
    }
}
