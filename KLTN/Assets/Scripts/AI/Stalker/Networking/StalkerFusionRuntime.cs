using System.Collections.Generic;
using EchoProtocol.AI.Common;
using EchoProtocol.Networking;
using Fusion;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StalkerController))]
    [RequireComponent(typeof(StalkerVisionSensor))]
    public sealed class StalkerFusionRuntime : NetworkBehaviour
    {
        [SerializeField] private StalkerController controller;
        [SerializeField] private StalkerVisionSensor visionSensor;
        [SerializeField] private FusionPlayerLifecycle lifecycle;

        private readonly StalkerFusionTargetFrameBuilder _frameBuilder = new StalkerFusionTargetFrameBuilder();
        private readonly List<StalkerPerceptionTargetSnapshot> _perceptionSnapshots =
            new List<StalkerPerceptionTargetSnapshot>();
        private readonly List<StalkerTargetStatus> _targetStatuses =
            new List<StalkerTargetStatus>();
        private readonly List<StalkerTargetCandidate> _visibleCandidates =
            new List<StalkerTargetCandidate>();
        private bool _networkSimulationOwned;
        private AiSimulationStep _lastAuthoritativeStep;

        public int AuthoritativeSimulationCount { get; private set; }
        public bool HasLastAuthoritativeStep => _lastAuthoritativeStep.IsValid;
        public AiSimulationStep LastAuthoritativeStep => _lastAuthoritativeStep;

        private void Awake()
        {
            ResolveLocalDependencies();
        }

        private void OnEnable()
        {
            ResolveLocalDependencies();
            ApplyOwnedLegacySuppression();
        }

        private void OnDisable()
        {
            SetLegacySimulationSuppressed(_networkSimulationOwned);
        }

        public override void Spawned()
        {
            _networkSimulationOwned = true;
            ResolveLocalDependencies();
            ResolveLifecycle();
            SetLegacySimulationSuppressed(true);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _networkSimulationOwned = false;
            lifecycle = null;
            _lastAuthoritativeStep = AiSimulationStep.Invalid;
            SetLegacySimulationSuppressed(false);
        }

        public override void FixedUpdateNetwork()
        {
            ResolveLocalDependencies();

            if (!CanRunAuthoritativeSimulation())
            {
                ClearFrameBuffers();
                return;
            }

            if (!FusionAiSimulationStepAdapter.TryCreate(Runner, out var step))
            {
                ClearFrameBuffers();
                return;
            }

            _lastAuthoritativeStep = step;
            RunAuthoritativePipeline(step);
        }

        private bool CanRunAuthoritativeSimulation()
        {
            if (Runner == null
                || !Runner.IsRunning
                || !Runner.IsServer
                || Object == null
                || !Object.HasStateAuthority
                || controller == null
                || visionSensor == null)
            {
                return false;
            }

            var runnerLifecycle = Runner.GetComponent<FusionPlayerLifecycle>();
            if (lifecycle != runnerLifecycle)
            {
                lifecycle = runnerLifecycle;
            }

            return lifecycle != null;
        }

        private bool RunAuthoritativePipeline(AiSimulationStep step)
        {
            if (!step.IsValid || controller == null || visionSensor == null || lifecycle == null)
            {
                ClearFrameBuffers();
                return false;
            }

            if (!_frameBuilder.TryBuild(
                lifecycle,
                controller.DetectionTargetId,
                controller.CurrentTargetId,
                _perceptionSnapshots,
                _targetStatuses))
            {
                ClearFrameBuffers();
                return false;
            }

            StalkerPerceptionEvaluator.CollectVisibleTargetCandidates(
                visionSensor,
                _perceptionSnapshots,
                step.Time,
                _visibleCandidates);

            var input = new StalkerSimulationInput(
                step,
                _visibleCandidates,
                _targetStatuses);

            if (!controller.Simulate(input))
            {
                return false;
            }

            AuthoritativeSimulationCount++;
            return true;
        }

        private void ResolveLocalDependencies()
        {
            if (controller == null)
            {
                controller = GetComponent<StalkerController>();
            }

            if (visionSensor == null)
            {
                visionSensor = GetComponent<StalkerVisionSensor>();
            }
        }

        private void ResolveLifecycle()
        {
            var runner = Runner;
            if (runner == null)
            {
                lifecycle = null;
                return;
            }

            lifecycle = runner.GetComponent<FusionPlayerLifecycle>();
        }

        private void SetLegacySimulationSuppressed(bool suppressed)
        {
            if (controller != null)
            {
                controller.SuppressLegacyUpdateSimulation = suppressed;
            }
        }

        private void ApplyOwnedLegacySuppression()
        {
            if (_networkSimulationOwned)
            {
                SetLegacySimulationSuppressed(true);
            }
        }

        private void ClearFrameBuffers()
        {
            _perceptionSnapshots.Clear();
            _targetStatuses.Clear();
            _visibleCandidates.Clear();
        }
    }
}
