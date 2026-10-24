using EchoProtocol.Networking;
using EchoProtocol.AI.Minions;
using EchoProtocol.AI.Stalker.Networking;
using UnityEngine;

namespace EchoProtocol.Audio
{
    /// <summary>Lightweight local muffling. Preserves authored volume, fades and mixer levels.</summary>
    [DisallowMultipleComponent]
    public sealed class PurgeSpatialAudio : MonoBehaviour
    {
        private AudioSource _source;
        private AudioLowPassFilter _filter;
        private Transform _emitter;
        private readonly RaycastHit[] _hits = new RaycastHit[24];
        private float _nextProbe;
        private bool _blocked;

        public static PurgeSpatialAudio Attach(AudioSource source, Transform emitter)
        {
            if (source == null) return null;
            var audio = source.GetComponent<PurgeSpatialAudio>();
            if (audio == null) audio = source.gameObject.AddComponent<PurgeSpatialAudio>();
            audio._source = source; audio._emitter = emitter;
            audio._filter = source.GetComponent<AudioLowPassFilter>();
            if (audio._filter == null) audio._filter = source.gameObject.AddComponent<AudioLowPassFilter>();
            audio._filter.cutoffFrequency = 22000f;
            audio._nextProbe = Time.unscaledTime + Random.Range(0f, .2f);
            return audio;
        }

        private void Update()
        {
            var camera = Camera.main;
            if (_source == null || _filter == null) return;
            bool audible = _source.isPlaying && camera != null && _source.spatialBlend > .9f
                && (camera.transform.position - transform.position).sqrMagnitude < _source.maxDistance * _source.maxDistance;
            if (!audible) { _blocked = false; _filter.cutoffFrequency = 22000f; return; }
            if (Time.unscaledTime >= _nextProbe)
            { _blocked = IsBlocked(camera.transform.position); _nextProbe = Time.unscaledTime + .25f; }
            _filter.cutoffFrequency = Mathf.Lerp(_filter.cutoffFrequency, _blocked ? 2400f : 22000f,
                1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f));
        }

        private bool IsBlocked(Vector3 listener)
        {
            var offset = transform.position - listener;
            float distance = offset.magnitude;
            if (distance < .3f) return false;
            int count = Physics.RaycastNonAlloc(listener, offset / distance, _hits, distance - .15f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = _hits[i].collider;
                if (!GameplayEnvironmentPhysics.IsEnvironment(collider)
                    || _emitter != null && collider.transform.IsChildOf(_emitter)
                    || collider.GetComponentInParent<CreepMinionRuntime>() != null
                    || collider.GetComponentInParent<StalkerFusionRuntime>() != null
                    || collider.GetComponentInParent<AlienHuntActor>() != null) continue;
                return true;
            }
            return false;
        }

        private void OnDisable() { _blocked = false; if (_filter != null) _filter.cutoffFrequency = 22000f; }
    }
}
