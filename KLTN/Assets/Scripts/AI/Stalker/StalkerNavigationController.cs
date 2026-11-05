using System;
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
        private bool _authoritativeLocomotion;
        private Func<Vector3[], bool> _pathSafetyValidator;
        private Vector3? _lastOnNavMeshPosition;

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

        public void SetPathSafetyValidator(Func<Vector3[], bool> validator)
        {
            _pathSafetyValidator = validator;
        }

        public bool HasAuthoritativeLocomotion => _authoritativeLocomotion;
        public bool TryRecoverLocalPosition(Vector3 destination)
        {
            if (!TryFindLocalRecoveryPosition(destination, out var position)) return false;
            bool wasEnabled = _agent.enabled;
            if (!wasEnabled) _agent.enabled = true;
            if (!_agent.Warp(position))
            { if (!wasEnabled) _agent.enabled = false; return false; }
            _agent.transform.position = position;
            _progressMonitor.Reset();
            RememberOnNavMeshPosition();
            return true;
        }

        private bool TryFindLocalRecoveryPosition(Vector3 destination, out Vector3 position)
        {
            position = default;
            if (_agent == null) return false;
            var origin = _agent.transform.position;
            var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
            var body = _agent.GetComponent<CapsuleCollider>();
            var scale = _agent.transform.lossyScale;
            float radius = body != null ? body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : _agent.radius;
            float height = body != null ? body.height * Mathf.Abs(scale.y) : _agent.height;
            var center = body != null ? _agent.transform.TransformPoint(body.center) : origin + Vector3.up * height * .5f;
            float segment = Mathf.Max(0f, height * .5f - radius);
            bool backfaces = Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces = true;
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (i < 8 ? .6f : 1.2f);
                    if (!NavMesh.SamplePosition(origin + offset, out var hit, .25f, filter)
                        || Mathf.Abs(hit.position.y - origin.y) > .75f
                        || Vector3.Distance(origin, hit.position) > 1.25f
                        || Vector3.Distance(origin, hit.position) < .15f
                        || !NavMesh.CalculatePath(hit.position, destination, filter, _evaluationPath)
                        || _evaluationPath.status != NavMeshPathStatus.PathComplete) continue;
                    var delta = hit.position - origin;
                    bool blocked = false;
                    foreach (var collision in Physics.CapsuleCastAll(center - Vector3.up * segment,
                                 center + Vector3.up * segment, radius, delta.normalized, delta.magnitude + .05f,
                                 Physics.DefaultRaycastLayers & ~(1 << 4), QueryTriggerInteraction.Ignore))
                    {
                        if (!collision.transform.IsChildOf(_agent.transform)
                            && IsMovementHitBlocking(collision, body, origin, delta))
                        { blocked = true; break; }
                    }
                    // Only bridge missing navigation over continuous floor, never water or a drop.
                    for (float t = 0f; !blocked && t <= 1.001f; t += .125f)
                        blocked = !Physics.Raycast(Vector3.Lerp(origin, hit.position, t) + Vector3.up * .8f,
                            Vector3.down, 1.1f, Physics.DefaultRaycastLayers & ~(1 << 4), QueryTriggerInteraction.Ignore);
                    if (blocked) continue;
                    position = hit.position;
                    return true;
                }
                return false;
            }
            finally { Physics.queriesHitBackfaces = backfaces; }
        }

        public float AuthoritativeMoveSpeed { get; private set; }

        internal static bool IsMovementHitBlocking(RaycastHit hit, CapsuleCollider body, Vector3 bodyPosition, Vector3 motion)
        {
            // Initial-overlap cast normals point against the cast, even when the overlap is just the floor.
            if (hit.distance <= .00001f && body != null
                && Physics.ComputePenetration(body, bodyPosition, body.transform.rotation,
                    hit.collider, hit.transform.position, hit.transform.rotation, out var separation, out float depth))
            {
                if (separation.y >= .65f)
                {
                    var scale = body.transform.lossyScale;
                    float radius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    float segment = Mathf.Max(0f, body.height * Mathf.Abs(scale.y) * .5f - radius);
                    var center = bodyPosition + body.transform.TransformVector(body.center) + separation * (depth + .01f);
                    // A floor and a wall can share one mesh: query again above the initial floor overlap.
                    foreach (var raisedHit in Physics.CapsuleCastAll(center - Vector3.up * segment, center + Vector3.up * segment,
                                 radius, motion.normalized, motion.magnitude + .05f,
                                 Physics.DefaultRaycastLayers & ~(1 << 4), QueryTriggerInteraction.Ignore))
                        if (raisedHit.collider == hit.collider && raisedHit.normal.y < .65f) return true;
                    return false;
                }
                if (Vector3.Dot(motion, separation) < -.00001f) return true;
                return Physics.ComputePenetration(body, bodyPosition + motion, body.transform.rotation,
                    hit.collider, hit.transform.position, hit.transform.rotation, out _, out float nextDepth)
                    && nextDepth > depth + .0001f;
            }
            return hit.normal.y < .65f;
        }

        public void SetAuthoritativeLocomotion(bool enabled)
        {
            if (_agent == null || _authoritativeLocomotion == enabled)
            {
                return;
            }

            _authoritativeLocomotion = enabled;
            AuthoritativeMoveSpeed = 0f;
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.nextPosition = _agent.transform.position;
            }

            _agent.updatePosition = !enabled;
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = enabled;
            }
        }

        public void TickAuthoritativeLocomotion(float deltaSeconds)
        {
            AuthoritativeMoveSpeed = 0f;
            RememberOnNavMeshPosition();
            if (!_authoritativeLocomotion || !IsUsable || deltaSeconds <= 0f
                || !_agent.hasPath || _agent.pathPending
                || _agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                return;
            }

            if (!_agent.isStopped)
            {
                _agent.isStopped = true;
            }

            var position = _agent.transform.position;
            var toCorner = _agent.steeringTarget - position;
            toCorner.y = 0f;
            var distance = Mathf.Min(
                Mathf.Min(toCorner.magnitude,
                    Mathf.Max(0f, _agent.remainingDistance - _agent.stoppingDistance)),
                _agent.speed * deltaSeconds);
            if (distance <= 0f)
            {
                return;
            }

            var direction = toCorner.normalized;

            if (direction == Vector3.zero)
            {
                return;
            }

            _agent.nextPosition = position;
            _agent.Move(direction * distance);
            _agent.transform.position = _agent.nextPosition;
            RememberOnNavMeshPosition();
            AuthoritativeMoveSpeed = Vector3.Distance(position, _agent.transform.position) / deltaSeconds;
            if (_agent.updateRotation)
            {
                _agent.transform.rotation = Quaternion.RotateTowards(
                    _agent.transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up),
                    _agent.angularSpeed * deltaSeconds);
            }
        }

        // This is the controller's accepted destination cache, not proof of a complete Unity path.
        public bool HasActiveDestination => _activeDestination.HasValue;

        public NavigationFailureReason CurrentFailureReason => _currentFailureReason;

        public NavigationRecoveryReason CurrentRecoveryReason => _currentRecoveryReason;

        public bool TryGetActiveDestination(out Vector3 destination)
        {
            if (_activeDestination.HasValue)
            {
                destination = _activeDestination.Value;
                return true;
            }

            destination = default;
            return false;
        }

        public bool TryGetCurrentPathCorners(out Vector3[] corners)
        {
            if (_agent != null && _agent.enabled && _agent.hasPath)
            {
                corners = _agent.path.corners;
                return corners != null && corners.Length >= 2;
            }

            corners = null;
            return false;
        }

        public void RecordRecoveryReason(NavigationRecoveryReason reason)
        {
            _currentRecoveryReason = reason;
        }

        public bool TryReattachToNearestNavMesh(float maxSampleDistance)
        {
            if (_agent == null || !_agent.enabled)
            {
                _currentFailureReason = NavigationFailureReason.AgentUnavailable;
                return false;
            }

            if (_agent.isOnNavMesh)
            {
                RememberOnNavMeshPosition();
                _currentFailureReason = NavigationFailureReason.None;
                return true;
            }

            var sampleDistance = Mathf.Max(0.01f, maxSampleDistance);
            if (!NavMesh.SamplePosition(
                    _agent.transform.position,
                    out var hit,
                    sampleDistance,
                    _agent.areaMask)
                && (!_lastOnNavMeshPosition.HasValue
                    || Vector3.Distance(_agent.transform.position, _lastOnNavMeshPosition.Value) > sampleDistance
                    || !NavMesh.SamplePosition(_lastOnNavMeshPosition.Value, out hit, 0.1f, _agent.areaMask)))
            {
                _currentFailureReason = NavigationFailureReason.AgentNotOnNavMesh;
                return false;
            }

            if (!_agent.Warp(hit.position) || !_agent.isOnNavMesh)
            {
                _currentFailureReason = NavigationFailureReason.AgentNotOnNavMesh;
                return false;
            }

            if (_authoritativeLocomotion)
            {
                _agent.transform.position = _agent.nextPosition;
                _agent.isStopped = true;
            }

            _pathPendingElapsedSeconds = 0f;
            _pathPendingTimedOut = false;
            _currentFailureReason = NavigationFailureReason.None;
            _progressMonitor.Reset();
            RememberOnNavMeshPosition();

            return true;
        }

        public bool HasArrived()
        {
            if (GetPathStatus() != NavigationPathStatus.Complete
                || !_activeDestination.HasValue)
            {
                return false;
            }

            if (!DoesPathReachRequestedDestination(
                    _agent.path,
                    _activeDestination.Value))
            {
                return false;
            }

            return _agent.remainingDistance <= _agent.stoppingDistance + .01f;
        }

        public NavigationEvaluationResult EvaluateDestination(Vector3 destination)
        {
            return EvaluateDestination(destination, out _);
        }

        public NavigationEvaluationResult EvaluateDestination(Vector3 destination, out NavMeshPath path)
        {
            path = _evaluationPath;
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
                _currentFailureReason = NavigationFailureReason.PathInvalid;
                return new NavigationEvaluationResult(NavigationEvaluationStatus.Invalid, destination);
            }

            switch (_evaluationPath.status)
            {
                case NavMeshPathStatus.PathComplete:
                    if (!DoesPathReachRequestedDestination(
                            _evaluationPath,
                            destination))
                    {
                        _currentFailureReason = NavigationFailureReason.PathPartial;
                        return new NavigationEvaluationResult(
                            NavigationEvaluationStatus.Partial,
                            destination);
                    }

                    if (!IsPathSafe(_evaluationPath))
                    {
                        _currentFailureReason = NavigationFailureReason.PathInvalid;
                        return new NavigationEvaluationResult(NavigationEvaluationStatus.Invalid, destination);
                    }

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

            if (_pathSafetyValidator != null &&
                (!_agent.CalculatePath(destination, _evaluationPath) ||
                 _evaluationPath.status != NavMeshPathStatus.PathComplete ||
                 !DoesPathReachRequestedDestination(_evaluationPath, destination) ||
                 !IsPathSafe(_evaluationPath)))
            {
                _currentFailureReason = NavigationFailureReason.PathInvalid;
                return new NavigationPlanResult(NavigationPlanStatus.DestinationRequestFailed, destination);
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
            if (intent != NavigationRequestIntent.RecoveryRepath)
            {
                _currentRecoveryReason = NavigationRecoveryReason.None;
            }
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
            RememberOnNavMeshPosition();
            if (_pathSafetyValidator != null && IsUsable && _agent.hasPath && !_agent.pathPending &&
                _agent.pathStatus == NavMeshPathStatus.PathComplete && !IsPathSafe(_agent.path))
            {
                _agent.ResetPath();
                ClearDestinationCache();
                _currentFailureReason = NavigationFailureReason.PathInvalid;
                return;
            }

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

        private void RememberOnNavMeshPosition()
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                _lastOnNavMeshPosition = _agent.nextPosition;
        }

        private bool DoesPathReachRequestedDestination(
            NavMeshPath path,
            Vector3 requestedDestination)
        {
            if (path == null)
            {
                return false;
            }

            var corners = path.corners;
            if (corners == null || corners.Length == 0)
            {
                return false;
            }

            var endpoint = corners[corners.Length - 1];

            // A "complete" Unity path may terminate at a NavMesh projection
            // instead of the requested world-space point. Do not treat a
            // different floor/level as the requested logical destination.
            var endpointTolerance = Mathf.Max(
                0.01f,
                Mathf.Max(_agent.radius, _agent.stoppingDistance));

            return Vector3.Distance(endpoint, requestedDestination)
                <= endpointTolerance;
        }

        private bool IsPathSafe(NavMeshPath path) =>
            _pathSafetyValidator == null || path != null && _pathSafetyValidator(path.corners);

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
