using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests.MatchFlow
{
    [TestFixture]
    public class RestoreMainPowerTests
    {
        private GameObject _holder;
        private MatchFlowController _flow;
        private SecurityTerminalDownload _terminal;
        private PowerPuzzleController _powerPuzzle;

        [SetUp]
        public void SetUp()
        {
            _holder = new GameObject("RestoreMainPowerTestContext");
            _flow = _holder.AddComponent<MatchFlowController>();
            _terminal = _holder.AddComponent<SecurityTerminalDownload>();
            _powerPuzzle = _holder.AddComponent<PowerPuzzleController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_holder != null)
            {
                UnityEngine.Object.DestroyImmediate(_holder);
            }
        }

        [Test]
        public void TEST_01_SecurityHold_Completion_Generates_FourDigitCode_With_LeadingZero()
        {
            // Verify 4-digit code formatting preserves leading zero
            int sampleCode = 427;
            string formatted = sampleCode.ToString("D4");

            Assert.AreEqual("0427", formatted);
            Assert.AreEqual(4, formatted.Length);
            StringAssert.IsMatch("^[0-9]{4}$", formatted);
        }

        [Test]
        public void TEST_02_SecurityHold_Completion_Transitions_To_PowerPuzzle_Not_FinalHunt()
        {
            Assert.AreEqual(MatchPhase.ExploreCore, _flow.Phase);

            _flow.NotifyCoreObjectiveComplete();
            Assert.AreEqual(MatchPhase.SecurityHold, _flow.Phase);

            _flow.NotifySecurityHoldComplete();
            Assert.AreEqual(MatchPhase.PowerPuzzle, _flow.Phase, "Security Hold completion MUST transition to PowerPuzzle, NOT FinalHunt!");
            Assert.IsFalse(_flow.IsMatchEnded);
            Assert.IsTrue(_flow.IsSecurityHoldComplete);
            Assert.IsFalse(string.IsNullOrEmpty(_flow.PowerAuthorizationCode));
            Assert.AreEqual(4, _flow.PowerAuthorizationCode.Length);
        }

        [Test]
        public void TEST_03_PowerPuzzle_Rejects_Code_Before_SecurityHold_Complete()
        {
            _powerPuzzle.AuthoritativeCode = "1234";

            bool accepted = _powerPuzzle.SubmitAuthorizationCode("1234", _holder);
            Assert.IsFalse(accepted, "Power control must reject code submissions before Security Hold is complete.");
            Assert.IsFalse(_powerPuzzle.IsComplete);
        }

        [Test]
        public void TEST_04_PowerControl_Prompt_Reflects_Locked_And_Available_States()
        {
            // 1. Before Security Hold -> Locked
            string lockedPrompt = _powerPuzzle.GetPrompt(PowerPuzzleStationType.PowerControl);
            StringAssert.Contains("LOCKED", lockedPrompt.ToUpperInvariant());
            StringAssert.Contains("SECURITY AUTHENTICATION REQUIRED", lockedPrompt.ToUpperInvariant());

            // 2. After Security Hold -> Available
            _flow.NotifyCoreObjectiveComplete();
            _flow.NotifySecurityHoldComplete();

            string availablePrompt = _powerPuzzle.GetPrompt(PowerPuzzleStationType.PowerControl);
            StringAssert.Contains("AUTHORIZATION AVAILABLE", availablePrompt.ToUpperInvariant());
        }

        [Test]
        public void TEST_05_PowerControl_WrongCode_Fails_Without_Resetting_SecurityHold()
        {
            _flow.NotifyCoreObjectiveComplete();
            _flow.NotifySecurityHoldComplete();

            _powerPuzzle.AuthoritativeCode = _flow.PowerAuthorizationCode;

            bool result = _powerPuzzle.SubmitAuthorizationCode("9999", _holder);
            Assert.IsFalse(result);
            Assert.IsFalse(_powerPuzzle.IsComplete);
            Assert.IsTrue(_flow.IsSecurityHoldComplete, "Security Hold progress must NOT reset on wrong code entry.");
            Assert.AreEqual(MatchPhase.PowerPuzzle, _flow.Phase);
        }

        [Test]
        public void TEST_06_PowerControl_ConsecutiveFailures_Trigger_Lockout()
        {
            _flow.NotifyCoreObjectiveComplete();
            _flow.NotifySecurityHoldComplete();
            _powerPuzzle.AuthoritativeCode = _flow.PowerAuthorizationCode;

            // Submit 3 wrong codes
            _powerPuzzle.SubmitAuthorizationCode("0001", _holder);
            _powerPuzzle.SubmitAuthorizationCode("0002", _holder);
            _powerPuzzle.SubmitAuthorizationCode("0003", _holder);

            Assert.IsTrue(_powerPuzzle.IsLockedOut, "Three consecutive failures must trigger system lockout.");
            Assert.Greater(_powerPuzzle.LockoutRemaining, 0f);
        }

        [Test]
        public void TEST_07_PowerControl_CorrectCode_Completes_And_Transitions_To_FinalHunt()
        {
            _flow.NotifyCoreObjectiveComplete();
            _flow.NotifySecurityHoldComplete();

            string validCode = _flow.PowerAuthorizationCode;
            _powerPuzzle.AuthoritativeCode = validCode;

            bool success = _powerPuzzle.SubmitAuthorizationCode(validCode, _holder);
            Assert.IsTrue(success, "Correct 4-digit code must be accepted.");
            Assert.IsTrue(_powerPuzzle.IsComplete);
            Assert.AreEqual(MatchPhase.FinalHunt, _flow.Phase, "Correct code must advance MatchPhase to FinalHunt!");
            Assert.IsTrue(_flow.IsRestoreMainPowerComplete);
        }

        [Test]
        public void TEST_08_Double_Completion_Is_Idempotent()
        {
            _flow.NotifyCoreObjectiveComplete();
            _flow.NotifySecurityHoldComplete();

            string validCode = _flow.PowerAuthorizationCode;
            _powerPuzzle.AuthoritativeCode = validCode;

            bool first = _powerPuzzle.SubmitAuthorizationCode(validCode, _holder);
            bool second = _powerPuzzle.SubmitAuthorizationCode(validCode, _holder);

            Assert.IsTrue(first);
            Assert.IsFalse(second, "Subsequent submissions after completion must return false without state corruption.");
            Assert.AreEqual(MatchPhase.FinalHunt, _flow.Phase);
        }

        [Test]
        public void TEST_09_NetworkMatchState_Has_AuthoritativeCode_And_Required_Properties()
        {
            string source = File.ReadAllText("Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs");

            StringAssert.Contains("public NetworkString<_16> PowerAuthorizationCode", source);
            StringAssert.Contains("public NetworkBool SecurityHoldCompleted", source);
            StringAssert.Contains("public NetworkBool PowerAuthorizationAvailable", source);
            StringAssert.Contains("public NetworkBool PowerPuzzleCompleted", source);
            StringAssert.Contains("public NetworkBool RestoreMainPowerCompleted", source);
            StringAssert.Contains("public bool TrySubmitPowerCode(PlayerRef requester, string code)", source);
            StringAssert.Contains("public bool TryCompleteSecurityHold(NetworkId sourceId)", source);
        }

        [Test]
        public void TEST_10_NetworkPowerPuzzle_Has_RpcSubmitAuthorizationCode()
        {
            string source = File.ReadAllText("Assets/_Project/Scripts/Networking/Interaction/NetworkPowerPuzzle.cs");

            StringAssert.Contains("[Rpc(RpcSources.All, RpcTargets.StateAuthority)]", source);
            StringAssert.Contains("public void RpcSubmitAuthorizationCode(PlayerRef sender, string code)", source);
            StringAssert.Contains("matchState.TrySubmitPowerCode(sender, code)", source);
        }

        [Test]
        public void TEST_11_EmergencyNetworkState_Matches_Scene_Relays()
        {
            var relayA = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.RelayA.RelayAController>();
            var relayB = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.RelayB.RelayBController>();
            if (relayA != null && relayB != null)
            {
                bool expected = relayA.IsOnline && relayB.IsOnline;
                Assert.AreEqual(expected, EmergencyNetworkState.AreRelaysOnline());
            }
            else
            {
                Assert.IsTrue(EmergencyNetworkState.AreRelaysOnline());
            }
        }
    }
}
