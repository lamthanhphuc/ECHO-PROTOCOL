using System.Collections.Generic;
using EchoProtocol.Networking;
using UnityEngine;

namespace EchoProtocol.AI.Listener.Perception
{
    public sealed class UnityListenerOcclusionResolver : IListenerOcclusionResolver
    {
        private readonly LayerMask _acousticBlockerMask;
        private readonly QueryTriggerInteraction _triggerInteraction;
        private readonly RaycastHit[] _hits;
        private readonly Transform _listenerRoot;
        private readonly Collider[] _ignoredListenerColliders;
        private readonly HashSet<Collider> _ignoredSourceColliders;

        public UnityListenerOcclusionResolver(
            LayerMask acousticBlockerMask,
            Transform listenerRoot = null,
            Collider[] ignoredListenerColliders = null,
            int maxHits = 16,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide,
            Collider[] ignoredSourceColliders = null)
        {
            _acousticBlockerMask = acousticBlockerMask;
            _listenerRoot = listenerRoot;
            _ignoredListenerColliders = ignoredListenerColliders ?? System.Array.Empty<Collider>();
            _ignoredSourceColliders = new HashSet<Collider>(ignoredSourceColliders ?? System.Array.Empty<Collider>());
            _triggerInteraction = triggerInteraction;
            _hits = new RaycastHit[Mathf.Max(1, maxHits)];
        }

        public void SetIgnoredSourceColliders(IEnumerable<Collider> ignoredSourceColliders)
        {
            _ignoredSourceColliders.Clear();
            if (ignoredSourceColliders == null)
            {
                return;
            }

            foreach (var collider in ignoredSourceColliders)
            {
                if (collider != null)
                {
                    _ignoredSourceColliders.Add(collider);
                }
            }
        }

        public ListenerOcclusionClass Classify(Vector3 listenerPosition, Vector3 noisePosition)
        {
            var direction = listenerPosition - noisePosition;
            var distance = direction.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return ListenerOcclusionClass.CLEAR;
            }

            var hitCount = Physics.RaycastNonAlloc(
                noisePosition,
                direction / distance,
                _hits,
                distance,
                _acousticBlockerMask,
                _triggerInteraction);
            if (hitCount >= _hits.Length)
            {
                return ListenerOcclusionClass.QUERY_FAILED;
            }

            var strongest = ListenerOcclusionClass.CLEAR;
            for (var index = 0; index < hitCount; index++)
            {
                var collider = _hits[index].collider;
                if (IsIgnored(collider))
                {
                    continue;
                }

                var door = collider.GetComponentInParent<NetworkDoor>();
                var classified = door == null
                    ? ListenerOcclusionClass.SOLID_WALL
                    : ListenerOcclusionClassifier.ClassifyDoorState(door.State);
                strongest = ListenerOcclusionClassifier.Strongest(strongest, classified);
            }

            return strongest;
        }

        private bool IsIgnored(Collider collider)
        {
            if (collider == null)
            {
                return true;
            }

            if (_listenerRoot != null && (collider.transform == _listenerRoot || collider.transform.IsChildOf(_listenerRoot)))
            {
                return true;
            }

            for (var index = 0; index < _ignoredListenerColliders.Length; index++)
            {
                if (_ignoredListenerColliders[index] == collider)
                {
                    return true;
                }
            }

            return _ignoredSourceColliders.Contains(collider);
        }
    }
}
