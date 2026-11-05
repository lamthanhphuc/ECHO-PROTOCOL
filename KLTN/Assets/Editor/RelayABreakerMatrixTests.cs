using System;
using System.IO;
using EchoProtocol.RelayA;
using NUnit.Framework;

namespace EchoProtocol.Tests
{
    public sealed class RelayABreakerMatrixTests
    {
        [SetUp] public void SetUp() { }
        [TearDown] public void TearDown() { }

        // Independent exhaustive first-row chase: all solutions are determined by their first-row presses.
        public static uint FindOptimalPresses(RelayABreakerPattern pattern)
        {
            int size = pattern.Size;
            int best = int.MaxValue;
            uint solution = 0;
            for (int firstRow = 0; firstRow < 1 << size; firstRow++)
            {
                uint red = pattern.InitialRed;
                uint pressed = 0;
                bool valid = true;
                for (int row = 0; row < size && valid; row++)
                    for (int column = 0; column < size; column++)
                    {
                        int cell = row * size + column;
                        bool press = row == 0 ? (firstRow & (1 << column)) != 0
                            : (red & (1u << (cell - size))) != 0;
                        if (!press) continue;
                        if ((pattern.Locked & (1u << cell)) != 0) { valid = false; break; }
                        pressed |= 1u << cell;
                        // Deliberately use coordinates rather than the runtime bit-mask helper.
                        red ^= 1u << cell;
                        if (column > 0) red ^= 1u << (cell - 1);
                        if (column + 1 < size) red ^= 1u << (cell + 1);
                        if (row > 0) red ^= 1u << (cell - size);
                        if (row + 1 < size) red ^= 1u << (cell + size);
                    }
                int moves = RelayABreakerMatrix.CountBits(pressed);
                if (!valid || red != 0 || moves >= best) continue;
                best = moves;
                solution = pressed;
            }
            if (best == int.MaxValue) throw new InvalidOperationException("Authored matrix is unsolvable.");
            return solution;
        }

        [Test]
        public void SixAuthoredPatterns_HaveVerifiedOptimalDifficultyAndNoLockedPresses()
        {
            foreach (bool hard in new[] { false, true })
            {
                var seen = new System.Collections.Generic.HashSet<uint>();
                for (int index = 0; index < 3; index++)
                {
                    var pattern = RelayABreakerMatrix.GetPattern(hard, index);
                    Assert.That(pattern.Size, Is.EqualTo(hard ? 5 : 4));
                    Assert.That(RelayABreakerMatrix.CountBits(pattern.Locked), Is.EqualTo(hard ? 1 : 0));
                    Assert.That(pattern.InitialRed, Is.Not.Zero);
                    Assert.IsTrue(seen.Add(pattern.InitialRed));
                    uint solution = FindOptimalPresses(pattern);
                    Assert.That(solution & pattern.Locked, Is.Zero);
                    int moves = RelayABreakerMatrix.CountBits(solution);
                    Assert.That(moves, Is.InRange(hard ? 8 : 5, hard ? 12 : 8), $"hard={hard}, pattern={index}, optimum={moves}");
                }
            }
        }

        [Test]
        public void ToggleMask_CoversOnlyOrthogonalNeighborsAndDoesNotWrapRows()
        {
            Assert.That(RelayABreakerMatrix.ToggleMask(4, 5), Is.EqualTo((1u << 5) | (1u << 1) | (1u << 4) | (1u << 6) | (1u << 9)));
            Assert.That(RelayABreakerMatrix.CountBits(RelayABreakerMatrix.ToggleMask(4, 0)), Is.EqualTo(3));
            Assert.That(RelayABreakerMatrix.ToggleMask(4, 3) & (1u << 4), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => RelayABreakerMatrix.ToggleMask(5, 25));
        }

        [Test]
        public void Press_HasPulseDelayRejectsSpamAndDoublePressRestoresState()
        {
            var sim = new RelayABreakerMatrix();
            sim.Initialize(false, 123);
            uint before = sim.Snapshot.Red;
            Assert.IsTrue(sim.Press(5));
            Assert.That(sim.Snapshot.Red, Is.EqualTo(before));
            Assert.That(sim.Snapshot.Phase, Is.EqualTo(RelayABreakerPhase.Pulsing));
            Assert.IsFalse(sim.Press(6));
            Assert.IsFalse(sim.Reset());
            sim.Tick(0.1f);
            Assert.That(sim.Snapshot.Red, Is.EqualTo(before));
            sim.Tick(0.1f);
            Assert.That(sim.Snapshot.Red, Is.EqualTo(before ^ RelayABreakerMatrix.ToggleMask(4, 5)));
            Assert.That(sim.Snapshot.Moves, Is.EqualTo(1));
            Assert.IsTrue(sim.Press(5));
            sim.Tick(0.2f);
            Assert.That(sim.Snapshot.Red, Is.EqualTo(before));
            Assert.That(sim.Snapshot.Moves, Is.EqualTo(2));
        }

        [Test]
        public void LockedBreaker_RejectsDirectPressButChangesFromNeighbor()
        {
            var sim = new RelayABreakerMatrix();
            sim.Initialize(true, 123);
            Assert.IsFalse(sim.Press(12));
            Assert.IsFalse(sim.Press(-1));
            Assert.IsFalse(sim.Press(25));
            uint before = sim.Snapshot.Red;
            Assert.IsTrue(sim.Press(11));
            sim.Tick(0.2f);
            Assert.That((sim.Snapshot.Red ^ before) & (1u << 12), Is.Not.Zero);
        }

        [Test]
        public void Reset_RestoresSamePatternAndDoesNotLimitMoves()
        {
            var sim = new RelayABreakerMatrix();
            sim.Initialize(true, 321);
            var initial = sim.Snapshot;
            for (int press = 0; press < 100; press++)
            {
                Assert.IsTrue(sim.Press(0));
                sim.Tick(0.2f);
            }
            Assert.That(sim.Snapshot.Moves, Is.EqualTo(100));
            Assert.IsTrue(sim.Reset());
            Assert.That(sim.Snapshot.Red, Is.EqualTo(initial.Red));
            Assert.That(sim.Snapshot.PatternIndex, Is.EqualTo(initial.PatternIndex));
            Assert.That(sim.Snapshot.Moves, Is.Zero);
        }

        [Test]
        public void AllGreen_AutomaticallyBalancesBeforeCompletingExactlyOnce()
        {
            var sim = new RelayABreakerMatrix();
            sim.Initialize(true, 123);
            uint solution = FindOptimalPresses(sim.Snapshot.Pattern);
            int completed = 0;
            sim.Completed += () => completed++;
            for (int cell = 0; cell < 25; cell++)
                if ((solution & (1u << cell)) != 0)
                {
                    Assert.IsTrue(sim.Press(cell));
                    sim.Tick(0.2f);
                }
            Assert.That(sim.Snapshot.Red, Is.Zero);
            Assert.That(sim.Snapshot.Phase, Is.EqualTo(RelayABreakerPhase.Balancing));
            Assert.That(completed, Is.Zero);
            Assert.IsFalse(sim.Press(0));
            Assert.IsFalse(sim.Reset());
            sim.Tick(0.4f);
            Assert.That(completed, Is.Zero);
            sim.Tick(0.5f);
            Assert.That(completed, Is.EqualTo(1));
            sim.Tick(10f);
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void Seed_SelectsPatternsReproduciblyAndCoversAllThree()
        {
            var selected = new System.Collections.Generic.HashSet<int>();
            for (int seed = 1; seed <= 30; seed++)
            {
                var a = new RelayABreakerMatrix();
                var b = new RelayABreakerMatrix();
                a.Initialize(true, seed);
                b.Initialize(true, seed);
                Assert.That(a.Snapshot.Red, Is.EqualTo(b.Snapshot.Red));
                Assert.That(a.Snapshot.PatternIndex, Is.EqualTo(b.Snapshot.PatternIndex));
                selected.Add(a.Snapshot.PatternIndex);
            }
            Assert.That(selected.Count, Is.EqualTo(3));
        }

        [Test]
        public void ReplicatedMatrix_DoesNotAwardCompletionAndSkipsIdenticalSnapshots()
        {
            var proxy = new RelayABreakerMatrix();
            proxy.Initialize(true, 123);
            int notifications = 0, completed = 0;
            proxy.Changed += () => notifications++;
            proxy.Completed += () => completed++;
            var state = proxy.Snapshot;
            proxy.ApplyAuthoritative(true, state.PatternIndex, state.Red, state.Moves, state.Phase, state.Pulse, state.Sequence);
            Assert.That(notifications, Is.Zero);
            proxy.ApplyAuthoritative(true, state.PatternIndex, 0, 10, RelayABreakerPhase.Complete, 0, 11);
            Assert.That(proxy.Snapshot.Moves, Is.EqualTo(10));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(completed, Is.Zero);
        }

        [Test]
        public void UiAndNetworkContracts_KeepStageScopedResetAndValidatedOperatorCommands()
        {
            string prefix = Directory.Exists("Assets/Scripts/RelayA") ? "" : "KLTN/";
            string network = File.ReadAllText(prefix + "Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs");
            StringAssert.Contains("RpcRelayABreaker", network);
            int start = network.IndexOf("private bool TryRelayABreakerAuthoritative", StringComparison.Ordinal);
            int end = network.IndexOf("private bool TryRelayAStabilizationAuthoritative", start, StringComparison.Ordinal);
            string handler = network.Substring(start, end - start);
            StringAssert.Contains("TryValidateRelayCommand(requester, slot", handler);
            StringAssert.Contains("GetRelayOperator(slot) != requester", handler);
            StringAssert.Contains("CaptureRelayACircuitState", handler);
            string ui = File.ReadAllText(prefix + "Assets/Scripts/RelayA/RelayAUIController.Breakers.cs");
            StringAssert.Contains("RESET MATRIX", ui);
            StringAssert.Contains("MOVES", ui);
            StringAssert.DoesNotContain("VERIFY", ui);
            StringAssert.DoesNotContain("FindOptimalPresses", ui);
        }
    }
}
