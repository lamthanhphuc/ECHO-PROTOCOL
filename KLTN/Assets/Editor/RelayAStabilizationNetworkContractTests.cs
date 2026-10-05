using System.IO;
using NUnit.Framework;

namespace EchoProtocol.Tests
{
    // Source contracts supplement, but do not replace, Fusion host/client playtests.
    public sealed class RelayAStabilizationNetworkContractTests
    {
        private string _match;
        [SetUp]
        public void SetUp()
        {
            string path = "Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs";
            if (!File.Exists(path)) path = "KLTN/" + path;
            _match = File.ReadAllText(path).Replace("\r\n", "\n");
        }
        [TearDown] public void TearDown() { }

        [Test]
        public void StabilizationRequests_UseStateAuthorityRpc()
        {
            StringAssert.Contains("[Rpc(RpcSources.All, RpcTargets.StateAuthority)]\n        private void RpcRelayAStabilization", _match);
            StringAssert.Contains("TryRelayAStabilizationAuthoritative(info.Source, (RelaySlot)relaySlot, controls, action)", _match);
        }

        [Test]
        public void StabilizationMutations_RequireValidatedActiveOperator()
        {
            int start = _match.IndexOf("private bool TryRelayAStabilizationAuthoritative");
            int end = _match.IndexOf("private bool TryApplyRelayBControlsAuthoritative", start);
            string method = _match.Substring(start, end - start);
            StringAssert.Contains("!TryValidateRelayCommand(requester, slot, out var target)", method);
            StringAssert.Contains("GetRelayOperator(slot) != requester", method);
            StringAssert.Contains("controller.SetControls", method);
            StringAssert.Contains("controller.StartStabilization()", method);
            StringAssert.Contains("controller.EmergencyStop()", method);
        }

        [Test]
        public void ExistingCircuitRequests_AndFinalTelemetryRemainReplicated()
        {
            StringAssert.Contains("private void RpcRelayARotate", _match);
            StringAssert.Contains("private void RpcRelayATest", _match);
            StringAssert.Contains("[Networked] public RelayAStabilizationTelemetry RelayA1Stabilization", _match);
            StringAssert.Contains("[Networked] public RelayAStabilizationTelemetry RelayA2Stabilization", _match);
            StringAssert.Contains("Progress = stabilization.StabilitySeconds", _match);
            StringAssert.Contains("ActiveFault = (byte)stabilization.ActiveFault", _match);
        }
    }
}
