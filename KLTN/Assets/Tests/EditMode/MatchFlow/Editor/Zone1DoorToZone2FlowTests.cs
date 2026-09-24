using EchoProtocol.Networking;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests.MatchFlow
{
    [TestFixture]
    public sealed class Zone1DoorToZone2FlowTests
    {
        private GameObject _doorRoot;
        private GameObject _blocker;
        private MatchFlowController _flow;

        [SetUp]
        public void SetUp()
        {
            _doorRoot = new GameObject("DoorToZone2");
            _blocker = new GameObject("LP_Bay_Door_snaps");
            _blocker.transform.SetParent(_doorRoot.transform);
            _blocker.AddComponent<BoxCollider>();

            _flow = new GameObject("MatchFlow").AddComponent<MatchFlowController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_flow != null)
            {
                Object.DestroyImmediate(_flow.gameObject);
            }

            if (_doorRoot != null)
            {
                Object.DestroyImmediate(_doorRoot);
            }
        }

        [Test]
        public void Zone1CoreCompletion_Hides_DoorToZone2_LPBayDoorSnaps()
        {
            Assert.IsTrue(_blocker.activeSelf);
            Assert.IsTrue(_blocker.GetComponent<BoxCollider>().enabled);

            _flow.NotifyCoreObjectiveComplete();

            Assert.IsFalse(_blocker.activeSelf);
            Assert.IsFalse(_blocker.GetComponent<BoxCollider>().enabled);
        }

        [Test]
        public void NetworkZone2PhasePresentation_Hides_DoorToZone2_LPBayDoorSnaps()
        {
            _flow.ApplyAuthoritativeSnapshot(
                NetworkMatchPhase.Zone2Objective,
                NetworkMatchStatus.Running,
                NetworkMatchResult.None);

            Assert.IsFalse(_blocker.activeSelf);
            Assert.IsFalse(_blocker.GetComponent<BoxCollider>().enabled);
        }
    }
}
