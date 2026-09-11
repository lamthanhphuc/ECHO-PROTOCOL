using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.Tools.Scanner
{
    [DisallowMultipleComponent]
    public class MotionScannableTarget : MonoBehaviour, IMotionScannable
    {
        private static readonly List<IMotionScannable> _activeTargets = new List<IMotionScannable>();
        public static IReadOnlyList<IMotionScannable> ActiveTargets => _activeTargets;

        [SerializeField] private float manualSpeedThreshold = 0.2f;

        private NavMeshAgent _navMeshAgent;
        private Rigidbody _rigidbody;
        private Vector3 _lastPosition;
        private float _estimatedSpeed;

        public int TargetId => gameObject != null ? gameObject.GetHashCode() : 0;
        public Vector3 WorldPosition => transform.position;

        public float CurrentSpeed
        {
            get
            {
                if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
                {
                    return _navMeshAgent.velocity.magnitude;
                }

                if (_rigidbody != null && !_rigidbody.isKinematic)
                {
                    return _rigidbody.linearVelocity.magnitude;
                }

                return _estimatedSpeed;
            }
        }

        public bool IsMoving => CurrentSpeed >= manualSpeedThreshold;
        public bool IsActiveTarget => gameObject.activeInHierarchy && enabled;

        private void Awake()
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
            _rigidbody = GetComponent<Rigidbody>();
            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            if (!_activeTargets.Contains(this))
            {
                _activeTargets.Add(this);
            }
        }

        private void OnDisable()
        {
            _activeTargets.Remove(this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0.0001f)
            {
                Vector3 currentPos = transform.position;
                _estimatedSpeed = Vector3.Distance(currentPos, _lastPosition) / dt;
                _lastPosition = currentPos;
            }
        }

        public static void RegisterManualTarget(IMotionScannable target)
        {
            if (target != null && !_activeTargets.Contains(target))
            {
                _activeTargets.Add(target);
            }
        }

        public static void UnregisterManualTarget(IMotionScannable target)
        {
            if (target != null)
            {
                _activeTargets.Remove(target);
            }
        }

        public static void ClearAllTargetsForTesting()
        {
            _activeTargets.Clear();
        }
    }
}
