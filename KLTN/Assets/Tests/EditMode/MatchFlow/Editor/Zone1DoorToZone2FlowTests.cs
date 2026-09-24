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
        private BoxCollider _passageCollider;
        private BoxCollider _wallEdgeCollider;
        private MatchFlowController _flow;

        [SetUp]
        public void SetUp()
        {
            _doorRoot = new GameObject("DoorToZone2");
            _blocker = new GameObject("LP_Bay_Door_snaps");
            _blocker.transform.SetParent(_doorRoot.transform);
            _blocker.AddComponent<BoxCollider>();

            var wall = new GameObject("LP_Bay_Door_Wall_snaps");
            wall.transform.SetParent(_doorRoot.transform);
            _passageCollider = wall.AddComponent<BoxCollider>();
            _passageCollider.size = new Vector3(0.3f, 9f, 6f);
            _wallEdgeCollider = wall.AddComponent<BoxCollider>();
            _wallEdgeCollider.size = new Vector3(1.03f, 0.87f, 0.19f);

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
            Assert.IsFalse(_passageCollider.enabled);
            Assert.IsTrue(_wallEdgeCollider.enabled);
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
            Assert.IsFalse(_passageCollider.enabled);
            Assert.IsTrue(_wallEdgeCollider.enabled);

            _flow.ApplyAuthoritativeSnapshot(
                NetworkMatchPhase.CoreObjective,
                NetworkMatchStatus.Running,
                NetworkMatchResult.None);

            Assert.IsTrue(_blocker.activeSelf);
            Assert.IsTrue(_passageCollider.enabled);
            Assert.IsTrue(_wallEdgeCollider.enabled);
        }
    }
}
