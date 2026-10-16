using System;
using System.Reflection;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDV02HostGuardTests
    {
        private static Type Authority => Type.GetType("EchoProtocol.AI.AED.AEDv2Authority, Assembly-CSharp", true);
        private readonly Guid _match = Guid.NewGuid();
        private readonly DateTime _now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        private readonly string _roster = AEDV02Plan.Hash("ROSTER");
        [SetUp] public void Reset() => Authority.GetMethod("Reset").Invoke(null, new object[] { Guid.Empty });
        [TearDown] public void Cleanup() => Reset();

        [Test]
        public void V02_ApprovedHostPlan_IsConsumedByStalkerAndReplayDoesNotIncrementRevision()
        {
            var initial = Payload(AEDV02Plan.Normal(), 0);
            Assert.That(Commit(initial, true, true, true, true), Is.True);
            Assert.That(Commit(initial, true, true, true, true), Is.True);
            var relief = AEDV02Plan.Normal().With(AEDV02Key.ChaseSpeed, 7);
            var changed = Payload(relief, 1);
            Assert.That(Commit(changed, true, true, true, true), Is.True);
            var get = new object[] { _match, null, 0 };
            Assert.That(Authority.GetMethod("TryGetV02").Invoke(null, get), Is.EqualTo(true));
            Assert.That(get[2], Is.EqualTo(2));
            var bridge = Type.GetType("EchoProtocol.AI.AED.AEDv2GameplayBridge, Assembly-CSharp", true);
            var profile = bridge.GetMethod("ToV02NormalDifficultyProfile").Invoke(null, new[] { get[1] });
            var gameObject = new GameObject("V02ConsumerTest");
            try
            {
                var controller = gameObject.AddComponent(Type.GetType("EchoProtocol.AI.Stalker.StalkerController, Assembly-CSharp", true));
                controller.GetType().GetMethod("SetAEDv2ProfileOwnership").Invoke(controller, new object[] { true });
                controller.GetType().GetMethod("ApplyMatchDifficulty").Invoke(controller, new[] { profile });
                Assert.That(controller.GetType().GetProperty("AppliedChaseSpeed").GetValue(controller), Is.EqualTo(7));
                Assert.That(controller.GetType().GetProperty("AppliedSearchDuration").GetValue(controller), Is.EqualTo(2.25));
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        [TestCase("client")]
        [TestCase("fixed")]
        [TestCase("toggle")]
        [TestCase("mode")]
        [TestCase("difficulty")]
        [TestCase("snapshot")]
        [TestCase("roster")]
        [TestCase("match")]
        [TestCase("expired")]
        [TestCase("fingerprint")]
        [TestCase("policy")]
        [TestCase("window")]
        [TestCase("approval")]
        [TestCase("revision")]
        [TestCase("unsafe")]
        [TestCase("missing-source")]
        public void V02_HostGuardFailsClosed(string failure)
        {
            Assert.That(Commit(Payload(AEDV02Plan.Normal(), 0), true, true, true, true), Is.True);
            var data = Payload(AEDV02Plan.Normal().With(AEDV02Key.ChaseSpeed, 7), 1);
            switch (failure)
            {
                case "mode": Set(data.Request, "resolutionMode", "Fixed"); break;
                case "difficulty": Set(data.Request, "difficulty", "Hard"); break;
                case "snapshot": Set(data.Approval, "snapshotFingerprint", AEDV02Plan.Hash("wrong")); break;
                case "roster": Set(data.Approval, "rosterIdentity", AEDV02Plan.Hash("wrong")); break;
                case "match": Set(data.Approval, "matchId", Guid.NewGuid().ToString("D")); break;
                case "expired": Set(data.Approval, "expiresAtUtc", _now.AddSeconds(-1).ToString("O")); break;
                case "fingerprint": Set(data.Approval, "planFingerprint", AEDV02Plan.Hash("wrong")); break;
                case "policy": Set(data.Request, "policyVersion", "WRONG"); break;
                case "window": Set(data.Approval, "windowId", AEDV02Plan.Hash("wrong")); break;
                case "approval": Set(data.Approval, "approvalStatus", "HOLD"); break;
                case "revision": Set(data.Approval, "expectedPlanRevision", 0); break;
                case "missing-source": Set(data.Request, "sourceFingerprint", ""); Set(data.Approval, "sourceFingerprint", ""); break;
            }
            Assert.That(Commit(data, failure != "client", failure != "fixed", failure != "toggle", failure != "unsafe"), Is.False);
            var read = new object[] { _match, null, 0 };
            Authority.GetMethod("TryGetV02").Invoke(null, read);
            Assert.That(read[2], Is.EqualTo(1));
            Assert.That(((AEDV02Plan)read[1]).Fingerprint(), Is.EqualTo(AEDV02Plan.Normal().Fingerprint()));
        }

        [Test]
        public void V02_NormalProfilePreservesEveryFixedNormalParameter()
        {
            var bridge = Type.GetType("EchoProtocol.AI.AED.AEDv2GameplayBridge, Assembly-CSharp", true);
            var profile = bridge.GetMethod("ToV02NormalDifficultyProfile").Invoke(null, new object[] { AEDV02Plan.Normal() });
            var difficulty = Type.GetType("EchoProtocol.Gameplay.MatchDifficulty, Assembly-CSharp", true);
            var normal = Type.GetType("EchoProtocol.Gameplay.MatchDifficultyProfiles, Assembly-CSharp", true).GetMethod("Get")
                .Invoke(null, new[] { Enum.Parse(difficulty, "Normal") });
            foreach (var property in profile.GetType().GetProperties()) Assert.That(property.GetValue(profile), Is.EqualTo(property.GetValue(normal)), property.Name);
        }

        [TestCase(0, 1, false)]
        [TestCase(1, 1, true)]
        [TestCase(1, 2, true)]
        [TestCase(1, 3, false)]
        [TestCase(1, 4, false)]
        public void V02_PilotCabinetPermissionReservesOnlyTheTwoApprovedZones(int enabled, int zone, bool reserved)
        {
            var plan = AEDV02Plan.Normal().With(AEDV02Key.CabinetAmbushEnabled, enabled);
            Assert.That(plan.ReservesCabinetBudget(zone), Is.EqualTo(reserved));
            Assert.That(plan.PositiveTokens(0), Is.Zero); // Permission is not a fabricated active encounter.
            Assert.That(plan.Get(AEDV02Key.DetectionAcquireSeconds), Is.EqualTo(1.25));
        }

        private bool Commit((object Request, object Approval) data, bool host, bool adaptiveNormal, bool enabled, bool safe) =>
            (bool)Authority.GetMethod("CommitV02").Invoke(null, new object[] { _match, data.Request, data.Approval, _roster, host, adaptiveNormal, enabled, safe, _now, null });
        private (object Request, object Approval) Payload(AEDV02Plan plan, int previousRevision)
        {
            var request = Activator.CreateInstance(Type.GetType("EchoProtocol.AI.AED.AEDV02DecisionRequestDto, Assembly-CSharp", true));
            var approval = Activator.CreateInstance(Type.GetType("EchoProtocol.AI.AED.AEDV02DecisionResultDto, Assembly-CSharp", true));
            var point = previousRevision == 0 ? AEDV02DecisionPoint.PRE_MATCH : AEDV02DecisionPoint.SAFE_WINDOW;
            var source = previousRevision == 0 ? "" : AEDV02Plan.Hash("source");
            int ordinal = previousRevision == 0 ? 0 : 1, sequence = previousRevision == 0 ? 0 : 1;
            string decision = Guid.NewGuid().ToString("D"), snapshot = Guid.NewGuid().ToString("D"), scene = AEDV02Plan.Hash("scene");
            foreach (var target in new[] { request, approval })
            {
                Set(target, "decisionId", decision); Set(target, "phaseOrdinal", ordinal); Set(target, "decisionPoint", point.ToString());
                Set(target, "windowSequence", sequence); Set(target, "windowId", AEDV02Plan.WindowId(_match, ordinal, point, sequence, 0, 1800, source));
                Set(target, "expectedPlanRevision", previousRevision); Set(target, "previousPlanFingerprint", AEDV02Plan.Normal().Fingerprint());
                Set(target, "planValues", plan.ToArray()); Set(target, "snapshotId", snapshot); Set(target, "snapshotFingerprint", AEDV02Plan.Hash("snapshot"));
                Set(target, "rosterIdentity", _roster); Set(target, "sourceFingerprint", source); Set(target, "sceneManifestFingerprint", scene);
                Set(target, "policyVersion", AEDV02PilotPolicy.Version); Set(target, "catalogVersion", AEDV02Plan.CatalogVersion); Set(target, "targetPhaseName", "CORE_COLLECTION");
            }
            Set(request, "resolutionMode", "Adaptive"); Set(request, "difficulty", "Normal"); Set(request, "startTick", 0L); Set(request, "endTick", 1800L);
            Set(approval, "matchId", _match.ToString("D")); Set(approval, "planRevision", previousRevision + 1); Set(approval, "planFingerprint", plan.Fingerprint());
            Set(approval, "approvalStatus", "APPROVED"); Set(approval, "applyStatus", "PENDING"); Set(approval, "expiresAtUtc", _now.AddSeconds(10).ToString("O"));
            return (request, approval);
        }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field).SetValue(target, value);
    }
}
