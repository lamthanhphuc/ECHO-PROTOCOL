using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDv2BoundaryCoordinatorTests
    {
        private static object _approval;
        private static string _order;

        [Test]
        public async Task ApprovedPlanAppliesBeforeTransitionAndReceipt()
        {
            var coordinatorType = RuntimeType("AEDv2BoundaryCoordinator");
            var transactionType = RuntimeType("AEDv2BoundaryTransaction");
            var approvalType = RuntimeType("AEDPlanV2Data");
            var coordinator = Activator.CreateInstance(coordinatorType);
            var transaction = CreateTransaction(transactionType, approvalType);
            _order = string.Empty;
            var submitMethod = GetType().GetMethod(nameof(Submit),
                BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(approvalType);
            var submit = Delegate.CreateDelegate(
                typeof(Func<,>).MakeGenericType(typeof(CancellationToken),
                    typeof(Task<>).MakeGenericType(approvalType)), submitMethod);
            var started = (bool)coordinatorType.GetMethod("TryBegin").Invoke(coordinator,
                new object[] { transaction, submit,
                    MakeDelegate(nameof(Validate), approvalType),
                    MakeDelegate(nameof(Apply), approvalType),
                    new Action(Transition),
                    new Func<CancellationToken, Task<bool>>(Receipt),
                    null, null });
            Assert.That(started, Is.True);

            for (var i = 0; i < 20 && coordinatorType.GetProperty("State")
                     .GetValue(coordinator).ToString() != "Idle"; i++)
                await Task.Delay(10);

            Assert.That(_order, Is.EqualTo("apply|transition|receipt"));
            Assert.That(coordinatorType.GetProperty("State").GetValue(coordinator).ToString(),
                Is.EqualTo("Idle"));
            ((IDisposable)coordinator).Dispose();
        }

        private static Type RuntimeType(string name) => Type.GetType(
            $"EchoProtocol.AI.AED.{name}, Assembly-CSharp", true);

        private static object CreateTransaction(Type transactionType, Type approvalType)
        {
            var matchId = Guid.NewGuid();
            var decisionId = Guid.NewGuid();
            var plan = AEDv2Plan.Normal().With(AEDv2Key.ReviveBonusPerZone, 1);
            var request = Activator.CreateInstance(RuntimeType("AEDPlanV2Request"));
            Set(request, "decisionId", decisionId.ToString("D"));
            Set(request, "phaseOrdinal", 2);
            Set(request, "decisionPoint", "ALLOWED_PHASE_BOUNDARY");
            Set(request, "previousPlanFingerprint", AEDv2Plan.Normal().Fingerprint());
            Set(request, "resultingPlanFingerprint", plan.Fingerprint());
            Set(request, "evidenceFingerprint", new string('e', 64));
            Set(request, "rosterIdentity", new string('r', 64));
            Set(request, "commitStatus", "COMMITTED");
            _approval = Activator.CreateInstance(approvalType);
            foreach (var pair in new[]
            {
                ("matchId", (object)matchId.ToString("D")),
                ("decisionId", decisionId.ToString("D")),
                ("phaseOrdinal", 2),
                ("decisionPoint", "ALLOWED_PHASE_BOUNDARY"),
                ("previousPlanFingerprint", AEDv2Plan.Normal().Fingerprint()),
                ("resultingPlanFingerprint", plan.Fingerprint()),
                ("evidenceFingerprint", new string('e', 64)),
                ("rosterIdentity", new string('r', 64)),
                ("commitStatus", "COMMITTED"),
                ("applyStatus", "PENDING")
            }) Set(_approval, pair.Item1, pair.Item2);
            return Activator.CreateInstance(transactionType, matchId, decisionId,
                "A", "B", 2u, new string('r', 64), new string('e', 64),
                AEDv2Plan.Normal().Fingerprint(), plan,
                AEDv2Key.ReviveBonusPerZone, request);
        }

        private static Delegate MakeDelegate(string method, Type argument) =>
            Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(argument, typeof(bool)),
                typeof(AEDv2BoundaryCoordinatorTests).GetMethod(method,
                    BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(argument));
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field).SetValue(target, value);
        private static Task<T> Submit<T>(CancellationToken _) => Task.FromResult((T)_approval);
        private static bool Validate<T>(T _) => true;
        private static bool Apply<T>(T _) { _order += "apply|"; return true; }
        private static void Transition() => _order += "transition|";
        private static Task<bool> Receipt(CancellationToken _) { _order += "receipt"; return Task.FromResult(true); }
    }
}
