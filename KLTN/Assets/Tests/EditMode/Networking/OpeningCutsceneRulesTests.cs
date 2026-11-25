using EchoProtocol.Networking.Loading;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class OpeningCutsceneRulesTests
    {
        [TestCase(OpeningCutsceneState.NotStarted, false)]
        [TestCase(OpeningCutsceneState.Playing, false)]
        [TestCase(OpeningCutsceneState.Completed, true)]
        public void ConfiguredMapGameplayAndTimerStayGatedUntilCompletion(OpeningCutsceneState state, bool released) =>
            Assert.That(OpeningCutsceneRules.GameplayReleased(true, state), Is.EqualTo(released));

        [TestCase(OpeningCutsceneState.NotStarted)]
        [TestCase(OpeningCutsceneState.Playing)]
        [TestCase(OpeningCutsceneState.Completed)]
        public void UnsupportedMapOrMissingConfigurationPreservesGameplay(OpeningCutsceneState state) =>
            Assert.That(OpeningCutsceneRules.GameplayReleased(false, state), Is.True);

        [Test]
        public void SharedStartTick_SeeksLatePeerWithoutReplayingFromZero()
        {
            Assert.That(OpeningCutsceneRules.Elapsed(900, 1000, 60), Is.Zero);
            Assert.That(OpeningCutsceneRules.Elapsed(1000, 1000, 60), Is.Zero);
            Assert.That(OpeningCutsceneRules.Elapsed(1600, 1000, 60), Is.EqualTo(10d));
            Assert.That(OpeningCutsceneRules.Elapsed(1600, 1000, 0), Is.Zero);
        }

        [Test]
        public void SnapshotRestoration_UsesOriginalStartTick()
        {
            double before = OpeningCutsceneRules.Elapsed(1900, 1000, 60);
            double after = OpeningCutsceneRules.Elapsed(1901, 1000, 60);
            Assert.That(after, Is.EqualTo(before + 1d / 60d).Within(0.000001d));
        }

        [Test]
        public void SkipRequiresEveryConnectedPlayer_AndRejectsDuplicateOrForeignVotes()
        {
            var players = new[] { 1, 2, 3 };
            Assert.That(OpeningCutsceneRules.CanVote(OpeningCutsceneState.Playing, 2, players, new[] { 1 }), Is.True);
            Assert.That(OpeningCutsceneRules.CanVote(OpeningCutsceneState.Playing, 1, players, new[] { 1 }), Is.False);
            Assert.That(OpeningCutsceneRules.CanVote(OpeningCutsceneState.Playing, 9, players, new int[0]), Is.False);
            Assert.That(OpeningCutsceneRules.Unanimous(players, new[] { 1, 2, 2, 9 }), Is.False);
            Assert.That(OpeningCutsceneRules.CountVotes(players, new[] { 1, 2, 2, 9 }), Is.EqualTo(2));
            Assert.That(OpeningCutsceneRules.Unanimous(players, new[] { 1, 2, 3 }), Is.True);
        }

        [Test]
        public void DisconnectRecomputesElectorate_AndLateJoinRequiresAnotherVote()
        {
            Assert.That(OpeningCutsceneRules.Unanimous(new[] { 1, 2, 3 }, new[] { 1, 2 }), Is.False);
            Assert.That(OpeningCutsceneRules.Unanimous(new[] { 1, 2 }, new[] { 1, 2 }), Is.True);
            Assert.That(OpeningCutsceneRules.CountVotes(new[] { 1 }, new[] { 1, 2 }), Is.EqualTo(1));
        }

        [Test]
        public void SoloCanSkip_EmptySessionCannotTriggerUnanimity()
        {
            Assert.That(OpeningCutsceneRules.Unanimous(new[] { 1 }, new[] { 1 }), Is.True);
            Assert.That(OpeningCutsceneRules.Unanimous(new int[0], new[] { 1 }), Is.False);
        }

        [TestCase(OpeningCutsceneState.NotStarted)]
        [TestCase(OpeningCutsceneState.Completed)]
        public void VoteOutsidePlayback_IsRejected(OpeningCutsceneState state) =>
            Assert.That(OpeningCutsceneRules.CanVote(state, 1, new[] { 1 }, new int[0]), Is.False);

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(61d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidTimelineDuration_CannotStart(double duration) =>
            Assert.That(OpeningCutsceneRules.ValidDuration(duration), Is.False);

        [TestCase(25d)]
        [TestCase(30d)]
        [TestCase(35d)]
        public void AuthoredTimelineDuration_IsAccepted(double duration) =>
            Assert.That(OpeningCutsceneRules.ValidDuration(duration), Is.True);
    }
}
