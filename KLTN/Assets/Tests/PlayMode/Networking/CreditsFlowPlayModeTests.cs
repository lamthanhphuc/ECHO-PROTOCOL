#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace EchoProtocol.Networking.Tests
{
    public sealed class CreditsFlowPlayModeTests
    {
        private object _fixture;

        [UnityTest]
        public IEnumerator VerifiedResultPlaysLocalCreditsAndPreservesLobbyAfterSharedHandoff()
            => StartFixture("ResultCreditsLobby");

        [UnityTest]
        [Timeout(185000)]
        public IEnumerator DirectPreviewCompletesTheFullUnscaledTimeline()
            => StartFixture("DirectPreviewPlaysFullTimelineWithoutNetworkOrAuthentication");

        [UnityTest]
        public IEnumerator UnconfirmedRewardRemainsNavigable()
            => StartFixture("UnconfirmedRewardReturnsToLobbyWithoutLoadingCredits");

        [UnityTest]
        public IEnumerator AutoPlayCanRecoverFromMalformedCredits()
            => StartFixture("AutoPlayWithMissingCreditsContentFallsBackToLobby");

        private IEnumerator StartFixture(string name)
        {
            _fixture = Activator.CreateInstance(Type.GetType(
                "EchoProtocol.Tests.EditMode.Controls.CreditsLocalFlowSmoke, Assembly-CSharp-Editor", true));
            return Run(name);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_fixture != null) yield return Run("Cleanup");
            _fixture = null;
        }

        private IEnumerator Run(string name) => (IEnumerator)_fixture.GetType()
            .GetMethod(name, BindingFlags.Public | BindingFlags.Instance).Invoke(_fixture, null);
    }
}
#endif
