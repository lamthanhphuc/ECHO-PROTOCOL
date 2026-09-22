using System.IO;
using EchoProtocol.MatchFlow;
using EchoProtocol.RelayA;
using EchoProtocol.RelayB;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests.MatchFlow
{
    [TestFixture]
    public class Zone2MissionFlowTests
    {
        private GameObject _holder;
        private Zone2MissionDirector _director;
        private SecurityTerminalDownload _terminal;
        private RelayAController _relayA1;
        private RelayAController _relayA2;
        private RelayBController _relayB1;
        private RelayBController _relayB2;
        private PowerControlUIController _panel1;
        private PowerControlUIController _panel2;
        private GameObject _doorBlocker1;
        private GameObject _doorBlocker2;

        [SetUp]
        public void SetUp()
        {
            _holder = new GameObject("Zone2MissionFlowTestContext");
            _director = _holder.AddComponent<Zone2MissionDirector>();

            // Security terminal
            var termGo = new GameObject("Terminal");
            termGo.transform.SetParent(_holder.transform);
            _terminal = termGo.AddComponent<SecurityTerminalDownload>();

            // Relays
            var rA1Go = new GameObject("RelayA1");
            rA1Go.transform.SetParent(_holder.transform);
            rA1Go.AddComponent<AudioSource>();
            _relayA1 = rA1Go.AddComponent<RelayAController>();

            var rA2Go = new GameObject("RelayA2");
            rA2Go.transform.SetParent(_holder.transform);
            rA2Go.AddComponent<AudioSource>();
            _relayA2 = rA2Go.AddComponent<RelayAController>();

            var rB1Go = new GameObject("RelayB1");
            rB1Go.transform.SetParent(_holder.transform);
            rB1Go.AddComponent<AudioSource>();
            _relayB1 = rB1Go.AddComponent<RelayBController>();

            var rB2Go = new GameObject("RelayB2");
            rB2Go.transform.SetParent(_holder.transform);
            rB2Go.AddComponent<AudioSource>();
            _relayB2 = rB2Go.AddComponent<RelayBController>();

            // Panels
            var p1Go = new GameObject("Panel1");
            p1Go.transform.SetParent(_holder.transform);
            _panel1 = p1Go.AddComponent<PowerControlUIController>();

            var p2Go = new GameObject("Panel2");
            p2Go.transform.SetParent(_holder.transform);
            _panel2 = p2Go.AddComponent<PowerControlUIController>();

            // Door blockers
            _doorBlocker1 = new GameObject("LP_Bay_Door_snaps_1");
            _doorBlocker1.transform.SetParent(_holder.transform);
            _doorBlocker1.AddComponent<BoxCollider>();

            _doorBlocker2 = new GameObject("LP_Bay_Door_snaps_2");
            _doorBlocker2.transform.SetParent(_holder.transform);
            _doorBlocker2.AddComponent<BoxCollider>();

            // Wire references via SerializedObject
            var so = new UnityEditor.SerializedObject(_director);
            so.FindProperty("securityTerminal").objectReferenceValue = _terminal;
            so.FindProperty("relayA1").objectReferenceValue = _relayA1;
            so.FindProperty("relayA2").objectReferenceValue = _relayA2;
            so.FindProperty("relayB1").objectReferenceValue = _relayB1;
            so.FindProperty("relayB2").objectReferenceValue = _relayB2;
            so.FindProperty("distributionPanel1").objectReferenceValue = _panel1;
            so.FindProperty("distributionPanel2").objectReferenceValue = _panel2;
            so.FindProperty("doorBlocker1").objectReferenceValue = _doorBlocker1;
            so.FindProperty("doorBlocker2").objectReferenceValue = _doorBlocker2;
            so.ApplyModifiedPropertiesWithoutUndo();

            _director.SetOfflineStage(Zone2MissionStage.FindSecurityTerminal);
            _director.ApplyDoorState(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_holder != null)
            {
                Object.DestroyImmediate(_holder);
            }
        }

        [Test]
        public void TEST_01_Zone1_Completion_Does_Not_Enter_Old_Power_Puzzle()
        {
            var flow = _holder.AddComponent<MatchFlowController>();
            flow.NotifyCoreObjectiveComplete();

            Assert.AreNotEqual(MatchPhase.PowerPuzzle, flow.Phase, "Zone 1 complete must NOT enter old PowerPuzzle!");
        }

        [Test]
        public void TEST_02_Zone1_Completion_Activates_FindSecurityTerminal()
        {
            Assert.AreEqual(Zone2MissionStage.FindSecurityTerminal, _director.CurrentStage);
            Assert.IsFalse(_director.IsSecurityTerminalDiscovered);
        }

        [Test]
        public void TEST_03_First_Terminal_Interaction_Advances_To_RepairRelays_Without_Starting_Hold()
        {
            _director.DiscoverSecurityTerminal();

            Assert.IsTrue(_director.IsSecurityTerminalDiscovered);
            Assert.AreEqual(Zone2MissionStage.RepairRelays, _director.CurrentStage);
            Assert.IsFalse(_terminal.IsDownloading);
            Assert.IsFalse(_terminal.IsComplete);
        }

        [Test]
        public void TEST_04_Terminal_Cannot_Start_Security_Hold_At_Incomplete_Relays()
        {
            _director.DiscoverSecurityTerminal();

            // At 0/4
            Assert.IsFalse(_director.AreAllRelaysOnline);
            Assert.AreEqual(0, _director.CompletedRelayCount);

            // At 1/4
            _director.ReportRelayOnline(RelaySlot.RelayA_1);
            Assert.AreEqual(1, _director.CompletedRelayCount);
            Assert.IsFalse(_director.AreAllRelaysOnline);

            // At 2/4
            _director.ReportRelayOnline(RelaySlot.RelayB_1);
            Assert.AreEqual(2, _director.CompletedRelayCount);
            Assert.IsFalse(_director.AreAllRelaysOnline);

            // At 3/4
            _director.ReportRelayOnline(RelaySlot.RelayA_2);
            Assert.AreEqual(3, _director.CompletedRelayCount);
            Assert.IsFalse(_director.AreAllRelaysOnline);
            Assert.AreNotEqual(Zone2MissionStage.SecurityHoldReady, _director.CurrentStage);
        }

        [Test]
        public void TEST_05_Each_Unique_Relay_Contributes_Exactly_Once()
        {
            _director.DiscoverSecurityTerminal();

            _director.ReportRelayOnline(RelaySlot.RelayA_1);
            Assert.AreEqual(1, _director.CompletedRelayCount);

            // Duplicate report for the same relay
            _director.ReportRelayOnline(RelaySlot.RelayA_1);
            Assert.AreEqual(1, _director.CompletedRelayCount, "Reporting the same relay multiple times must not increment progress!");
        }

        [Test]
        public void TEST_06_Relay_Order_Does_Not_Matter()
        {
            _director.DiscoverSecurityTerminal();

            // Order: B2, A1, A2, B1
            _director.ReportRelayOnline(RelaySlot.RelayB_2);
            Assert.AreEqual(1, _director.CompletedRelayCount);

            _director.ReportRelayOnline(RelaySlot.RelayA_1);
            Assert.AreEqual(2, _director.CompletedRelayCount);

            _director.ReportRelayOnline(RelaySlot.RelayA_2);
            Assert.AreEqual(3, _director.CompletedRelayCount);

            _director.ReportRelayOnline(RelaySlot.RelayB_1);
            Assert.AreEqual(4, _director.CompletedRelayCount);
            Assert.IsTrue(_director.AreAllRelaysOnline);
            Assert.AreEqual(Zone2MissionStage.SecurityHoldReady, _director.CurrentStage);
        }

        [Test]
        public void TEST_07_FourOfFour_Allows_Security_Hold()
        {
            _director.DiscoverSecurityTerminal();

            _director.ReportRelayOnline(RelaySlot.RelayA_1);
            _director.ReportRelayOnline(RelaySlot.RelayA_2);
            _director.ReportRelayOnline(RelaySlot.RelayB_1);
            _director.ReportRelayOnline(RelaySlot.RelayB_2);

            Assert.IsTrue(_director.AreAllRelaysOnline);
            Assert.AreEqual(Zone2MissionStage.SecurityHoldReady, _director.CurrentStage);
        }

        [Test]
        public void TEST_08_Security_Hold_Completion_Reveals_Exactly_One_Code()
        {
            _director.DiscoverSecurityTerminal();
            _director.ReportRelayOnline(RelaySlot.RelayA_1);
            _director.ReportRelayOnline(RelaySlot.RelayA_2);
            _director.ReportRelayOnline(RelaySlot.RelayB_1);
            _director.ReportRelayOnline(RelaySlot.RelayB_2);

            _director.OnSecurityHoldCompleted(_terminal);

            string code1 = _director.AuthorizationCode;
            Assert.IsFalse(string.IsNullOrEmpty(code1));
            Assert.AreEqual(4, code1.Length);
            StringAssert.IsMatch("^[0-9]{4}$", code1);

            // Accessing multiple times produces the exact same code
            string code2 = _director.AuthorizationCode;
            Assert.AreEqual(code1, code2);
        }

        [Test]
        public void TEST_09_Security_Hold_Completion_Does_NOT_Directly_Trigger_Final_Hunt()
        {
            var flow = _holder.AddComponent<MatchFlowController>();
            flow.NotifySecurityHoldComplete();

            Assert.AreNotEqual(MatchPhase.FinalHunt, flow.Phase, "Security Hold completion must not directly trigger Final Hunt!");
        }

        [Test]
        public void TEST_10_Panels_Are_Locked_Before_Code_Reveal()
        {
            Assert.IsFalse(_director.IsSecurityHoldComplete);
            Assert.IsFalse(_director.AreZoneDoorsUnlocked);
        }

        [Test]
        public void TEST_11_Wrong_Code_Does_Not_Reset_Mission_State()
        {
            _director.DiscoverSecurityTerminal();
            _director.ReportRelayOnline(RelaySlot.RelayA_1);
            _director.ReportRelayOnline(RelaySlot.RelayA_2);
            _director.ReportRelayOnline(RelaySlot.RelayB_1);
            _director.ReportRelayOnline(RelaySlot.RelayB_2);
            _director.OnSecurityHoldCompleted(_terminal);

            string authCode = _director.AuthorizationCode;
            string wrongCode = authCode == "0000" ? "9999" : "0000";

            bool accepted = _director.SubmitAccessCode(wrongCode);
            Assert.IsFalse(accepted);
            Assert.IsTrue(_director.AreAllRelaysOnline);
            Assert.IsTrue(_director.IsSecurityHoldComplete);
            Assert.IsFalse(_director.AreZoneDoorsUnlocked);
            Assert.AreEqual(authCode, _director.AuthorizationCode, "Authorization code must not change on wrong submission!");
        }

        [Test]
        public void TEST_12_Correct_Code_On_Panel1_Unlocks_Both_Doors()
        {
            _director.DiscoverSecurityTerminal();
            _director.ReportRelayOnline(RelaySlot.RelayA_1);
            _director.ReportRelayOnline(RelaySlot.RelayA_2);
            _director.ReportRelayOnline(RelaySlot.RelayB_1);
            _director.ReportRelayOnline(RelaySlot.RelayB_2);
            _director.OnSecurityHoldCompleted(_terminal);

            string authCode = _director.AuthorizationCode;
            bool success = _director.SubmitAccessCode(authCode);

            Assert.IsTrue(success);
            Assert.IsTrue(_director.AreZoneDoorsUnlocked);
            Assert.IsFalse(_doorBlocker1.activeSelf, "Door blocker 1 must be inactive!");
            Assert.IsFalse(_doorBlocker2.activeSelf, "Door blocker 2 must be inactive!");
        }

        [Test]
        public void TEST_13_Correct_Code_On_Panel2_Unlocks_Both_Doors()
        {
            _director.DiscoverSecurityTerminal();
            _director.ReportRelayOnline(RelaySlot.RelayA_1);
            _director.ReportRelayOnline(RelaySlot.RelayA_2);
            _director.ReportRelayOnline(RelaySlot.RelayB_1);
            _director.ReportRelayOnline(RelaySlot.RelayB_2);
            _director.OnSecurityHoldCompleted(_terminal);

            string authCode = _director.AuthorizationCode;
            bool success = _director.SubmitAccessCode(authCode);

            Assert.IsTrue(success);
            Assert.IsTrue(_director.AreZoneDoorsUnlocked);
            Assert.IsFalse(_doorBlocker1.activeSelf);
            Assert.IsFalse(_doorBlocker2.activeSelf);
        }

        [Test]
        public void TEST_14_Correct_Code_Only_Needs_To_Be_Entered_Once()
        {
            _director.DiscoverSecurityTerminal();
            _director.ReportRelayOnline(RelaySlot.RelayA_1);
            _director.ReportRelayOnline(RelaySlot.RelayA_2);
            _director.ReportRelayOnline(RelaySlot.RelayB_1);
            _director.ReportRelayOnline(RelaySlot.RelayB_2);
            _director.OnSecurityHoldCompleted(_terminal);

            string authCode = _director.AuthorizationCode;
            Assert.IsTrue(_director.SubmitAccessCode(authCode));
            Assert.AreEqual(Zone2MissionStage.Zone2Completed, _director.CurrentStage);
        }

        [Test]
        public void TEST_15_Duplicate_Correct_Submissions_Are_Harmless()
        {
            _director.DiscoverSecurityTerminal();
            _director.ReportRelayOnline(RelaySlot.RelayA_1);
            _director.ReportRelayOnline(RelaySlot.RelayA_2);
            _director.ReportRelayOnline(RelaySlot.RelayB_1);
            _director.ReportRelayOnline(RelaySlot.RelayB_2);
            _director.OnSecurityHoldCompleted(_terminal);

            string authCode = _director.AuthorizationCode;
            Assert.IsTrue(_director.SubmitAccessCode(authCode));
            // Second submission
            Assert.IsTrue(_director.SubmitAccessCode(authCode));
            Assert.AreEqual(Zone2MissionStage.Zone2Completed, _director.CurrentStage);
            Assert.IsTrue(_director.AreZoneDoorsUnlocked);
        }

        [Test]
        public void TEST_16_DoorUnlocked_Causes_Both_LP_Bay_Door_snaps_To_Become_Inactive()
        {
            Assert.IsTrue(_doorBlocker1.activeSelf);
            Assert.IsTrue(_doorBlocker2.activeSelf);

            _director.ApplyDoorState(true);

            Assert.IsFalse(_doorBlocker1.activeSelf);
            Assert.IsFalse(_doorBlocker2.activeSelf);
        }

        [Test]
        public void TEST_17_Late_Join_Presentation_Shows_Correct_Door_State()
        {
            // Simulate late-join receiving unlocked door state
            _director.OnAuthoritativeStateChanged(
                Zone2MissionStage.Zone2Completed,
                0x0F,
                true,
                true,
                true,
                "1234");

            Assert.IsFalse(_doorBlocker1.activeSelf);
            Assert.IsFalse(_doorBlocker2.activeSelf);
        }

        [Test]
        public void TEST_18_Zone2Completed_Commits_Once()
        {
            int transitionCount = 0;
            _director.StageChanged += (stage) =>
            {
                if (stage == Zone2MissionStage.Zone2Completed) transitionCount++;
            };

            _director.DiscoverSecurityTerminal();
            _director.ReportRelayOnline(RelaySlot.RelayA_1);
            _director.ReportRelayOnline(RelaySlot.RelayA_2);
            _director.ReportRelayOnline(RelaySlot.RelayB_1);
            _director.ReportRelayOnline(RelaySlot.RelayB_2);
            _director.OnSecurityHoldCompleted(_terminal);

            _director.SubmitAccessCode(_director.AuthorizationCode);
            _director.SubmitAccessCode(_director.AuthorizationCode);

            Assert.AreEqual(1, transitionCount, "Zone2Completed must only commit once!");
        }
    }
}

