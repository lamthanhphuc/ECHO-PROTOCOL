using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerNavigationController
    {
        private readonly NavMeshAgent _agent;
        private readonly NavigationProgressMonitor _progressMonitor;
        private readonly NavMeshPath _evaluationPath = new NavMeshPath();
        private Vector3? _activeDestination;

        public StalkerNavigationController(NavMeshAgent agent)
            : this(agent, NavigationProgressSettings.Default)
        {
        }

        public StalkerNavigationController(NavMeshAgent agent, NavigationProgressSettings progressSettings)
        {
            _agent = agent;
            _progressMonitor = new NavigationProgressMonitor(progressSettings);
        }

        public bool IsUsable => _agent != null
            && _agent.enabled
            && _agent.isOnNavMesh;

        // This is the controller's accepted destination cache, not proof of a complete Unity path.
        public bool HasActiveDestination => _activeDestination.HasValue;

        public bool HasArrived()
        {
            return GetPathStatus() == NavigationPathStatus.Complete
                && _agent.remainingDistance <= _agent.stoppingDistance;
        }

        public NavigationEvaluationResult EvaluateDestination(Vector3 destination)
        {
            if (_agent == null || !_agent.enabled)
            {
                return new NavigationEvaluationResult(NavigationEvaluationStatus.AgentUnavailable, destination);
            }

            if (!_agent.isOnNavMesh)
            {
                return new NavigationEvaluationResult(NavigationEvaluationStatus.AgentNotOnNavMesh, destination);
            }

            if (!IsFinite(destination))
            {
                return new NavigationEvaluationResult(NavigationEvaluationStatus.DestinationInvalid, destination);
            }

            if (!_agent.CalculatePath(destination, _evaluationPath))
            {
                return new NavigationEvaluationResult(NavigationEvaluationStatus.Invalid, destination);
            }

            switch (_evaluationPath.status)
            {
                case NavMeshPathStatus.PathComplete:
                    return new NavigationEvaluationResult(NavigationEvaluationStatus.Complete, destination);
                case NavMeshPathStatus.PathPartial:
                    return new NavigationEvaluationResult(NavigationEvaluationStatus.Partial, destination);
                case NavMeshPathStatus.PathInvalid:
                    return new NavigationEvaluationResult(NavigationEvaluationStatus.Invalid, destination);
                default:
                    return new NavigationEvaluationResult(NavigationEvaluationStatus.Invalid, destination);
            }
        }

        public void ClearDestinationCache()
        {
            _activeDestination = null;
            _progressMonitor.Reset();
        }

        public void Stop()
        {
            if (IsUsable)
            {
                _agent.ResetPath();
            }

            ClearDestinationCache();
        }

        public NavigationPlanResult RequestDestination(Vector3 destination)
        {
            return RequestDestination(destination, NavigationRequestIntent.NewGoal);
        }

        public NavigationPlanResult RequestDestination(Vector3 destination, bool forceRepath)
        {
            return RequestDestination(
                destination,
                forceRepath
                    ? NavigationRequestIntent.RecoveryRepath
                    : NavigationRequestIntent.NewGoal);
        }

        public NavigationPlanResult RequestDestination(Vector3 destination, NavigationRequestIntent intent)
        {
            if (_agent == null || !_agent.enabled)
            {
                ClearDestinationCache();
                return new NavigationPlanResult(NavigationPlanStatus.AgentUnavailable, destination);
            }

            if (!_agent.isOnNavMesh)
            {
                ClearDestinationCache();
                return new NavigationPlanResult(NavigationPlanStatus.AgentNotOnNavMesh, destination);
            }

            // RecoveryRepath bypasses the cache but still only issues a path request.
            if (intent != NavigationRequestIntent.RecoveryRepath
                && _activeDestination.HasValue
                && _activeDestination.Value == destination)
            {
                return new NavigationPlanResult(NavigationPlanStatus.AlreadyActive, destination);
            }

            // SetDestination accepts a request; it does not prove the resulting path is complete.
            if (!_agent.SetDestination(destination))
            {
                ClearDestinationCache();
                return new NavigationPlanResult(NavigationPlanStatus.DestinationRequestFailed, destination);
            }

            _activeDestination = destination;
            if (intent == NavigationRequestIntent.NewGoal
                || intent == NavigationRequestIntent.RecoveryRepath)
            {
                _progressMonitor.Reset();
            }

            return new NavigationPlanResult(NavigationPlanStatus.Accepted, destination);
        }

        public bool TrySetDestination(Vector3 destination)
        {
            return RequestDestination(destination).IsAccepted;
        }

        public void TickProgress(float deltaTime)
        {
            var pathStatus = GetPathStatus();
            if (pathStatus == NavigationPathStatus.Pending)
            {
                return;
            }

            if (pathStatus != NavigationPathStatus.Complete)
            {
                _progressMonitor.Reset();
                return;
            }

            if (HasArrived())
            {
                _progressMonitor.Reset();
                return;
            }

            _progressMonitor.Observe(
                _agent.transform.position,
                _agent.remainingDistance,
                deltaTime);
        }

        public NavigationExecutionStatus GetExecutionStatus()
        {
            switch (GetPathStatus())
            {
                case NavigationPathStatus.AgentUnavailable:
                case NavigationPathStatus.AgentNotOnNavMesh:
                case NavigationPathStatus.Stale:
                case NavigationPathStatus.Partial:
                case NavigationPathStatus.Invalid:
                    return NavigationExecutionStatus.Failed;
                case NavigationPathStatus.NoDestination:
                    return NavigationExecutionStatus.Idle;
                case NavigationPathStatus.Pending:
                    return NavigationExecutionStatus.RepathPending;
                case NavigationPathStatus.Complete:
                    if (HasArrived())
                    {
                        return NavigationExecutionStatus.Arrived;
                    }

                    switch (_progressMonitor.State)
                    {
                        case NavigationProgressState.Stuck:
                            return NavigationExecutionStatus.Stuck;
                        case NavigationProgressState.NoProgress:
                            return NavigationExecutionStatus.NoProgress;
                        case NavigationProgressState.Moving:
                            return NavigationExecutionStatus.Moving;
                        default:
                            return NavigationExecutionStatus.Moving;
                    }
                default:
                    return NavigationExecutionStatus.Failed;
            }
        }

        public NavigationPathStatus GetPathStatus()
        {
            if (_agent == null || !_agent.enabled)
            {
                return NavigationPathStatus.AgentUnavailable;
            }

            if (!_agent.isOnNavMesh)
            {
                return NavigationPathStatus.AgentNotOnNavMesh;
            }

            if (!_activeDestination.HasValue)
            {
                return NavigationPathStatus.NoDestination;
            }

            if (_agent.pathPending)
            {
                return NavigationPathStatus.Pending;
            }

            // Observational only; recovery policy belongs to the caller.
            if (_agent.isPathStale)
            {
                return NavigationPathStatus.Stale;
            }

            switch (_agent.pathStatus)
            {
                case NavMeshPathStatus.PathComplete:
                    return NavigationPathStatus.Complete;
                case NavMeshPathStatus.PathPartial:
                    return NavigationPathStatus.Partial;
                case NavMeshPathStatus.PathInvalid:
                    return NavigationPathStatus.Invalid;
                default:
                    return NavigationPathStatus.Invalid;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
