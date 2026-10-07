using UnityEngine;
using EchoProtocol.Networking;

namespace EchoProtocol.MatchFlow
{
    /// <summary>Small local presentation driven exclusively by replicated convoy fuel.</summary>
    public sealed class Zone3FuelPresentation : MonoBehaviour
    {
        [SerializeField] private AudioClip engineLoop;
        [SerializeField] private AudioClip warningBeep;
        [SerializeField] private AudioClip shutdown;
        [SerializeField] private AudioClip ignition;
        private Zone3ConvoyController _convoy;
        private Renderer _indicator;
        private Light _light;
        private AudioSource _engine;
        private AudioSource _feedback;
        private MaterialPropertyBlock _block;
        private int _lastFuel = -1;
        private float _nextWarning;
        private readonly System.Collections.Generic.List<AudioClip> _generated =
            new System.Collections.Generic.List<AudioClip>();

        private void Awake()
        {
            _convoy = GetComponent<Zone3ConvoyController>();
            var port = GetComponentInChildren<Zone3FuelPort>();
            _indicator = port != null ? port.GetComponentInChildren<Renderer>() : null;
            _light = port != null ? port.GetComponentInChildren<Light>() : null;
            _block = new MaterialPropertyBlock();
            _engine = gameObject.AddComponent<AudioSource>();
            _feedback = gameObject.AddComponent<AudioSource>();
            foreach (var source in new[] { _engine, _feedback })
            {
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.minDistance = 3f;
                source.maxDistance = 24f;
                source.rolloffMode = AudioRolloffMode.Linear;
            }
            _engine.loop = true;
            _engine.volume = 0.1f;
            _feedback.volume = 0.25f;
            _engine.clip = engineLoop != null ? engineLoop : MakeTone("Convoy motor", 1f, 80f, 80f, true);
            if (warningBeep == null) warningBeep = MakeTone("Fuel warning", 0.14f, 600f, 600f, false);
            if (shutdown == null) shutdown = MakeTone("Engine shutdown", 0.65f, 160f, 35f, false);
            if (ignition == null) ignition = MakeTone("Engine ignition", 0.8f, 45f, 210f, false);
        }
        private AudioClip MakeTone(string title, float duration, float start, float end, bool loop)
        {
            const int rate = 22050;
            var samples = new float[Mathf.RoundToInt(duration * rate)];
            float phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)samples.Length;
                phase += 2f * Mathf.PI * Mathf.Lerp(start, end, t) / rate;
                float envelope = loop ? 1f : Mathf.Min(1f, t * 20f) * Mathf.Min(1f, (1f - t) * 10f);
                samples[i] = (Mathf.Sin(phase) * 0.65f + Mathf.Sin(phase * 2f) * 0.15f) * envelope;
            }
            var clip = AudioClip.Create(title, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            _generated.Add(clip);
            return clip;
        }
        private void Update()
        {
            if (_convoy == null || !_convoy.IsInitialized) return;
            int fuel = _convoy.FuelPointsRemaining;
            bool docked = _convoy.CurrentPoint == Zone3ConvoyRoutePoint.Final;
            if (_lastFuel >= 0 && fuel != _lastFuel && !docked)
            {
                if (fuel == 0) _feedback.PlayOneShot(shutdown);
                else if (_lastFuel == 0) _feedback.PlayOneShot(ignition);
                else if (fuel == 1) _feedback.PlayOneShot(warningBeep);
            }
            _lastFuel = fuel;
            bool running = fuel > 0 && !docked && Zone3MissionDirector.Instance?.IsPushAvailable == true;
            if (running && !_engine.isPlaying) _engine.Play();
            if (!running && _engine.isPlaying) _engine.Stop();
            _engine.pitch = fuel == 1 ? 0.82f : 1f;
            if (running && fuel == 1 && Time.unscaledTime >= _nextWarning)
            {
                _feedback.PlayOneShot(warningBeep);
                _nextWarning = Time.unscaledTime + 8f;
            }
            Color color = fuel == 0 ? new Color(1f, 0.08f, 0.02f)
                : fuel == 1 ? new Color(1f, 0.5f, 0.02f) : new Color(0.15f, 1f, 0.4f);
            if (_indicator != null)
            {
                _indicator.GetPropertyBlock(_block);
                _block.SetColor("_BaseColor", color);
                _block.SetColor("_EmissionColor", color * 2f);
                _indicator.SetPropertyBlock(_block);
            }
            if (_light != null)
            {
                _light.color = color;
                _light.intensity = fuel == 0 ? 0.7f + Mathf.Sin(Time.unscaledTime * 5f) * 0.3f : 1f;
            }
        }
        private void OnDisable()
        {
            if (_engine != null) _engine.Stop();
            if (_feedback != null) _feedback.Stop();
        }
        private void OnDestroy()
        {
            foreach (var clip in _generated) if (clip != null) Destroy(clip);
        }
    }
}
