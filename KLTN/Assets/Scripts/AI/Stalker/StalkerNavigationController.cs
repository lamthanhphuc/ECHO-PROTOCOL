using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerNavigationController
    {
        private readonly NavMeshAgent _agent;
        private readonly NavigationProgressMonitor _progressMonitor;
        private readonly NavMeshPath _evaluationPath = new NavMeshPath();
        private readonly float _pathPendingTimeoutSeconds;
        private Vector3? _activeDestination;
        private float _pathPendingElapsedSeconds;
        private bool _pathPendingTimedOut;
        private NavigationFailureReason _currentFailureReason;
        private NavigationRecoveryReason _currentRecoveryReason;

        public StalkerNavigationController(NavMeshAgent agent)
            : this(agent, NavigationProgressSettings.Default)
        {
        }

        public StalkerNavigationController(NavMeshAgent agent, NavigationProgressSettings progressSettings)
            : this(agent, progressSettings, 1f)
        {
        }

        public StalkerNavigationController(
            NavMeshAgent agent,
            NavigationProgressSettings progressSettings,
            float pathPendingTimeoutSeconds)
        {
            _agent = agent;
            _progressMonitor = new NavigationProgressMonitor(progressSettings);
            _pathPendingTimeoutSeconds = Mathf.Max(0.01f, pathPendingTimeoutSeconds);
        }

        public bool IsUsable => _agent != null
            && _agent.enabled
            && _agent.isOnNavMesh;

        // This is the controller's accepted destination cache, not proof of a complete Unity path.
        public bool HasActiveDestination => _activeDestination.HasValue;

        public NavigationFailureReason CurrentFailureReason => _currentFailureReason;

        public NavigationRecoveryReason CurrentRecoveryReason => _currentRecoveryReason;

        public void RecordRecoveryReason(NavigationRecoveryReason reason)
        {
            _currentRecoveryReason = reason;
        }

        public bool HasArrived()
        {
            return GetPathStatus() == NavigationPathStatus.Complete
                && _agent.remainingDistance <= _agent.stoppingDistance;
        }

        public NavigationEvaluationResult EvaluateDestination(Vector3 destination)
        {
            if (_agent == null || !_agent.enabled)
            {
                _currentFailureReason = NavigationFailureReason.AgentUnavailable;
                return new NavigationEvaluationResult(NavigationEvaluationStatus.AgentUnavailable, destination);
            }

            if (!_agent.isOnNavMesh)
            {
                _currentFailureReason = NavigationFailureReason.AgentNotOnNavMesh;
                return new NavigationEvaluationResult(NavigationEvaluationStatus.AgentNotOnNavMesh, destination);
            }

            if (!IsFinite(destination))
            {
                _currentFailureReason = NavigationFailureReason.DestinationInvalid;
                return new NavigationEvaluationResult(NavigationEvaluationStatus.DestinationInvalid, destination);
            }

            if (!_agent.CalculatePath(destination, _evaluationPath))
            {
                return new NavigationEvaluationResult(NavigationEvaluationStatus.Invalid, destination);
            }

            switch (_evaluationPath.status)
            {
                case NavMeshPathStatus.PathComplete:
                    _currentFailureReason = NavigationFailureReason.None;
                    return new NavigationEvaluationResult(NavigationEvaluationStatus.Complete, destination);
                case NavMeshPathStatus.PathPartial:
                    _currentFailureReason = NavigationFailureReason.PathPartial;
                    return new NavigationEvaluationResult(NavigationEvaluationStatus.Partial, destination);
                case NavMeshPathStatus.PathInvalid:
                    _currentFailureReason = NavigationFailureReason.PathInvalid;
                    return new NavigationEvaluationResult(NavigationEvaluationStatus.Invalid, destination);
                default:
                    _currentFailureReason = NavigationFailureReason.PathInvalid;
                    return new NavigationEvaluationResult(NavigationEvaluationStatus.Invalid, destination);
            }
        }

        public void ClearDestinationCache()
        {
            _activeDestination = null;
            _pathPendingElapsedSeconds = 0f;
            _pathPendingTimedOut = false;
            _currentFailureReason = NavigationFailureReason.None;
            _currentRecoveryReason = NavigationRecoveryReason.None;
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
                _currentFailureReason = NavigationFailureReason.AgentUnavailable;
                return new NavigationPlanResult(NavigationPlanStatus.AgentUnavailable, destination);
            }

            if (!_agent.isOnNavMesh)
            {
                ClearDestinationCache();
                _currentFailureReason = NavigationFailureReason.AgentNotOnNavMesh;
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
                _currentFailureReason = NavigationFailureReason.DestinationInvalid;
                return new NavigationPlanResult(NavigationPlanStatus.DestinationRequestFailed, destination);
            }

            _activeDestination = destination;
            _pathPendingElapsedSeconds = 0f;
            _pathPendingTimedOut = false;
            _currentFailureReason = NavigationFailureReason.None;
            _currentRecoveryReason = intent == NavigationRequestIntent.RecoveryRepath
                ? NavigationRecoveryReason.RetryLogicalObjective
                : NavigationRecoveryReason.None;
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
                if (deltaTime > 0f)
                {
                    _pathPendingElapsedSeconds += deltaTime;
                    if (_pathPendingElapsedSeconds >= _pathPendingTimeoutSeconds)
                    {
                        _pathPendingTimedOut = true;
                        _currentFailureReason = NavigationFailureReason.PathPendingTimeout;
                    }
                }

                return;
            }

            if (pathStatus != NavigationPathStatus.Complete)
            {
                _currentFailureReason = MapPathFailureReason(pathStatus);
                _progressMonitor.Reset();
                return;
            }

            _pathPendingElapsedSeconds = 0f;
            _pathPendingTimedOut = false;
            if (HasArrived())
            {
                _currentFailureReason = NavigationFailureReason.None;
                _progressMonitor.Reset();
                return;
            }

            _progressMonitor.Observe(
                _agent.transform.position,
                _agent.remainingDistance,
                deltaTime);
            _currentFailureReason = MapProgressFailureReason(_progressMonitor.State);
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
                    if (_pathPendingTimedOut)
                    {
                        return NavigationExecutionStatus.Failed;
                    }

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

        private static NavigationFailureReason MapPathFailureReason(NavigationPathStatus status)
        {
            switch (status)
            {
                case NavigationPathStatus.AgentUnavailable:
                    return NavigationFailureReason.AgentUnavailable;
                case NavigationPathStatus.AgentNotOnNavMesh:
                    return NavigationFailureReason.AgentNotOnNavMesh;
                case NavigationPathStatus.Partial:
                    return NavigationFailureReason.PathPartial;
                case NavigationPathStatus.Invalid:
                    return NavigationFailureReason.PathInvalid;
                case NavigationPathStatus.Stale:
                    return NavigationFailureReason.PathStale;
                default:
                    return NavigationFailureReason.None;
            }
        }

        private static NavigationFailureReason MapProgressFailureReason(NavigationProgressState state)
        {
            switch (state)
            {
                case NavigationProgressState.NoProgress:
                    return NavigationFailureReason.NoProgress;
                case NavigationProgressState.Stuck:
                    return NavigationFailureReason.Stuck;
                default:
                    return NavigationFailureReason.None;
            }
        }
    }
}
