using System;
using System.Reflection;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Common.Profile;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDv2AuthorityIntegrationTests
    {
        private static Type Authority => Type.GetType("EchoProtocol.AI.AED.AEDv2Authority, Assembly-CSharp", true);
        private static Type PlanData => Type.GetType("EchoProtocol.AI.AED.AEDPlanV2Data, Assembly-CSharp", true);

        [TearDown]
        public void Cleanup() => Authority.GetMethod("Reset").Invoke(null, new object[] { Guid.Empty });

        [Test]
        public void PreMatchBaselineNeverQueuesGameplayCommit()
        {
            var request = new ScenarioResolutionRequest(Guid.NewGuid(), Guid.NewGuid(),
                ScenarioResolutionMode.Adaptive, ScenarioDecisionPoint.PreMatch, "PRE_MATCH", "");
            var snapshot = Snapshot(request);
            var policy = Policy();
            var evidence = new AEDEvidencePolicy("TEST_EVIDENCE", 1, 1, true);
            var currency = new AdaptiveInputCurrencyValidation(true, true, true, true,
                true, true, true);
            var stage = Authority.GetMethod("Stage");
            var pending = Authority.GetField("_pendingApply", BindingFlags.NonPublic | BindingFlags.Static);

            stage.Invoke(null, new object[] { request, snapshot, policy, evidence, currency, true, true });
            Assert.That(pending.GetValue(null), Is.False);

            var approved = Activator.CreateInstance(PlanData);
            Set(approved, "matchId", request.TargetMatchId.ToString("D"));
            Set(approved, "decisionId", request.ResolutionId.ToString("D"));
            Set(approved, "snapshotId", snapshot.SnapshotId.ToString("D"));
            Set(approved, "snapshotFingerprint", snapshot.SnapshotContentFingerprint);
            Set(approved, "rosterIdentity", snapshot.RosterIdentity);
            Set(approved, "resultingPlanFingerprint",
                AEDv2Plan.Normal().With(AEDv2Key.SupportBonus, 2).Fingerprint());
            Set(approved, "commitStatus", "COMMITTED");
            Authority.GetMethod("ApproveBackendPreMatch").Invoke(null,
                new[] { (object)request.TargetMatchId, approved });
            stage.Invoke(null, new object[] { request, snapshot, policy, evidence, currency, true, true });
            Assert.That(pending.GetValue(null), Is.False);
            Assert.That(Authority.GetProperty("Revision").GetValue(null),
                Is.EqualTo(0u));
            Assert.That(Authority.GetProperty("HasAppliedPlan").GetValue(null),
                Is.False);

            Set(approved, "snapshotFingerprint", "stale");
            Authority.GetMethod("ApproveBackendPreMatch").Invoke(null,
                new[] { (object)request.TargetMatchId, approved });
            stage.Invoke(null, new object[] { request, snapshot, policy, evidence, currency, true, true });
            Assert.That(pending.GetValue(null), Is.False);
        }

        [Test]
        public void ShadowStageNeverQueuesGameplayCommit()
        {
            var request = new ScenarioResolutionRequest(Guid.NewGuid(), Guid.NewGuid(),
                ScenarioResolutionMode.Adaptive, ScenarioDecisionPoint.PreMatch, "PRE_MATCH", "");
            Authority.GetMethod("Stage").Invoke(null, new object[]
            {
                request, Snapshot(request), Policy(),
                new AEDEvidencePolicy("TEST_EVIDENCE", 1, 1, true),
                new AdaptiveInputCurrencyValidation(true, true, true, true, true, true, true),
                true, false
            });
            Assert.That(Authority.GetField("_pendingApply", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null), Is.False);
            Assert.That(Authority.GetProperty("Revision").GetValue(null), Is.EqualTo(0u));
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field).SetValue(target, value);

        private static AEDPolicyConfig Policy() => new AEDPolicyConfig(
            AEDPolicyDefinition.PolicySemanticId, "TEST_POLICY", "TEST_EVIDENCE",
            "TEST_REGISTRY", FixedDirector.FixedBaselineContentWhitelistVersion,
            new ScoreBandThresholds(30, 70), new ScoreBandThresholds(30, 70));

        private static AdaptiveInputSnapshot Snapshot(ScenarioResolutionRequest request)
        {
            var revision = new ProfileRevisionRef("player-1", 1);
            var player = new PlayerProfileSnapshot("player-1", 1, "lineage-1",
                new PlayerDimensionSnapshot(20, PlayerDimensionStatus.Active, 5, "SURVIVAL", 1),
                new PlayerDimensionSnapshot(80, PlayerDimensionStatus.Active, 5, "NOISE", 1));
            var summary = new RosterProfileSummary("roster-1", 1,
                new RosterDimensionSummary(1, 0, 0, 0, 1,
                    RosterAggregationStatus.Available, "SURVIVAL", 20),
                new RosterDimensionSummary(1, 0, 0, 0, 1,
                    RosterAggregationStatus.Available, "NOISE", 80));
            var provenance = new AdaptiveInputProvenance("PROFILE_FORMULA_TEST_V1",
                "SURVIVAL", "NOISE", "TEAM_FORMULA_TEST_V1",
                new[] { revision }, "TELEMETRY_TEST_V1");
            return new AdaptiveInputSnapshot(Guid.NewGuid(), "snapshot-fingerprint",
                request.TargetMatchId, request.DecisionPoint, request.PhaseContext,
                DateTime.UtcNow, "roster-1", 1, new[] { player }, summary,
                SnapshotValidity.Valid, Array.Empty<string>(), provenance);
        }
    }
}
