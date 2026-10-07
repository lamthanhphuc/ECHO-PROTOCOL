using EchoProtocol.RelayA;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests
{
    public sealed class RelayACircuitTests
    {
        private RelayAConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<RelayAConfig>();
            _config.InitializeDefaultCircuitScenariosIfEmpty();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void Config_UpgradesOldDefaultsButPreservesCustomBoards()
        {
            var type = typeof(RelayAConfig);
            var revision = type.GetField("defaultBoardRevision", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic);
            var scenarios = type.GetField("circuitScenarios", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic);
            var oldDefaults = RelayACircuitScenarios.CreateDefaults();
            scenarios.SetValue(_config, oldDefaults);
            revision.SetValue(_config, 0);
            _config.InitializeDefaultCircuitScenariosIfEmpty();
            Assert.That(_config.CircuitScenarios, Is.Not.SameAs(oldDefaults));
            var custom = new[] { oldDefaults[0], oldDefaults[2] };
            scenarios.SetValue(_config, custom);
            revision.SetValue(_config, 0);
            _config.InitializeDefaultCircuitScenariosIfEmpty();
            Assert.That(_config.CircuitScenarios, Is.SameAs(custom));
        }

        [Test]
        public void Reroute_RequiresMultipleChangedTiles_NotOneClick()
        {
            for (int i = 0; i < _config.CircuitScenarios.Count; i++)
            {
                var board = _config.GetCircuitScenario(i);
                int budget = 3;
                var rotations = (int[])board.InitialSolution.Clone();
                Assert.IsFalse(CanSolveWithChanges(board, rotations, 0, budget), board.Name);
                Assert.IsTrue(RelayACircuitBoard.Evaluate(board, board.FaultSolution, true).IsValid);
            }
        }

        private static bool CanSolveWithChanges(RelayACircuitScenario board, int[] rotations, int start, int budget, bool faultActive = true)
        {
            if (RelayACircuitBoard.Evaluate(board, rotations, faultActive).IsValid) return true;
            if (budget == 0) return false;
            for (int cell = start; cell < board.Count; cell++)
            {
                if (board.Cells[cell].Locked || faultActive && cell == board.FailedCell) continue;
                int original = rotations[cell];
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    if (rotation == original) continue;
                    rotations[cell] = rotation;
                    bool solved = CanSolveWithChanges(board, rotations, cell + 1, budget - 1, faultActive);
                    rotations[cell] = original;
                    if (solved) return true;
                }
            }
            return false;
        }

        [Test]
        public void InitialBoards_AreDenseAndCannotBeSolvedByFiveTileEdits()
        {
            foreach (var board in _config.CircuitScenarios)
            {
                Assert.That(board.Width, Is.EqualTo(6));
                Assert.That(board.Height, Is.EqualTo(5));
                var initial = new int[board.Count];
                var result = RelayACircuitBoard.Evaluate(board, board.InitialSolution, false);
                int poweredEditable = 0, meaningfulChanges = 0;
                for (int cell = 0; cell < board.Count; cell++)
                {
                    initial[cell] = board.Cells[cell].Rotation;
                    if (board.Cells[cell].Locked || (result.Powered & (1UL << cell)) == 0) continue;
                    poweredEditable++;
                    if (RelayACircuitBoard.Connections(board.Cells[cell].Type, initial[cell])
                        != RelayACircuitBoard.Connections(board.Cells[cell].Type, board.InitialSolution[cell])) meaningfulChanges++;
                }
                Assert.That(poweredEditable, Is.GreaterThanOrEqualTo(12), board.Name);
                Assert.That(meaningfulChanges, Is.EqualTo(poweredEditable), board.Name);
                Assert.IsFalse(CanSolveWithChanges(board, initial, 0, 5, false), board.Name);
            }
        }

        [Test]
        public void StartingRotations_CannotBeSolvedByUniformRotation()
        {
            foreach (var board in _config.CircuitScenarios)
                for (int offset = 0; offset < 4; offset++)
                {
                    var rotations = new int[board.Count];
                    for (int cell = 0; cell < board.Count; cell++)
                        rotations[cell] = (board.Cells[cell].Rotation + (board.Cells[cell].Locked ? 0 : offset)) & 3;
                    Assert.IsFalse(RelayACircuitBoard.Evaluate(board, rotations, false).IsValid, board.Name);
                }
        }

        [Test]
        public void TileConnections_RotateClockwiseAndWrap()
        {
            Assert.That(RelayACircuitBoard.Connections(RelayACircuitTile.Straight, 0), Is.EqualTo(5));
            Assert.That(RelayACircuitBoard.Connections(RelayACircuitTile.Straight, 1), Is.EqualTo(10));
            Assert.That(RelayACircuitBoard.Connections(RelayACircuitTile.Corner, 0), Is.EqualTo(3));
            Assert.That(RelayACircuitBoard.Connections(RelayACircuitTile.Corner, 1), Is.EqualTo(6));
            Assert.That(RelayACircuitBoard.Connections(RelayACircuitTile.Junction, 1), Is.EqualTo(7));
            Assert.That(RelayACircuitBoard.Connections(RelayACircuitTile.Cross, 3), Is.EqualTo(15));
            Assert.That(RelayACircuitBoard.Connections(RelayACircuitTile.Corner, 4), Is.EqualTo(3));
        }

        [Test]
        public void EveryRelayABoard_HasInitialAndPostFaultSolution()
        {
            Assert.That(_config.CircuitScenarios.Count, Is.EqualTo(4));
            for (int i = 0; i < _config.CircuitScenarios.Count; i++)
            {
                var board = _config.GetCircuitScenario(i);
                Assert.IsTrue(RelayACircuitBoard.HasAuthoredSolutions(board), board.Name);
                var starting = new int[board.Count];
                for (int cell = 0; cell < board.Count; cell++) starting[cell] = board.Cells[cell].Rotation;
                Assert.IsFalse(RelayACircuitBoard.Evaluate(board, starting, false).IsValid, board.Name);
                Assert.IsFalse(RelayACircuitBoard.Evaluate(board, board.InitialSolution, true).IsValid, board.Name);
            }
        }

        [Test]
        public void Connectivity_RequiresReciprocalPorts()
        {
            var cells = new[]
            {
                new RelayACircuitCell(RelayACircuitTile.Source, 1),
                new RelayACircuitCell(RelayACircuitTile.Straight, 0),
                new RelayACircuitCell(RelayACircuitTile.Target, 3)
            };
            var board = new RelayACircuitScenario("Reciprocal", 3, 1, cells, 1,
                new[] { 1, 1, 3 }, new[] { 1, 1, 3 });
            Assert.That(RelayACircuitBoard.Evaluate(board, new[] { 1, 0, 3 }, false).MissingTargets,
                Is.EqualTo(1));
            Assert.IsTrue(RelayACircuitBoard.Evaluate(board, new[] { 1, 1, 3 }, false).IsValid);
        }

        [Test]
        public void Splitter_PowersBothTargetsWithoutPoweringRed()
        {
            var cells = new RelayACircuitCell[9];
            for (int i = 0; i < cells.Length; i++) cells[i] = new RelayACircuitCell(RelayACircuitTile.Empty);
            cells[3] = new RelayACircuitCell(RelayACircuitTile.Source, 1);
            cells[4] = new RelayACircuitCell(RelayACircuitTile.Junction, 3);
            cells[1] = new RelayACircuitCell(RelayACircuitTile.Target, 2);
            cells[7] = new RelayACircuitCell(RelayACircuitTile.Target, 0);
            cells[5] = new RelayACircuitCell(RelayACircuitTile.Fault, 3);
            var rotations = new[] { 0, 2, 0, 1, 3, 3, 0, 0, 0 };
            var board = new RelayACircuitScenario("Split", 3, 3, cells, 4, rotations, rotations);
            Assert.IsTrue(RelayACircuitBoard.Evaluate(board, rotations, false).IsValid);
            rotations[4] = 2;
            var tripped = RelayACircuitBoard.Evaluate(board, rotations, false);
            Assert.IsTrue(tripped.FaultPowered);
            Assert.IsFalse(tripped.IsValid);
        }

        [Test]
        public void InitialFailure_DoesNotRevealPowerUntilTestAndNeverCompletes()
        {
            var sim = new RelayACircuitSimulation();
            sim.Initialize(_config, 2);
            Assert.That(sim.Snapshot.Powered, Is.Zero);
            Assert.IsTrue(sim.TestCircuit());
            Assert.IsFalse(sim.TestCircuit());
            sim.Tick(0.1f);
            Assert.That(sim.Snapshot.Phase, Is.EqualTo(RelayACircuitPhase.Testing));
            sim.Tick(_config.CircuitTestSeconds);
            Assert.That(sim.Snapshot.Phase, Is.EqualTo(RelayACircuitPhase.Failed));
            Assert.IsFalse(sim.IsOnline);
        }

        [Test]
        public void Replication_IsCompactAndDoesNotRefreshIdenticalState()
        {
            var host = new RelayACircuitSimulation();
            var proxy = new RelayACircuitSimulation();
            host.Initialize(_config, 3);
            proxy.Initialize(_config, 3);
            var state = host.Snapshot;
            int notifications = 0;
            proxy.Changed += _ => notifications++;
            proxy.ApplyAuthoritative(3, host.PackedRotations, state.Phase, 0, 0, false, 0, false);
            Assert.That(notifications, Is.Zero);
            Assert.That(proxy.Snapshot.Rotations, Is.EqualTo(state.Rotations));
            Assert.IsTrue(host.Rotate(state.Scenario.FailedCell));
            proxy.ApplyAuthoritative(3, host.PackedRotations, state.Phase, 0, 0, false, 0, false);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(proxy.Snapshot.Rotations, Is.EqualTo(host.Snapshot.Rotations));
        }

        [Test]
        public void Rotation_WrapsAndLockedTilesRemainFixed()
        {
            var sim = new RelayACircuitSimulation();
            sim.Initialize(_config, 2);
            int cell = sim.Snapshot.Scenario.FailedCell;
            int rotation = sim.Snapshot.Rotations[cell];
            for (int i = 0; i < 4; i++) Assert.IsTrue(sim.Rotate(cell));
            Assert.That(sim.Snapshot.Rotations[cell], Is.EqualTo(rotation));
            for (int i = 0; i < sim.Snapshot.Scenario.Count; i++)
                if (sim.Snapshot.Scenario.Cells[i].Locked) Assert.IsFalse(sim.Rotate(i));
        }

        [Test]
        public void MalformedBoard_IsRejectedBeforeNetworkPacking()
        {
            var cells = new RelayACircuitCell[35];
            var board = new RelayACircuitScenario("Too large", 7, 5, cells, 1, new int[35], new int[35]);
            Assert.IsFalse(RelayACircuitBoard.HasAuthoredSolutions(board));
            Assert.Throws<System.ArgumentException>(() => RelayACircuitBoard.Evaluate(board, new int[35], false));
        }

        [Test]
        public void RotationAndTwoTests_CompleteExactlyOnce()
        {
            typeof(RelayAConfig).GetField("requireFaultRecovery", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).SetValue(_config, false);
            var sim = new RelayACircuitSimulation();
            sim.Initialize(_config, 0);
            var board = sim.Snapshot.Scenario;
            int completions = 0;
            int faults = 0;
            sim.Completed += () => completions++;
            sim.FaultActivated += () => faults++;
            Assert.IsFalse(sim.IsOnline);
            Assert.IsFalse(sim.Rotate(System.Array.FindIndex(board.Cells, cell => cell.Type == RelayACircuitTile.Source)));
            Assert.IsFalse(sim.SetStabilizationControls(50f, 50f, 50f));
            Assert.IsFalse(sim.SetStabilizationRunning(true));

            for (int i = 0; i < board.Count; i++)
                while (sim.Snapshot.Rotations[i] != board.InitialSolution[i])
                    Assert.IsTrue(sim.Rotate(i));
            Assert.IsTrue(sim.TestCircuit());
            Assert.IsFalse(sim.Rotate(board.FailedCell));

            sim.Tick(_config.CircuitTestSeconds + 0.1f);
            Assert.That(sim.Snapshot.Phase, Is.EqualTo(RelayACircuitPhase.Stable));
            Assert.That(completions, Is.Zero);
            sim.Tick(_config.CircuitFaultDelaySeconds + 0.1f);
            Assert.IsTrue(sim.Snapshot.FaultActive);
            board = sim.Snapshot.Scenario;
            Assert.IsFalse(sim.Rotate(board.FailedCell));

            Assert.IsTrue(sim.TestCircuit());
            sim.Tick(_config.CircuitTestSeconds + 0.1f);
            Assert.That(sim.Snapshot.Phase, Is.EqualTo(RelayACircuitPhase.Failed));
            Assert.IsTrue(sim.Snapshot.FaultActive);
            Assert.That(faults, Is.EqualTo(1));

            for (int i = 0; i < board.Count; i++)
                while (i != board.FailedCell && sim.Snapshot.Rotations[i] != board.FaultSolution[i])
                    Assert.IsTrue(sim.Rotate(i));
            Assert.IsTrue(sim.TestCircuit());
            sim.Tick(_config.CircuitTestSeconds + 0.1f);
            Assert.That(sim.Snapshot.Phase, Is.EqualTo(RelayACircuitPhase.BreakerMatrix));
            Assert.IsFalse(sim.SetStabilizationRunning(true));
            Assert.IsFalse(sim.SetStabilizationControls(50f, 50f, 50f));
            var routeBeforeMatrix = sim.Snapshot.Rotations;
            Assert.IsTrue(sim.ResetBreakers());
            Assert.That(sim.Snapshot.Rotations, Is.EqualTo(routeBeforeMatrix));
            uint breakerSolution = RelayABreakerMatrixTests.FindOptimalPresses(sim.Breakers.Snapshot.Pattern);
            for (int cell = 0; cell < 16; cell++)
                if ((breakerSolution & (1u << cell)) != 0)
                {
                    Assert.IsTrue(sim.PressBreaker(cell));
                    sim.Tick(0.2f);
                }
            Assert.That(sim.Snapshot.Phase, Is.EqualTo(RelayACircuitPhase.BreakerMatrix));
            sim.Tick(RelayABreakerMatrix.BalanceSeconds + 0.1f);
            Assert.That(sim.Snapshot.Phase, Is.EqualTo(RelayACircuitPhase.StabilizeOutput));
            Assert.IsFalse(sim.PressBreaker(0));
            Assert.IsFalse(sim.ResetBreakers());
            Assert.That(sim.Snapshot.Rotations, Is.EqualTo(routeBeforeMatrix));
            Assert.IsFalse(sim.IsOnline);
            Assert.That(completions, Is.Zero);
            Assert.IsFalse(sim.Rotate(1));
            Assert.IsFalse(sim.TestCircuit());
            Assert.IsFalse(sim.SetStabilizationControls(float.NaN, 50f, 50f));
            var preservedRotations = sim.Snapshot.Rotations;
            Assert.IsTrue(sim.SetStabilizationRunning(true));
            sim.Tick(0.2f);
            Assert.IsTrue(sim.SetStabilizationRunning(false));
            Assert.That(sim.Snapshot.Rotations, Is.EqualTo(preservedRotations));
            Vector3 solved = Vector3.zero;
            float bestDistance = float.PositiveInfinity;
            for (int g = 0; g <= 100; g += 5)
                for (int f = 0; f <= 100; f += 5)
                    for (int l = 0; l <= 100; l += 5)
                    {
                        var candidate = new Vector3(g, f, l);
                        var output = sim.Stabilization.EvaluateControlTarget(candidate, RelayAFaultType.None, 0f);
                        float distance = Mathf.Pow((output.Voltage - 225f) / 4f, 2f)
                            + Mathf.Pow((output.Frequency - 50f) / 0.8f, 2f)
                            + Mathf.Pow((output.LoadBalance - 50f) / 2.5f, 2f);
                        if (distance >= bestDistance) continue;
                        bestDistance = distance;
                        solved = candidate;
                    }
            Assert.IsTrue(sim.SetStabilizationControls(solved.x, solved.y, solved.z));
            for (int tick = 0; tick < 100; tick++) sim.Tick(0.1f);
            Assert.IsTrue(sim.SetStabilizationRunning(true));
            for (int tick = 0; tick < 400 && !sim.IsOnline; tick++) sim.Tick(0.1f);
            Assert.IsTrue(sim.IsOnline);
            Assert.That(completions, Is.EqualTo(1));
            Assert.IsFalse(sim.TestCircuit());
            Assert.IsFalse(sim.Rotate(1));
            sim.Tick(100f);
            Assert.That(faults, Is.EqualTo(1));
            sim.Initialize(_config, 0);
            Assert.IsFalse(sim.Snapshot.FaultActive);
            Assert.IsFalse(sim.IsOnline);
            Assert.That(sim.Snapshot.TestSequence, Is.Zero);
            Assert.That(sim.Breakers.Snapshot.Moves, Is.Zero);
            Assert.That(sim.Breakers.Snapshot.Red, Is.EqualTo(sim.Breakers.Snapshot.Pattern.InitialRed));
        }

        [Test]
        public void FailedRoutingTest_RescramblesEditableTilesAndReplicatesReset()
        {
            var host = new RelayACircuitSimulation();
            host.Initialize(_config, 2, 123);
            var before = host.Snapshot;
            Assert.IsFalse(RelayACircuitBoard.Evaluate(before.Scenario, before.Rotations, false).IsValid);
            host.TestCircuit();
            host.Tick(_config.CircuitTestSeconds + 0.1f);
            var after = host.Snapshot;
            Assert.That(after.Phase, Is.EqualTo(RelayACircuitPhase.Failed));
            Assert.That(after.Powered, Is.Zero);
            Assert.That(after.Rotations, Is.Not.EqualTo(before.Rotations));
            Assert.IsFalse(RelayACircuitBoard.Evaluate(after.Scenario, after.Rotations, false).IsValid);
            for (int cell = 0; cell < after.Scenario.Count; cell++)
                if (after.Scenario.Cells[cell].Locked)
                    Assert.That(after.Rotations[cell], Is.EqualTo(before.Rotations[cell]));
            var proxy = new RelayACircuitSimulation();
            proxy.Initialize(_config, 2);
            proxy.ApplyAuthoritative(2, host.PackedRotations, after.Phase, after.Powered,
                after.MissingTargets, after.FaultPowered, after.TestSequence, after.FaultActive);
            Assert.That(proxy.Snapshot.Rotations, Is.EqualTo(after.Rotations));
        }

        [Test]
        public void UnseededRoutingAndFault_AlwaysRequirePlayerEdits()
        {
            for (int index = 0; index < _config.CircuitScenarios.Count; index++)
            {
                var sim = new RelayACircuitSimulation();
                sim.Initialize(_config, index);
                var state = sim.Snapshot;
                Assert.IsFalse(RelayACircuitBoard.Evaluate(state.Scenario, state.Rotations, false).IsValid);
                for (int cell = 0; cell < state.Scenario.Count; cell++)
                    while (sim.Snapshot.Rotations[cell] != state.Scenario.InitialSolution[cell])
                        Assert.IsTrue(sim.Rotate(cell));
                sim.TestCircuit();
                sim.Tick(_config.CircuitTestSeconds + 0.1f);
                sim.Tick(_config.CircuitFaultDelaySeconds + 0.1f);
                var fault = sim.Snapshot;
                Assert.IsTrue(fault.FaultActive);
                Assert.IsFalse(RelayACircuitBoard.Evaluate(fault.Scenario, fault.Rotations, true).IsValid);
                Assert.IsFalse(sim.Rotate(fault.Scenario.FailedCell));
            }
        }

        [Test]
        public void SeededRouting_StartsUnsolvedPreservesLocksAndVariesAcrossAttempts()
        {
            for (int index = 0; index < _config.CircuitScenarios.Count; index++)
            {
                var board = _config.GetCircuitScenario(index);
                ulong first = 0;
                bool varied = false;
                for (int seed = 1; seed <= 30; seed++)
                {
                    var sim = new RelayACircuitSimulation();
                    sim.Initialize(_config, index, seed);
                    var state = sim.Snapshot;
                    Assert.IsFalse(RelayACircuitBoard.Evaluate(board, state.Rotations, false).IsValid);
                    for (int cell = 0; cell < board.Count; cell++)
                        if (board.Cells[cell].Locked) Assert.That(state.Rotations[cell], Is.EqualTo(board.Cells[cell].Rotation));
                    if (seed == 1) first = sim.PackedRotations;
                    else varied |= sim.PackedRotations != first;
                }
                Assert.IsTrue(varied, board.Name);
            }
        }
    }
}
