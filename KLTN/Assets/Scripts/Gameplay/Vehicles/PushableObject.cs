using System.Collections.Generic;
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

        [Header("Co-op Push")]
        [Tooltip("Extra push speed multiplier per additional pusher.")]
        [SerializeField, Range(0f, 0.5f)] private float extraPusherSpeedBonus = 0.15f;

        [Tooltip("Maximum total push speed multiplier when multiple players push together.")]
        [SerializeField, Min(1f)] private float maxCoopSpeedMultiplier = 1.45f;

        private Rigidbody _rigidbody;
        private readonly List<GameObject> _currentPushers = new List<GameObject>();
        private readonly Dictionary<GameObject, PlayerMovement> _pusherMovements = new Dictionary<GameObject, PlayerMovement>();
        private bool _isBeingPushed;
        private float _currentSpeed;
        private float _currentTurnSpeed;
        private float _groundOffset;
        private Vector3 _lastPushDirection;
        private Vector3 _lastPushPoint;

        public bool IsBeingPushed => _isBeingPushed;
        public GameObject CurrentPusher => _currentPushers.Count > 0 ? _currentPushers[0] : null;
        public float CurrentSpeed => _currentSpeed;
        public float CurrentTurnSpeed => _currentTurnSpeed;
        public Vector3 LastPushDirection => _lastPushDirection;
        public Vector3 LastPushPoint => _lastPushPoint;

        // ──────────────────────────────────────────────────────────────────────
        // IHoldInteractable
        // ──────────────────────────────────────────────────────────────────────

        public string InteractionPrompt
        {
            get
            {
                var zone3 = EchoProtocol.Networking.Zone3MissionDirector.Instance;
                if (zone3 != null && zone3.Frigate == this && zone3.Convoy != null)
                    return zone3.Convoy.InteractionPrompt;
                return _isBeingPushed ? "Hold [E] to Assist Push" : "Hold [E] to Push";
            }
        }

        public bool RequiresHold
        {
            get
            {
                var zone3 = EchoProtocol.Networking.Zone3MissionDirector.Instance;
                return !(zone3 != null && zone3.Frigate == this && zone3.Convoy != null);
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!isActiveAndEnabled || interactor == null) return false;
            var zone3 = EchoProtocol.Networking.Zone3MissionDirector.Instance;
            if (zone3 != null && zone3.Frigate == this && zone3.Convoy != null)
                return zone3.Convoy.CanInteract(interactor);
            if (zone3 != null && zone3.Frigate == this && !zone3.IsPushAvailable) return false;
            float dist = GetDistanceToPusher(interactor.transform.position);
            return dist <= maxInteractionDistance;
        }

        public void Interact(GameObject interactor)
        {
            var zone3 = EchoProtocol.Networking.Zone3MissionDirector.Instance;
            if (zone3 != null && zone3.Frigate == this && zone3.Convoy != null)
                zone3.Convoy.Interact(interactor);
        }

        public void BeginHoldInteract(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            var zone3 = EchoProtocol.Networking.Zone3MissionDirector.Instance;
            if (zone3 != null && zone3.Frigate == this && zone3.Convoy != null)
            {
                zone3.Convoy.Interact(interactor);
                return;
            }
            if (zone3 != null && zone3.Frigate == this)
            {
                var matchState = EchoProtocol.Networking.NetworkMatchState.Instance;
                if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
                {
                    matchState.RequestZone3Push(true);
                    if (!matchState.Object.HasStateAuthority)
                    {
                        AddPusher(interactor);
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

            AddPusher(interactor);

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
            if (interactor == null)
            {
                ClearPushers();
                return;
            }

            if (RemovePusher(interactor))
            {
                Debug.Log($"[PushableObject] EndPush by '{interactor.name}'.");
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
            var pushers = _currentPushers.ToArray();
            for (int i = 0; i < pushers.Length; i++)
            {
                EndHoldInteract(pushers[i]);
            }
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
                ReleaseInvalidPushers();
                if (_currentPushers.Count > 0)
                {
                    Vector3 com = GetCenterOfMass();
                    float maxLever = GetMaxLeverArm();
                    Vector3 combinedDirection = Vector3.zero;
                    Vector3 combinedPushPoint = Vector3.zero;
                    float combinedTorque = 0f;
                    int activePushers = 0;

                    for (int i = 0; i < _currentPushers.Count; i++)
                    {
                        var pusher = _currentPushers[i];
                        Vector3 pusherPos = pusher.transform.position;
                        Vector3 contactPoint = GetPushPoint(pusherPos);
                        var calc = PushablePhysics.Calculate(pusherPos, contactPoint, com, maxLever, rotationDeadZone);
                        if (calc.PushDirection.sqrMagnitude <= 0.001f) continue;

                        combinedDirection += calc.PushDirection;
                        combinedPushPoint += contactPoint;
                        combinedTorque += calc.EffectiveTorque;
                        activePushers++;
                    }

                    if (activePushers > 0)
                    {
                        _lastPushDirection = combinedDirection.normalized;
                        _lastPushPoint = combinedPushPoint / activePushers;
                        float speedMultiplier = Mathf.Min(maxCoopSpeedMultiplier,
                            1f + extraPusherSpeedBonus * (activePushers - 1));
                        targetSpeed = pushSpeed * speedMultiplier;
                        targetTurnSpeed = maxTurnSpeed * Mathf.Clamp(combinedTorque / activePushers, -1f, 1f);
                    }
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
                if (IsPusherCollider(h.collider))
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

        private bool ValidatePusherLifecycle(GameObject pusher)
        {
            if (pusher == null || !pusher.activeInHierarchy)
            {
                return false;
            }

            float dist = GetDistanceToPusher(pusher.transform.position);
            if (dist > maxInteractionDistance)
            {
                return false;
            }

            // Check if player is downed or eliminated in multiplayer
            var lifeState = pusher.GetComponent<EchoProtocol.Networking.NetworkPlayerLifeState>()
                         ?? pusher.GetComponentInParent<EchoProtocol.Networking.NetworkPlayerLifeState>();
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

        private Vector3 GetPusherMoveDirection(GameObject pusher)
        {
            if (pusher == null) return Vector3.zero;

            _pusherMovements.TryGetValue(pusher, out var movement);
            if (movement != null)
            {
                Vector2 input = movement.MoveInput;
                if (input.sqrMagnitude > 0.01f)
                {
                    Vector3 world = pusher.transform.forward * input.y + pusher.transform.right * input.x;
                    world.y = 0f;
                    return world.normalized;
                }
            }
            else
            {
                var cc = pusher.GetComponent<CharacterController>();
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
                    if (hit.collider != pushCollider && !hit.collider.transform.IsChildOf(transform) && !IsPusherCollider(hit.collider))
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

        private void AddPusher(GameObject pusher)
        {
            if (pusher == null || _currentPushers.Contains(pusher)) return;
            _currentPushers.Add(pusher);
            _pusherMovements[pusher] = pusher.GetComponent<PlayerMovement>()
                                      ?? pusher.GetComponentInParent<PlayerMovement>();
            SetPusherAnimation(pusher, true);
            _isBeingPushed = _currentPushers.Count > 0;
        }

        private bool RemovePusher(GameObject pusher)
        {
            if (pusher == null) return false;
            bool removed = _currentPushers.Remove(pusher);
            _pusherMovements.Remove(pusher);
            if (removed) SetPusherAnimation(pusher, false);
            _isBeingPushed = _currentPushers.Count > 0;
            return removed;
        }

        private void ClearPushers()
        {
            for (int i = 0; i < _currentPushers.Count; i++)
            {
                SetPusherAnimation(_currentPushers[i], false);
            }
            _currentPushers.Clear();
            _pusherMovements.Clear();
            _isBeingPushed = false;
        }

        private static void SetPusherAnimation(GameObject pusher, bool active)
        {
            if (pusher == null) return;
            var networkMovement = pusher.GetComponent<EchoProtocol.Networking.NetworkPlayerMovement>()
                               ?? pusher.GetComponentInParent<EchoProtocol.Networking.NetworkPlayerMovement>();
            networkMovement?.SetAnimationPushing(active);

            var animatorDriver = pusher.GetComponent<PlayerAnimatorDriver>()
                              ?? pusher.GetComponentInParent<PlayerAnimatorDriver>();
            animatorDriver?.SetPushing(active);
        }

        private void ReleaseInvalidPushers()
        {
            for (int i = _currentPushers.Count - 1; i >= 0; i--)
            {
                var pusher = _currentPushers[i];
                if (!ValidatePusherLifecycle(pusher))
                {
                    EndHoldInteract(pusher);
                }
            }
        }

        private bool IsPusherCollider(Collider candidate)
        {
            if (candidate == null) return false;
            for (int i = 0; i < _currentPushers.Count; i++)
            {
                var pusher = _currentPushers[i];
                if (pusher != null && (candidate.gameObject == pusher || candidate.transform.IsChildOf(pusher.transform)))
                    return true;
            }
            return false;
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

            if (_isBeingPushed)
            {
                for (int i = 0; i < _currentPushers.Count; i++)
                {
                    var pusher = _currentPushers[i];
                    if (pusher == null) continue;
                    Vector3 pushPoint = GetPushPoint(pusher.transform.position);
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(pushPoint, 0.2f);

                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(com, pushPoint);
                }

                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(_lastPushPoint, _lastPushDirection * 2f);
            }
        }
#endif
    }
}
