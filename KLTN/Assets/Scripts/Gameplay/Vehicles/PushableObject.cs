using UnityEngine;

namespace EchoProtocol.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PushableObject : MonoBehaviour, IHoldInteractable
    {
        [Header("Push Tuning")]
        [Tooltip("Target linear speed in m/s when pushing actively.")]
        [SerializeField] private float pushSpeed = 1.4f;

        [Tooltip("Linear acceleration rate (m/s^2).")]
        [SerializeField] private float acceleration = 2.0f;

        [Tooltip("Linear deceleration rate when stopping or releasing push (m/s^2).")]
        [SerializeField] private float deceleration = 4.0f;

        [Tooltip("Maximum turning speed in degrees per second.")]
        [SerializeField] private float maxTurnSpeed = 25.0f;

        [Tooltip("Dead zone for rotation torque (normalized 0 to 1).")]
        [SerializeField, Range(0f, 0.5f)] private float rotationDeadZone = 0.12f;

        [Tooltip("Maximum distance from pusher to vehicle hull before push breaks.")]
        [SerializeField] private float maxInteractionDistance = 4.0f;

        [Header("Center of Mass")]
        [Tooltip("Optional marker Transform for custom Center of Mass.")]
        [SerializeField] private Transform centerOfMassMarker;

        [Tooltip("Local offset from transform position for Center of Mass if no marker is set.")]
        [SerializeField] private Vector3 centerOfMassOffset = Vector3.zero;

        [Header("Collision & Environment")]
        [Tooltip("Collider used for push contact calculation. Defaults to Collider on this GameObject.")]
        [SerializeField] private Collider pushCollider;

        [Tooltip("Layers considered obstacles for vehicle movement (walls, doors, props).")]
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Tooltip("Layers considered walkable ground.")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("Distance for downward ground check.")]
        [SerializeField] private float groundCheckDistance = 4.0f;

        private Rigidbody _rigidbody;
        private GameObject _currentPusher;
        private PlayerMovement _pusherMovement;
        private bool _isBeingPushed;
        private float _currentSpeed;
        private float _currentTurnSpeed;
        private float _groundOffset;
        private Vector3 _lastPushDirection;
        private Vector3 _lastPushPoint;

        public bool IsBeingPushed => _isBeingPushed;
        public GameObject CurrentPusher => _currentPusher;
        public float CurrentSpeed => _currentSpeed;
        public float CurrentTurnSpeed => _currentTurnSpeed;
        public Vector3 LastPushDirection => _lastPushDirection;
        public Vector3 LastPushPoint => _lastPushPoint;

        // ──────────────────────────────────────────────────────────────────────
        // IHoldInteractable
        // ──────────────────────────────────────────────────────────────────────

        public string InteractionPrompt => _isBeingPushed ? string.Empty : "Hold [E] to Push";
        public bool RequiresHold => true;

        public bool CanInteract(GameObject interactor)
        {
            if (!isActiveAndEnabled || interactor == null) return false;
            var zone3 = EchoProtocol.Networking.Zone3MissionDirector.Instance;
            if (zone3 != null && zone3.Frigate == this && !zone3.IsPushAvailable) return false;
            var matchState = EchoProtocol.Networking.NetworkMatchState.Instance;
            if (zone3 != null && zone3.Frigate == this
                && matchState != null && matchState.Object != null && matchState.Object.IsValid
                && matchState.Zone3Pusher.IsRealPlayer)
            {
                var playerObject = interactor.GetComponentInParent<Fusion.NetworkObject>();
                if (playerObject == null || playerObject.InputAuthority != matchState.Zone3Pusher) return false;
            }
            if (_currentPusher != null && _currentPusher != interactor) return false;

            float dist = GetDistanceToPusher(interactor.transform.position);
            return dist <= maxInteractionDistance;
        }

        public void Interact(GameObject interactor)
        {
            // Hold interaction handled by Begin/End
        }

        public void BeginHoldInteract(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            var zone3 = EchoProtocol.Networking.Zone3MissionDirector.Instance;
            if (zone3 != null && zone3.Frigate == this)
            {
                var matchState = EchoProtocol.Networking.NetworkMatchState.Instance;
                if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
                {
                    matchState.RequestZone3Push(true);
                    if (!matchState.Object.HasStateAuthority)
                    {
                        _currentPusher = interactor;
                        _isBeingPushed = true;
                    }
                    return;
                }
                else zone3.NotifyOfflinePushStarted();
            }

            BeginAuthoritativePush(interactor);
        }

        public void BeginAuthoritativePush(GameObject interactor)
        {
            if (interactor == null) return;

            _currentPusher = interactor;
            _isBeingPushed = true;
            _pusherMovement = interactor.GetComponent<PlayerMovement>()
                           ?? interactor.GetComponentInParent<PlayerMovement>();

            Debug.Log($"[PushableObject] BeginPush by '{interactor.name}'. Starting active push.");
        }

        public void EndHoldInteract(GameObject interactor)
        {
            var zone3 = EchoProtocol.Networking.Zone3MissionDirector.Instance;
            if (zone3 != null && zone3.Frigate == this)
            {
                var matchState = EchoProtocol.Networking.NetworkMatchState.Instance;
                if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
                {
                    var playerObject = interactor != null
                        ? interactor.GetComponentInParent<Fusion.NetworkObject>() : null;
                    if (matchState.Object.HasStateAuthority && playerObject != null)
                        matchState.ReleaseZone3PushAuthoritative(playerObject.InputAuthority);
                    else
                        matchState.RequestZone3Push(false);
                }
            }
            EndAuthoritativePush(interactor);
        }

        public void EndAuthoritativePush(GameObject interactor)
        {
            if (_currentPusher == interactor || interactor == null)
            {
                Debug.Log($"[PushableObject] EndPush by '{interactor?.name}'.");
                _isBeingPushed = false;
                _currentPusher = null;
                _pusherMovement = null;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (pushCollider == null)
            {
                pushCollider = GetComponent<Collider>();
            }

            ConfigureRigidbody();
            InitializeGroundOffset();
        }

        private void OnDisable()
        {
            EndHoldInteract(_currentPusher);
            _currentSpeed = 0f;
            _currentTurnSpeed = 0f;
        }

        private void FixedUpdate()
        {
            var matchState = EchoProtocol.Networking.NetworkMatchState.Instance;
            if (EchoProtocol.Networking.Zone3MissionDirector.Instance?.Frigate == this
                && matchState != null && matchState.Object != null && matchState.Object.IsValid
                && !matchState.Object.HasStateAuthority) return;

            float targetSpeed = 0f;
            float targetTurnSpeed = 0f;

            if (_isBeingPushed)
            {
                if (!ValidatePusherLifecycle())
                {
                    EndHoldInteract(_currentPusher);
                }
                else
                {
                    Vector3 pusherPos = _currentPusher.transform.position;
                    Vector3 contactPoint = GetPushPoint(pusherPos);
                    Vector3 com = GetCenterOfMass();
                    float maxLever = GetMaxLeverArm();

                    var calc = PushablePhysics.Calculate(pusherPos, contactPoint, com, maxLever, rotationDeadZone);
                    _lastPushPoint = contactPoint;

                    if (calc.PushDirection.sqrMagnitude > 0.001f)
                    {
                        _lastPushDirection = calc.PushDirection;
                    }

                    // Holding E actively applies push speed in calculated direction
                    targetSpeed = pushSpeed;
                    targetTurnSpeed = maxTurnSpeed * calc.EffectiveTorque;
                }
            }

            // Acceleration / Deceleration
            float accelRate = targetSpeed > _currentSpeed ? acceleration : deceleration;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, accelRate * Time.fixedDeltaTime);
            _currentTurnSpeed = Mathf.MoveTowards(_currentTurnSpeed, targetTurnSpeed, deceleration * 15f * Time.fixedDeltaTime);

            // Translation with collision check and ground alignment
            if (_currentSpeed > 0.001f && _lastPushDirection.sqrMagnitude > 0.001f)
            {
                Vector3 moveStep = _lastPushDirection * (_currentSpeed * Time.fixedDeltaTime);
                moveStep = CheckObstacleCollision(moveStep);
                ApplyGroundedMovement(moveStep);
            }

            // Rotation around Y axis
            if (Mathf.Abs(_currentTurnSpeed) > 0.001f)
            {
                Quaternion deltaRot = Quaternion.Euler(0f, _currentTurnSpeed * Time.fixedDeltaTime, 0f);
                if (_rigidbody != null)
                {
                    _rigidbody.MoveRotation(_rigidbody.rotation * deltaRot);
                }
                else
                {
                    transform.rotation = transform.rotation * deltaRot;
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helper Methods
        // ──────────────────────────────────────────────────────────────────────

        private void ConfigureRigidbody()
        {
            if (_rigidbody == null) return;

            _rigidbody.isKinematic = true;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX
                                   | RigidbodyConstraints.FreezeRotationZ;
        }

        private bool TryGetGround(Vector3 testPos, out RaycastHit bestHit)
        {
            bestHit = default;
            Ray ray = new Ray(testPos + Vector3.up * 1.5f, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, groundCheckDistance + 2.5f, groundMask, QueryTriggerInteraction.Ignore);
            float closestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == pushCollider || h.collider.transform.IsChildOf(transform))
                    continue;
                if (_currentPusher != null && (h.collider.gameObject == _currentPusher || h.collider.transform.IsChildOf(_currentPusher.transform)))
                    continue;

                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    bestHit = h;
                    found = true;
                }
            }

            return found;
        }

        private void InitializeGroundOffset()
        {
            if (TryGetGround(transform.position, out RaycastHit hit))
            {
                _groundOffset = transform.position.y - hit.point.y;
            }
            else
            {
                _groundOffset = 0f;
            }
        }

        private bool ValidatePusherLifecycle()
        {
            if (_currentPusher == null || !_currentPusher.activeInHierarchy)
            {
                return false;
            }

            float dist = GetDistanceToPusher(_currentPusher.transform.position);
            if (dist > maxInteractionDistance)
            {
                return false;
            }

            // Check if player is downed or eliminated in multiplayer
            var lifeState = _currentPusher.GetComponent<EchoProtocol.Networking.NetworkPlayerLifeState>()
                         ?? _currentPusher.GetComponentInParent<EchoProtocol.Networking.NetworkPlayerLifeState>();
            if (lifeState != null && lifeState.Status != EchoProtocol.Networking.NetworkPlayerLifeStatus.Alive)
            {
                return false;
            }

            return true;
        }

        public Vector3 GetPushPoint(Vector3 playerPos)
        {
            if (pushCollider != null)
            {
                Vector3 pt = pushCollider.ClosestPoint(playerPos);
                pt.y = playerPos.y;
                return pt;
            }
            return transform.position;
        }

        public Vector3 GetCenterOfMass()
        {
            if (centerOfMassMarker != null)
            {
                return centerOfMassMarker.position;
            }
            return transform.TransformPoint(centerOfMassOffset);
        }

        public float GetMaxLeverArm()
        {
            if (pushCollider is BoxCollider box)
            {
                Vector3 lossy = transform.lossyScale;
                float halfWidth = box.size.x * 0.5f * lossy.x;
                float halfLength = box.size.z * 0.5f * lossy.z;
                return Mathf.Max(halfWidth, halfLength);
            }
            if (pushCollider != null)
            {
                Vector3 ext = pushCollider.bounds.extents;
                return Mathf.Max(ext.x, ext.z);
            }
            return 4.5f;
        }

        public float GetDistanceToPusher(Vector3 pusherPos)
        {
            if (pushCollider != null)
            {
                Vector3 closest = pushCollider.ClosestPoint(pusherPos);
                closest.y = pusherPos.y;
                return Vector3.Distance(pusherPos, closest);
            }
            return Vector3.Distance(pusherPos, transform.position);
        }

        private Vector3 GetPusherMoveDirection()
        {
            if (_pusherMovement != null)
            {
                Vector2 input = _pusherMovement.MoveInput;
                if (input.sqrMagnitude > 0.01f)
                {
                    Vector3 world = _currentPusher.transform.forward * input.y + _currentPusher.transform.right * input.x;
                    world.y = 0f;
                    return world.normalized;
                }
            }
            else if (_currentPusher != null)
            {
                var cc = _currentPusher.GetComponent<CharacterController>();
                if (cc != null && cc.velocity.sqrMagnitude > 0.01f)
                {
                    Vector3 vel = cc.velocity;
                    vel.y = 0f;
                    return vel.normalized;
                }
            }
            return Vector3.zero;
        }

        private Vector3 CheckObstacleCollision(Vector3 moveStep)
        {
            float dist = moveStep.magnitude;
            if (dist <= 0.0001f) return Vector3.zero;

            Vector3 dir = moveStep / dist;
            if (pushCollider is BoxCollider box)
            {
                Vector3 center = transform.TransformPoint(box.center);
                Vector3 lossy = transform.lossyScale;
                Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, lossy) * 0.9f;

                // Lift the box cast up so its bottom is at least 0.5m above the ground to avoid scraping floor colliders
                float bottomClearance = 0.5f;
                Vector3 castCenter = center + Vector3.up * (bottomClearance * 0.5f);
                Vector3 castExtents = new Vector3(halfExtents.x, Mathf.Max(0.2f, halfExtents.y - bottomClearance * 0.5f), halfExtents.z);

                if (Physics.BoxCast(castCenter, castExtents, dir, out RaycastHit hit, transform.rotation, dist + 0.05f, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != pushCollider && !hit.collider.transform.IsChildOf(transform) && (_currentPusher == null || hit.collider.gameObject != _currentPusher))
                    {
                        // Ignore floors/slopes with upward normals
                        if (hit.normal.y > 0.6f)
                        {
                            return moveStep;
                        }

                        float allowedDist = Mathf.Max(0f, hit.distance - 0.05f);
                        if (allowedDist <= 0.001f)
                        {
                            _currentSpeed = 0f;
                            return Vector3.zero;
                        }
                        return dir * allowedDist;
                    }
                }
            }
            return moveStep;
        }

        private void ApplyGroundedMovement(Vector3 moveStep)
        {
            if (moveStep.sqrMagnitude <= 0.000001f) return;

            Vector3 nextPos = _rigidbody != null ? _rigidbody.position + moveStep : transform.position + moveStep;
            if (TryGetGround(nextPos, out RaycastHit groundHit))
            {
                // Slope check: do not push up unrealistic steep surfaces (> 35 deg)
                if (Vector3.Angle(groundHit.normal, Vector3.up) > 35f)
                {
                    _currentSpeed = 0f;
                    return;
                }

                nextPos.y = groundHit.point.y + _groundOffset;
                if (_rigidbody != null)
                {
                    _rigidbody.MovePosition(nextPos);
                }
                else
                {
                    transform.position = nextPos;
                }
            }
            else
            {
                // Abyss or ledge ahead - stop to prevent falling off floor
                _currentSpeed = 0f;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Debug Visualization
        // ──────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 com = GetCenterOfMass();
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(com, 0.25f);

            if (_isBeingPushed && _currentPusher != null)
            {
                Vector3 pushPoint = GetPushPoint(_currentPusher.transform.position);
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(pushPoint, 0.2f);

                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(pushPoint, _lastPushDirection * 2f);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(com, pushPoint);
            }
        }
#endif
    }
}
