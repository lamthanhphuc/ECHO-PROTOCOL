using System;
using UnityEngine;

namespace EchoProtocol.RelayA
{
    public enum RelayACircuitPhase : byte
    {
        Editing, Testing, Failed, Stable, Faulted, Online, StabilizeOutput, BreakerMatrix
    }

    public readonly struct RelayACircuitSnapshot
    {
        public RelayACircuitSnapshot(RelayACircuitScenario scenario, int scenarioIndex, int[] rotations,
            RelayACircuitPhase phase, ulong powered, int missingTargets, bool faultPowered, int testSequence, bool faultActive)
        {
            Scenario = scenario;
            ScenarioIndex = scenarioIndex;
            Rotations = rotations != null ? (int[])rotations.Clone() : Array.Empty<int>();
            Phase = phase;
            Powered = powered;
            MissingTargets = missingTargets;
            FaultPowered = faultPowered;
            TestSequence = testSequence;
            FaultActive = faultActive;
        }

        public RelayACircuitScenario Scenario { get; }
        public int ScenarioIndex { get; }
        public int[] Rotations { get; }
        public RelayACircuitPhase Phase { get; }
        public ulong Powered { get; }
        public int MissingTargets { get; }
        public bool FaultPowered { get; }
        public int TestSequence { get; }
        public bool IsOnline => Phase == RelayACircuitPhase.Online;
        public bool FaultActive { get; }
    }

    public sealed class RelayACircuitSimulation
    {
        private RelayAConfig _config;
        private RelayACircuitScenario _scenario;
        private int _scenarioIndex;
        private int[] _rotations = Array.Empty<int>();
        private RelayACircuitPhase _phase;
        private RelayACircuitResult _testResult;
        private float _elapsed;
        private ulong _powered;
        private int _missingTargets;
        private bool _faultPowered;
        private int _testSequence;
        private System.Random _routingRandom;

        public event Action<RelayACircuitSnapshot> Changed;
        public event Action Completed;
        public event Action ProtectionTripped;
        public event Action FaultActivated;

        public RelayAStabilizationSimulation Stabilization { get; } = new RelayAStabilizationSimulation();
        public RelayABreakerMatrix Breakers { get; } = new RelayABreakerMatrix();

        public RelayACircuitSimulation()
        {
            Breakers.Changed += () => { if (_phase == RelayACircuitPhase.BreakerMatrix) Notify(); };
            Breakers.Completed += () =>
            {
                if (_phase != RelayACircuitPhase.BreakerMatrix) return;
                _phase = RelayACircuitPhase.StabilizeOutput;
                Notify();
            };
            Stabilization.Changed += _ =>
            {
                if (_phase == RelayACircuitPhase.StabilizeOutput || _phase == RelayACircuitPhase.Online) Notify();
            };
            Stabilization.Completed += () =>
            {
                if (_phase != RelayACircuitPhase.StabilizeOutput) return;
                _phase = RelayACircuitPhase.Online;
                Notify();
                Completed?.Invoke();
            };
        }

        public RelayACircuitSnapshot Snapshot => new RelayACircuitSnapshot(_scenario, _scenarioIndex, _rotations,
            _phase, _powered, _missingTargets, _faultPowered, _testSequence, _faultWasActivated);
        public bool IsOnline => _phase == RelayACircuitPhase.Online;
        public bool IsTesting => _phase == RelayACircuitPhase.Testing;
        public ulong PackedRotations
        {
            get
            {
                ulong packed = 0;
                for (int i = 0; i < _rotations.Length; i++) packed |= (ulong)(_rotations[i] & 3) << (2 * i);
                return packed;
            }
        }

        public void Initialize(RelayAConfig config, int scenarioIndex, int attemptSeed = 0)
        {
            _config = config;
            _scenarioIndex = scenarioIndex;
            _scenario = config != null ? config.GetCircuitScenario(scenarioIndex) : null;
            if (_scenario != null && !RelayACircuitBoard.HasAuthoredSolutions(_scenario))
                throw new ArgumentException("Circuit must have playable initial and post-fault solutions.");
            _rotations = _scenario != null ? new int[_scenario.Count] : Array.Empty<int>();
            _faultWasActivated = false;
            for (int i = 0; i < _rotations.Length; i++) _rotations[i] = _scenario.Cells[i].Rotation;
            _routingRandom = new System.Random(attemptSeed != 0 ? attemptSeed : Environment.TickCount);
            for (int cell = 0; cell < _rotations.Length; cell++)
                if (!_scenario.Cells[cell].Locked) _rotations[cell] = _scenario.InitialSolution[cell];
            ScrambleRouting();
            _phase = RelayACircuitPhase.Editing;
            _powered = 0;
            _missingTargets = 0;
            _faultPowered = false;
            _elapsed = 0;
            _testSequence = 0;
            Stabilization.Initialize(config, true, attemptSeed);
            Breakers.Initialize(scenarioIndex >= 2, attemptSeed);
            Notify();
        }

        public bool Rotate(int cellIndex)
        {
            if (_scenario == null || cellIndex < 0 || cellIndex >= _rotations.Length || _scenario.Cells[cellIndex].Locked
                || _phase == RelayACircuitPhase.Testing || _phase == RelayACircuitPhase.Stable
                || _phase == RelayACircuitPhase.Online || _phase == RelayACircuitPhase.StabilizeOutput || _phase == RelayACircuitPhase.BreakerMatrix
                || (_faultWasActivated && cellIndex == _scenario.FailedCell))
                return false;
            _rotations[cellIndex] = (_rotations[cellIndex] + 1) & 3;
            _powered = 0;
            _missingTargets = 0;
            _faultPowered = false;
            if (_phase == RelayACircuitPhase.Failed) _phase = RelayACircuitPhase.Editing;
            Notify();
            return true;
        }

        public bool TestCircuit()
        {
            if (_scenario == null || _phase == RelayACircuitPhase.Testing || _phase == RelayACircuitPhase.Stable
                || _phase == RelayACircuitPhase.Online || _phase == RelayACircuitPhase.StabilizeOutput || _phase == RelayACircuitPhase.BreakerMatrix) return false;
            _testResult = RelayACircuitBoard.Evaluate(_scenario, _rotations, _faultWasActivated);
            _phase = RelayACircuitPhase.Testing;
            _elapsed = 0;
            _powered = 0;
            _testSequence++;
            Notify();
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (_phase == RelayACircuitPhase.BreakerMatrix)
            {
                Breakers.Tick(deltaTime);
                return;
            }
            if (_phase == RelayACircuitPhase.StabilizeOutput)
            {
                Stabilization.Tick(deltaTime);
                return;
            }
            if (_phase == RelayACircuitPhase.Testing)
            {
                _elapsed += Mathf.Max(0f, deltaTime);
                float duration = _config != null ? _config.CircuitTestSeconds : 1.2f;
                int maxDepth = 0;
                foreach (int depth in _testResult.Depth) maxDepth = Mathf.Max(maxDepth, depth);
                int visibleDepth = Mathf.FloorToInt((_elapsed / duration) * (maxDepth + 1));
                ulong visible = 0;
                for (int i = 0; i < _testResult.Depth.Length; i++)
                    if (_testResult.Depth[i] >= 0 && _testResult.Depth[i] <= visibleDepth) visible |= 1UL << i;
                if (_powered != visible) { _powered = visible; Notify(); }
                if (_elapsed < duration) return;

                _powered = _testResult.Powered;
                _missingTargets = _testResult.MissingTargets;
                _faultPowered = _testResult.FaultPowered;
                _elapsed = 0;
                if (!_testResult.IsValid)
                {
                    _phase = RelayACircuitPhase.Failed;
                    if (_faultPowered) ProtectionTripped?.Invoke();
                    ScrambleRouting();
                    _powered = 0;
                    _missingTargets = 0;
                }
                else
                {
                    if (_faultWasActivated)
                    {
                        _phase = RelayACircuitPhase.BreakerMatrix;
                    }
                    else _phase = RelayACircuitPhase.Stable;
                }
                Notify();
            }
            else if (_phase == RelayACircuitPhase.Stable)
            {
                _elapsed += Mathf.Max(0f, deltaTime);
                if (_elapsed < (_config != null ? _config.CircuitFaultDelaySeconds : 1.2f)) return;
                SelectLiveFault();
                _faultWasActivated = true;
                ScrambleRouting();
                _phase = RelayACircuitPhase.Faulted;
                _powered = 0;
                _elapsed = 0;
                FaultActivated?.Invoke();
                Notify();
            }
        }

        private bool _faultWasActivated;

        private void SelectLiveFault()
        {
            var choices = new System.Collections.Generic.List<RelayACircuitScenario>();
            var redundantChoices = new System.Collections.Generic.List<RelayACircuitScenario>();
            var authored = _config.GetCircuitScenario(_scenarioIndex);
            ulong live = RelayACircuitBoard.Evaluate(_scenario, _rotations, false).Powered;
            for (int cell = 0; cell < _scenario.Count; cell++)
            {
                if (_scenario.Cells[cell].Locked || (live & (1UL << cell)) == 0
                    || _scenario.Cells[cell].Type == RelayACircuitTile.Source) continue;
                foreach (var bypass in new[] { authored.InitialSolution, authored.FaultSolution })
                {
                    var candidate = new RelayACircuitScenario(_scenario.Name, _scenario.Width, _scenario.Height,
                        _scenario.Cells, cell, (int[])_rotations.Clone(), (int[])bypass.Clone());
                    if (!RelayACircuitBoard.Evaluate(candidate, bypass, true).IsValid) continue;
                    if (RelayACircuitBoard.HasAuthoredSolutions(candidate)) choices.Add(candidate);
                    else redundantChoices.Add(candidate);
                    break;
                }
            }
            if (choices.Count == 0) choices = redundantChoices;
            if (choices.Count == 0) throw new InvalidOperationException("No validated bypass for the live route.");
            _scenario = choices[_routingRandom.Next(choices.Count)];
        }

        public bool PressBreaker(int cell) => _phase == RelayACircuitPhase.BreakerMatrix && Breakers.Press(cell);
        public bool ResetBreakers() => _phase == RelayACircuitPhase.BreakerMatrix && Breakers.Reset();

        private void ScrambleRouting()
        {
            if (_scenario == null) return;
            for (int cell = 0; cell < _rotations.Length; cell++)
            {
                if (_scenario.Cells[cell].Locked || _faultWasActivated && cell == _scenario.FailedCell) continue;
                int mask = RelayACircuitBoard.Connections(_scenario.Cells[cell].Type, _rotations[cell]);
                int start = _routingRandom.Next(1, 4);
                for (int offset = 0; offset < 3; offset++)
                {
                    int rotation = (_rotations[cell] + 1 + (start + offset) % 3) & 3;
                    if (RelayACircuitBoard.Connections(_scenario.Cells[cell].Type, rotation) == mask) continue;
                    _rotations[cell] = rotation;
                    break;
                }
            }
            // A retry is always a puzzle; do not accidentally grant a solved random board.
            if (RelayACircuitBoard.Evaluate(_scenario, _rotations, _faultWasActivated).IsValid)
            {
                var solution = _faultWasActivated ? _scenario.FaultSolution : _scenario.InitialSolution;
                for (int cell = 0; cell < _rotations.Length; cell++)
                    if (!_scenario.Cells[cell].Locked && (!_faultWasActivated || cell != _scenario.FailedCell))
                        _rotations[cell] = (solution[cell] + 1) & 3;
            }
        }

        public bool SetStabilizationControls(float generator, float frequency, float load)
        {
            if (_phase != RelayACircuitPhase.StabilizeOutput
                || !float.IsFinite(generator) || !float.IsFinite(frequency) || !float.IsFinite(load)) return false;
            Stabilization.SetControls(generator, frequency, load);
            return true;
        }

        public bool SetStabilizationRunning(bool running)
        {
            if (_phase != RelayACircuitPhase.StabilizeOutput || Stabilization.Snapshot.IsRunning == running) return false;
            if (running) Stabilization.Start();
            else Stabilization.EmergencyStop();
            return true;
        }

        public void ApplyAuthoritative(int scenarioIndex, ulong packedRotations, RelayACircuitPhase phase,
            ulong powered, int missingTargets, bool faultPowered, int testSequence, bool faultActive, int failedCell = -1)
        {
            if (_config == null) return;
            if (_scenario != null && _scenarioIndex == scenarioIndex && PackedRotations == packedRotations
                && _phase == phase && _powered == powered && _missingTargets == missingTargets
                && _faultPowered == faultPowered && _testSequence == testSequence && _faultWasActivated == faultActive
                && (failedCell < 0 || _scenario.FailedCell == failedCell))
                return;
            if (_scenario == null || _scenarioIndex != scenarioIndex) Initialize(_config, scenarioIndex);
            if (failedCell >= 0 && failedCell < _scenario.Count && _scenario.FailedCell != failedCell)
                _scenario = new RelayACircuitScenario(_scenario.Name, _scenario.Width, _scenario.Height,
                    _scenario.Cells, failedCell, _scenario.InitialSolution, _scenario.FaultSolution);
            for (int i = 0; i < _rotations.Length; i++) _rotations[i] = (int)((packedRotations >> (2 * i)) & 3UL);
            _phase = phase;
            _faultWasActivated = faultActive;
            _powered = powered;
            _missingTargets = missingTargets;
            _faultPowered = faultPowered;
            _testSequence = testSequence;
            Notify();
        }

        public void ForceOnline()
        {
            if (_phase == RelayACircuitPhase.Online) return;
            _phase = RelayACircuitPhase.Online;
            Stabilization.ForceCompleteForAuthoritativeSync();
            Notify();
        }

        private void Notify() => Changed?.Invoke(Snapshot);
    }
}
