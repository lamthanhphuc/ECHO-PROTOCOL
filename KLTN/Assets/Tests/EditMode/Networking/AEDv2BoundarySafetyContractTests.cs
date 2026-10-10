using System;
using System.IO;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDv2BoundarySafetyContractTests
    {
        private const string MatchStateSource =
            "Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs";

        [Test]
        public void FixedModeCannotApplyAndAdaptiveNormalCanApply()
        {
            var modeType = typeof(ScenarioResolutionMode);
            var difficultyType = Type.GetType(
                "EchoProtocol.Gameplay.MatchDifficulty, Assembly-CSharp", true);
            var rulesType = Type.GetType(
                "EchoProtocol.Networking.NetworkMatchStateRules, Assembly-CSharp", true);
            var method = rulesType.GetMethod("CanApplyAEDv2Gameplay");
            var normal = Enum.Parse(difficultyType, "Normal");

            Assert.That(method.Invoke(null, new[] {
                (object)true, Enum.Parse(modeType, "Fixed"), normal
            }), Is.EqualTo(false));
            Assert.That(method.Invoke(null, new[] {
                (object)true, Enum.Parse(modeType, "Adaptive"), normal
            }), Is.EqualTo(true));
        }

        [Test]
        public void ShadowEvaluationAuditsButNeverCommitsBoundary()
        {
            var source = File.ReadAllText(MatchStateSource);
            var start = source.IndexOf("private void EvaluateAEDv2BoundaryShadow(",
                StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            var end = source.IndexOf("private static void LogAEDv2PolicyMetrics(",
                start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            var method = source.Substring(start, end - start);

            StringAssert.Contains("AuditV2", method);
            StringAssert.Contains("SubmitAEDv2BoundaryShadowAsync", method);
            StringAssert.DoesNotContain("CommitAEDv2Boundary(", method);
        }
    }
}
