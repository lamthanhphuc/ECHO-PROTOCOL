using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.Editor.AED
{
    public sealed class AEDTestLabTests
    {
        [Test] public void ExportedInputIsImmutableAndCanReplayAfterJsonRoundTrip()
        {
            var input = new AEDTestLabScenario();
            var result = AEDTestLabEvaluator.Evaluate(input);
            input.pressure[0] = 0; input.before[4] = 8;
            var restored = UnityEngine.JsonUtility.FromJson<AEDTestLabResult>(UnityEngine.JsonUtility.ToJson(result));
            Assert.That(restored.input.pressure[0], Is.EqualTo(.9));
            var replay = AEDTestLabEvaluator.Evaluate(restored.input);
            Assert.That(replay.after, Is.EqualTo(result.after));
            Assert.That(replay.reason, Is.EqualTo(result.reason));
        }

        [Test] public void EveryPresetProducesItsExpectedOutcome()
        {
            var expected = new[] { "LOCAL_CONTRACT_VALID_NOT_BACKEND_APPROVED", "LOCAL_CONTRACT_VALID_NOT_BACKEND_APPROVED", "HOLD_NO_RELIEF_CANDIDATE", "HOLD_INSUFFICIENT_EVIDENCE", "HOLD_STALE_EVIDENCE", "HOLD_FIXED", "HOLD_PHASE4_INCREASE", "LOCAL_CONTRACT_VALID_NOT_BACKEND_APPROVED", "HOLD_GLOBAL_PRESSURE_BUDGET", "LOCAL_CONTRACT_VALID_NOT_BACKEND_APPROVED", "HOLD_INITIAL_OPTIONAL_BUDGET", "HOLD_PRESSURE_HANDOFF_WORLD_NOT_REVERTED" };
            Assert.That(AEDTestLabPresets.Names.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
                Assert.That(AEDTestLabEvaluator.Evaluate(AEDTestLabPresets.Create(i)).reason, Is.EqualTo(expected[i]), AEDTestLabPresets.Names[i]);
        }

        [Test] public void CorruptImportedScenarioIsRejected()
        {
            var input = new AEDTestLabScenario { schemaVersion = 2 };
            Assert.Throws<ArgumentException>(() => AEDTestLabEvaluator.Evaluate(input));
            input.schemaVersion = 1; input.before = new double[11];
            Assert.Throws<ArgumentException>(() => AEDTestLabEvaluator.Evaluate(input));
            input.before = AEDV02Plan.Normal().ToArray(); input.pressure = null;
            Assert.Throws<ArgumentException>(() => AEDTestLabEvaluator.Evaluate(input));
        }

        [Test] public void HighPressureUsesRealReliefPolicyWithoutMutatingInput()
        {
            var input = new AEDTestLabScenario();
            var result = AEDTestLabEvaluator.Evaluate(input);
            Assert.That(result.accepted, Is.True);
            Assert.That(result.after[(int)AEDV02Key.ChaseSpeed], Is.EqualTo(7));
            Assert.That(input.before[(int)AEDV02Key.ChaseSpeed], Is.EqualTo(7.5));
            Assert.That(result.score, Is.EqualTo(.9).Within(.000001));
        }

        [TestCase("fixed", "HOLD_FIXED")]
        [TestCase("incomplete", "HOLD_INSUFFICIENT_EVIDENCE")]
        [TestCase("stale", "HOLD_STALE_EVIDENCE")]
        [TestCase("unsafe", "HOLD_SAFE_WINDOW_CLOSED")]
        public void UnavailableEvidenceAndFixedNeverAdvance(string mode, string reason)
        {
            var input = new AEDTestLabScenario();
            input.adaptive = mode != "fixed"; input.completeEvidence = mode != "incomplete";
            input.evidenceAgeSeconds = mode == "stale" ? 11 : 0; input.safeWindow = mode != "unsafe";
            var result = AEDTestLabEvaluator.Evaluate(input);
            Assert.That(result.accepted, Is.False); Assert.That(result.reason, Is.EqualTo(reason));
            Assert.That(result.after, Is.EqualTo(input.before));
        }

        [Test] public void FinalPhaseKeepsBlockedProposalSeparateFromAcceptedState()
        {
            var input = new AEDTestLabScenario { finalPhase = true, action = LabAction.ValidateManualPlan };
            input.proposed = AEDV02Plan.Normal().With(AEDV02Key.ChaseSpeed, 8).ToArray();
            var result = AEDTestLabEvaluator.Evaluate(input);
            Assert.That(result.reason, Is.EqualTo("HOLD_PHASE4_INCREASE"));
            Assert.That(result.after[4], Is.EqualTo(7.5)); Assert.That(result.proposed[4], Is.EqualTo(8));
        }

        [Test] public void LiveMinionBlocksPressureHandoff()
        {
            var input = new AEDTestLabScenario { action = LabAction.ValidateManualPlan, liveZone3Minions = 1 };
            input.proposed = AEDV02Plan.Normal().With(AEDV02Key.ChaseSpeed, 8).ToArray();
            Assert.That(AEDTestLabEvaluator.Evaluate(input).reason, Is.EqualTo("HOLD_PRESSURE_HANDOFF_WORLD_NOT_REVERTED"));
        }

        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-.1)] [TestCase(1.1)]
        public void InvalidPressureIsRejected(double value)
        {
            var input = new AEDTestLabScenario(); input.pressure[0] = value;
            Assert.Throws<ArgumentException>(() => AEDTestLabEvaluator.Evaluate(input));
        }

        [Test] public void SequentialReliefUsesPreviousAcceptedPlan()
        {
            var input = new AEDTestLabScenario(); var first = AEDTestLabEvaluator.Evaluate(input);
            input.before = first.after; var second = AEDTestLabEvaluator.Evaluate(input);
            Assert.That(second.accepted, Is.True); Assert.That(second.after[4], Is.EqualTo(7));
            Assert.That(second.after[0], Is.EqualTo(1.5));
        }
    }
}
