using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDv2DisabledRegressionTests
    {
        private static Type Authority => Type.GetType("EchoProtocol.AI.AED.AEDv2Authority, Assembly-CSharp", true);

        [TearDown]
        public void Cleanup() => Authority.GetMethod("Reset").Invoke(null, new object[] { Guid.Empty });

        [Test]
        public void FixedModeDoesNotStageV2AndClientCannotCommit()
        {
            Authority.GetMethod("Reset").Invoke(null, new object[] { Guid.Empty });
            var request = new ScenarioResolutionRequest(Guid.NewGuid(), Guid.NewGuid(),
                ScenarioResolutionMode.Fixed, ScenarioDecisionPoint.PreMatch, "PRE_MATCH", "");
            Authority.GetMethod("Stage").Invoke(null, new object[]
                { request, null, null, null, null, false, false });
            Assert.That(Authority.GetProperty("LastProposal").GetValue(null), Is.Null);
            Assert.That(Authority.GetMethod("Commit").Invoke(null,
                new object[] { request.TargetMatchId, request.ResolutionId, false, false }), Is.False);
            Assert.That(Authority.GetProperty("HasAppliedPlan").GetValue(null), Is.False);
        }
    }
}
