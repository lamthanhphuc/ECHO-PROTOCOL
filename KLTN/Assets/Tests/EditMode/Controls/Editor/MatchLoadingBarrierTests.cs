using System;
using EchoProtocol.Networking.Loading;
using NUnit.Framework;

namespace EchoProtocol.Tests.EditMode.Controls
{
    public sealed class MatchLoadingBarrierTests
    {
        private const string Attempt = "match-attempt-1";
        private const string Scene = "SciFi";
        private const int Host = 1;
        private const int Client = 2;

        [Test]
        public void HostAndClient_OnlyCompleteAfterLocalReadinessAndHostRelease()
        {
            var host = Begin();
            var client = Begin();
            MakeLocalReady(host);
            host.Acknowledge(Attempt, Scene, Host);
            client.Authorize();
            client.StartScene();
            client.SetSceneProgress(0.42f);

            Assert.That(host.TryRelease(Attempt, Scene, Host), Is.False);
            Assert.That(host.Snapshot.Phase, Is.EqualTo(MatchLoadingPhase.GameplayReadiness));
            Assert.That(host.Snapshot.LocalReady, Is.True);
            Assert.That(host.Snapshot.ReadyPlayers, Is.EqualTo(1));
            Assert.That(client.Snapshot.Phase, Is.EqualTo(MatchLoadingPhase.SceneLoading));
            Assert.That(client.SceneProgress, Is.EqualTo(0.42f));

            client.SceneLoaded();
            client.SetLocalReady();
            host.Acknowledge(Attempt, Scene, Client);
            Assert.That(host.TryRelease(Attempt, Scene, Host), Is.True);

            // The host's ready aggregate is applied only after transport source validation.
            foreach (int player in host.ReadyPlayers)
                client.Acknowledge(Attempt, Scene, player);
            Assert.That(client.TryRelease(Attempt, Scene, Host), Is.True);
            Assert.That(host.Phase, Is.EqualTo(MatchLoadingPhase.Complete));
            Assert.That(client.Phase, Is.EqualTo(MatchLoadingPhase.Complete));
            Assert.That(host.IsVisible, Is.True, "The presenter owns completion fading.");
            host.Dismiss();
            client.Dismiss();
            Assert.That(host.IsVisible || client.IsVisible, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AllPeerAcknowledgments_CannotReleaseBeforeLocalGameplayReady(bool sceneLoaded)
        {
            var barrier = Begin();
            barrier.Authorize();
            barrier.StartScene();
            barrier.SetSceneProgress(1f);
            if (sceneLoaded) barrier.SceneLoaded();
            barrier.Acknowledge(Attempt, Scene, Host);
            barrier.Acknowledge(Attempt, Scene, Client);

            Assert.That(barrier.TryRelease(Attempt, Scene, Host), Is.False);
            Assert.That(barrier.Phase, Is.Not.EqualTo(MatchLoadingPhase.Complete));
            Assert.That(barrier.LocalReady, Is.False);
        }

        [Test]
        public void SceneProgressOne_DoesNotMeanWholeSessionComplete()
        {
            var barrier = Begin();
            MakeLocalReady(barrier);
            barrier.Acknowledge(Attempt, Scene, Host);

            Assert.That(barrier.SceneProgress, Is.EqualTo(1f));
            Assert.That(barrier.ReleaseReceived, Is.False);
            Assert.That(barrier.Phase, Is.EqualTo(MatchLoadingPhase.GameplayReadiness));
            Assert.That(barrier.TryRelease(Attempt, Scene, Host), Is.False);
        }

        [Test]
        public void ClientCannotReleaseSessionEvenWhenEveryoneIsReady()
        {
            var barrier = Begin();
            MakeLocalReady(barrier);
            barrier.Acknowledge(Attempt, Scene, Host);
            barrier.Acknowledge(Attempt, Scene, Client);

            Assert.That(barrier.TryRelease(Attempt, Scene, Client), Is.False);
            Assert.That(barrier.TryRelease(Attempt, Scene, 99), Is.False);
            Assert.That(barrier.TryRelease(Attempt, Scene, Host), Is.True);
            Assert.That(barrier.TryRelease(Attempt, Scene, Host), Is.True,
                "A repeated host release must be harmless.");
        }

        [Test]
        public void ReadyMessages_RejectUnknownStaleAndWrongSceneAndDeduplicateMembers()
        {
            var barrier = Begin();
            Assert.That(barrier.Acknowledge(Attempt, Scene, 99), Is.False);
            Assert.That(barrier.Acknowledge("previous-attempt", Scene, Client), Is.False);
            Assert.That(barrier.Acknowledge(Attempt, "Scene_A", Client), Is.False);
            Assert.That(barrier.Acknowledge(Attempt, Scene, Client), Is.True);
            Assert.That(barrier.Acknowledge(Attempt, Scene, Client), Is.True);

            Assert.That(barrier.ReadyPlayers.Count, Is.EqualTo(1));
            Assert.That(barrier.Snapshot.ReadyPlayers, Is.EqualTo(1));
            Assert.That(barrier.TryRelease("previous-attempt", Scene, Host), Is.False);
        }

        [Test]
        public void FrozenRoster_DoesNotShrinkWhenCallerRosterChangesOrPeerNeverAcknowledges()
        {
            var roster = new System.Collections.Generic.List<int> { Host, Client };
            var barrier = new MatchLoadingBarrier();
            barrier.Begin(Attempt, Scene, Host, roster, 0d);
            roster.Remove(Client);
            MakeLocalReady(barrier);
            barrier.Acknowledge(Attempt, Scene, Host);

            Assert.That(barrier.RequiredPlayers, Is.EquivalentTo(new[] { Host, Client }));
            Assert.That(barrier.TryRelease(Attempt, Scene, Host), Is.False);
        }

        [Test]
        public void BackendAbort_StaysFailedDespiteLateSceneAndReadyMessages()
        {
            var barrier = Begin();
            barrier.Fail("Backend rejected match start.");
            MakeLocalReady(barrier);

            Assert.That(barrier.Acknowledge(Attempt, Scene, Host), Is.False);
            Assert.That(barrier.TryRelease(Attempt, Scene, Host), Is.False);
            Assert.That(barrier.Phase, Is.EqualTo(MatchLoadingPhase.Failed));
            Assert.That(barrier.BackendAuthorized, Is.False);
            Assert.That(barrier.Error, Is.EqualTo("Backend rejected match start."));
        }

        [Test]
        public void FailureAndRetry_ResetReadinessProgressAndIgnoreOldAttempt()
        {
            var barrier = Begin();
            MakeLocalReady(barrier);
            barrier.Acknowledge(Attempt, Scene, Host);
            barrier.Fail("Host disconnected.");
            barrier.Begin("retry", "Scene_A", Client, new[] { Client }, 50d);

            Assert.That(barrier.Error, Is.Empty);
            Assert.That(barrier.BackendAuthorized || barrier.SceneStarted || barrier.LocalSceneLoaded
                || barrier.LocalReady || barrier.ReleaseReceived, Is.False);
            Assert.That(barrier.SceneProgress, Is.EqualTo(-1f));
            Assert.That(barrier.ReadyPlayers, Is.Empty);
            Assert.That(barrier.RequiredPlayers, Is.EquivalentTo(new[] { Client }));
            Assert.That(barrier.StartedAt, Is.EqualTo(50d));
            Assert.That(barrier.Acknowledge(Attempt, Scene, Host), Is.False);
            MakeLocalReady(barrier);
            barrier.Acknowledge("retry", "Scene_A", Client);
            Assert.That(barrier.TryRelease("retry", "Scene_A", Client), Is.True);
        }

        [Test]
        public void Timeout_UsesElapsedAttemptTimeAndStopsAfterFailureOrDismissal()
        {
            var barrier = Begin();
            Assert.That(barrier.HasTimedOut(109.99d, 10d), Is.False);
            Assert.That(barrier.HasTimedOut(110d, 10d), Is.True);
            Assert.That(barrier.HasTimedOut(111d, 0d), Is.False);
            Assert.That(barrier.HasTimedOut(double.NaN, 10d), Is.False);
            barrier.Fail("Loading timed out.");
            Assert.That(barrier.HasTimedOut(120d, 10d), Is.False);
            barrier.Dismiss();
            Assert.That(barrier.IsVisible, Is.False);
        }

        [Test]
        public void DismissedAttempt_CannotBeReactivatedByLateCallbacks()
        {
            var barrier = Begin();
            barrier.Dismiss();
            MakeLocalReady(barrier);
            barrier.Fail("Late transport failure.");

            Assert.That(barrier.Acknowledge(Attempt, Scene, Host), Is.False);
            Assert.That(barrier.TryRelease(Attempt, Scene, Host), Is.False);
            Assert.That(barrier.IsVisible || barrier.BackendAuthorized || barrier.LocalReady, Is.False);
            Assert.That(barrier.Error, Is.Empty);
        }

        [Test]
        public void SceneProgress_ReportsRawMeasurementsAndNeverAdvancesFromTime()
        {
            var barrier = Begin();
            Assert.That(barrier.SceneProgress, Is.EqualTo(-1f));
            barrier.Authorize();
            Assert.That(barrier.Phase, Is.EqualTo(MatchLoadingPhase.NetworkSynchronization));
            barrier.StartScene();
            barrier.SetSceneProgress(0.3f);
            barrier.HasTimedOut(105d, 10d);
            Assert.That(barrier.SceneProgress, Is.EqualTo(0.3f));
            barrier.SetSceneProgress(float.NaN);
            Assert.That(barrier.SceneProgress, Is.EqualTo(0.3f));
            barrier.SetSceneProgress(-1f);
            Assert.That(barrier.SceneProgress, Is.EqualTo(-1f));
            barrier.SceneLoaded();
            barrier.SetSceneProgress(0.5f);
            Assert.That(barrier.SceneProgress, Is.EqualTo(1f));
        }

        [Test]
        public void BeginRejectsInvalidRosterWithoutCorruptingExistingAttempt()
        {
            var barrier = Begin();
            Assert.Throws<ArgumentException>(() => barrier.Begin("bad", Scene, Host,
                new[] { Client }, 1d));
            Assert.That(barrier.AttemptId, Is.EqualTo(Attempt));
            Assert.That(barrier.RequiredPlayers.Count, Is.EqualTo(2));
        }

        private static MatchLoadingBarrier Begin()
        {
            var barrier = new MatchLoadingBarrier();
            barrier.Begin(Attempt, Scene, Host, new[] { Host, Client }, 100d);
            return barrier;
        }

        private static void MakeLocalReady(MatchLoadingBarrier barrier)
        {
            barrier.Authorize();
            barrier.StartScene();
            barrier.SceneLoaded();
            barrier.SetLocalReady();
        }
    }
}
