using System;
using System.Reflection;
using EchoProtocol.RelayB;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.RelayB.Tests
{
    public sealed class RelayBDecoderTests
    {
        private static int Code(params int[] digits)
        {
            int result = 0; for (int i = 0; i < digits.Length; i++) result |= digits[i] << (i * 4); return result;
        }
        private static int Secret(RelayBDecoder decoder) => (int)typeof(RelayBDecoder)
            .GetField("_secret", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(decoder);
        private static RelayBDecoder Create(int seed = 41) { var d = new RelayBDecoder(); d.Initialize(seed); return d; }
        private static int Wrong(RelayBDecoder d) => Secret(d) == Code(1, 2, 3, 4, 5, 6) ? Code(6, 5, 4, 3, 2, 1) : Code(1, 2, 3, 4, 5, 6);

        [Test] public void FeedbackMatchesPromptExample()
        {
            int result = RelayBDecoder.Evaluate(Code(8, 3, 6, 1, 9, 4), Code(8, 6, 3, 1, 5, 4));
            var expected = new[] { RelayBCodeFeedback.Right, RelayBCodeFeedback.WrongPlace, RelayBCodeFeedback.WrongPlace,
                RelayBCodeFeedback.Right, RelayBCodeFeedback.Unused, RelayBCodeFeedback.Right };
            for (int i = 0; i < 6; i++) Assert.That(RelayBDecoder.FeedbackAt(result, i), Is.EqualTo(expected[i]));
        }
        [Test] public void RejectsMissingDuplicateOutOfRangeAndExtraBits()
        {
            var d = Create();
            foreach (int code in new[] { 0, Code(1, 2, 3, 4, 5, 0), Code(1, 2, 3, 4, 5, 1), Code(1, 2, 3, 4, 5, 10), Code(1, 2, 3, 4, 5, 6) | (1 << 24), -1 })
                Assert.IsFalse(d.Transmit(code));
            Assert.That(d.Snapshot.Attempts, Is.Zero);
        }
        [Test] public void GeneratedCodesAlwaysHaveSixDistinctSignalsAndVary()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int seed = 0; seed < 300; seed++) { int code = Secret(Create(seed)); Assert.IsTrue(RelayBDecoder.IsValidCode(code)); seen.Add(code); }
            Assert.That(seen.Count, Is.GreaterThan(280));
        }
        [Test] public void CannotDoubleTransmitDuringAnimation()
        {
            var d = Create(); Assert.IsTrue(d.Transmit(Wrong(d))); Assert.IsFalse(d.Transmit(Wrong(d)));
            Assert.That(d.Snapshot.Attempts, Is.EqualTo(1));
        }
        [Test] public void FeedbackIsStaggeredAndHistoryWaitsForHold()
        {
            var d = Create(); int guess = Wrong(d); d.Transmit(guess);
            d.Tick(0.8f); Assert.That(d.Snapshot.RevealedCount, Is.Zero);
            d.Tick(0.2f); Assert.That(d.Snapshot.RevealedCount, Is.EqualTo(1));
            Assert.That(d.Snapshot.Codes[0], Is.Zero);
            d.Tick(0.7f); Assert.That(d.Snapshot.Phase, Is.EqualTo(RelayBDecodePhase.Holding));
            Assert.That(d.Snapshot.Current, Is.EqualTo(guess)); Assert.That(d.Snapshot.Codes[0], Is.Zero);
            d.Tick(0.9f); Assert.That(d.Snapshot.Phase, Is.EqualTo(RelayBDecodePhase.Editing));
            Assert.That(d.Snapshot.Codes[0], Is.EqualTo(guess)); Assert.That(d.Snapshot.Current, Is.Zero);
        }
        [Test] public void FiveFailuresRerollOnlyAfterGlitchAndGenerateDifferentSecret()
        {
            var d = Create(); int original = Secret(d), failed = 0; d.Failed += () => failed++;
            for (int i = 0; i < 5; i++) { Assert.IsTrue(d.Transmit(Wrong(d))); d.Tick(2.52f); }
            Assert.That(d.Snapshot.Phase, Is.EqualTo(RelayBDecodePhase.Failed)); Assert.That(failed, Is.EqualTo(1));
            Assert.IsFalse(d.Transmit(Wrong(d))); d.Tick(1.7f); Assert.That(Secret(d), Is.EqualTo(original));
            d.Tick(0.2f); Assert.That(Secret(d), Is.Not.EqualTo(original)); Assert.That(d.Snapshot.Round, Is.EqualTo(2));
            Assert.That(d.Snapshot.Attempts, Is.Zero); Assert.That(d.Snapshot.Codes, Is.All.Zero);
        }
        [Test] public void CorrectCodeDoesNotCompleteBeforeConfirmationAndCompletesOnce()
        {
            var d = Create(); int completed = 0; d.Completed += () => completed++;
            d.Transmit(Secret(d)); d.Tick(2.52f); Assert.That(d.Snapshot.Phase, Is.EqualTo(RelayBDecodePhase.Solved));
            Assert.That(completed, Is.Zero); d.Tick(0.81f); Assert.IsTrue(d.Snapshot.IsComplete);
            d.Tick(100f); Assert.That(completed, Is.EqualTo(1)); Assert.IsFalse(d.Transmit(Secret(d)));
        }
        [Test] public void LargeTicksMatchSmallTicksAndInvalidDeltasAreIgnored()
        {
            var a = Create(); var b = Create(); a.Transmit(Wrong(a)); b.Transmit(Wrong(b));
            a.Tick(2.6f); for (int i = 0; i < 260; i++) b.Tick(0.01f);
            Assert.That(a.Snapshot.Phase, Is.EqualTo(b.Snapshot.Phase));
            Assert.That(a.Snapshot.Codes, Is.EqualTo(b.Snapshot.Codes));
            a.Tick(float.NaN); a.Tick(float.PositiveInfinity); a.Tick(-1); Assert.That(a.Snapshot.Attempts, Is.EqualTo(1));
        }
        [Test] public void ReplicaHistoryDoesNotEmitWorldEventsOrExposeSecret()
        {
            var host = Create(); host.Transmit(Secret(host)); host.Tick(4);
            var replica = Create(22); int events = 0; replica.Failed += () => events++; replica.Completed += () => events++;
            replica.ApplyAuthoritative(host.Snapshot); Assert.IsTrue(replica.Snapshot.IsComplete); Assert.That(events, Is.Zero);
            Assert.IsNull(typeof(RelayBDecodeTelemetry).GetField("Secret"));
            Assert.That(RelayBDecodeTelemetry.From(host.Snapshot).Snapshot.Codes, Is.EqualTo(host.Snapshot.Codes));
        }
        [Test] public void SnapshotHistoryIsImmutableFromCallers()
        {
            var d = Create(); d.Transmit(Wrong(d)); d.Tick(2.6f); var snapshot = d.Snapshot;
            snapshot.Codes[0] = 0; Assert.That(d.Snapshot.Codes[0], Is.Not.Zero);
        }
        [Test] public void StageOneSurvivesDecoderFailureAndSyncRequiresDecodedSignal()
        {
            var config = ScriptableObject.CreateInstance<RelayBConfig>();
            try
            {
                config.InitializeDefaultPresetsIfEmpty(); var sim = new RelayBSignalSimulation(); sim.Initialize(config);
                sim.ScanSpectrum(); sim.SelectChannel(sim.GetCurrentPreset().CorrectChannelIndex); sim.SetActiveTab(1);
                sim.SetPipelineSlot(0, RelayBModuleType.NoiseSuppressor); sim.AnalyzeOutput(); sim.StartSynchronization();
                Assert.IsFalse(sim.IsSynchronizing); sim.SetActiveTab(2); Assert.That(sim.ActiveTab, Is.EqualTo(1));
                int channel = sim.SelectedChannelIndex;
                for (int i = 0; i < 5; i++) { sim.Decoder.Transmit(Wrong(sim.Decoder)); sim.Tick(2.52f); }
                sim.Tick(1.81f); Assert.IsTrue(sim.IsSignalFound); Assert.That(sim.SelectedChannelIndex, Is.EqualTo(channel));
                sim.Decoder.Transmit(Secret(sim.Decoder)); sim.Tick(3.4f);
                Assert.IsTrue(sim.IsSignalClean); Assert.That(sim.ActiveTab, Is.EqualTo(1)); Assert.IsFalse(sim.IsSynchronizing);
                sim.SetActiveTab(2); Assert.That(sim.ActiveTab, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public void WaitingNeverConsumesAttempts()
        {
            var d = Create(); d.Tick(3600); Assert.That(d.Snapshot.Attempts, Is.Zero); Assert.That(d.Snapshot.Round, Is.EqualTo(1));
        }
        [Test] public void ReplicaAcceptsRoundResetFromNewAuthoritativeRepairAttempt()
        {
            var replica = Create();
            replica.ApplyAuthoritative(new RelayBDecodeSnapshot(4, 1, 0, 0, RelayBDecodePhase.Complete, 0, new int[5], new int[5]));
            replica.ApplyAuthoritative(Create(77).Snapshot);
            Assert.That(replica.Snapshot.Round, Is.EqualTo(1)); Assert.IsFalse(replica.IsComplete); Assert.IsTrue(replica.CanEdit);
        }
    }
}
