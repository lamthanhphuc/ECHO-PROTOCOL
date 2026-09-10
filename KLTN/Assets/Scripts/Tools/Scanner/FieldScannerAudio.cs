using UnityEngine;

namespace EchoProtocol.Tools.Scanner
{
    [RequireComponent(typeof(AudioSource))]
    public class FieldScannerAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0.05f, 1f)] private float volume = 0.4f;

        private AudioClip _chirpClip;
        private AudioClip _beepClip;
        private float _beepTimer;
        private float _beepInterval;
        private int _currentIntensityLevel; // 0=none, 1=weak, 2=med, 3=strong, 4=critical

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f; // half 2D / 3D so owner hears clearly and nearby players hear locally
            audioSource.volume = volume;

            _chirpClip = CreateChirpClip(800f, 1600f, 0.18f);
            _beepClip = CreateToneClip(1200f, 0.08f);
        }

        private void Update()
        {
            if (_currentIntensityLevel <= 0) return;

            _beepTimer -= Time.deltaTime;
            if (_beepTimer <= 0f)
            {
                PlayBeep();
                _beepTimer = _beepInterval;
            }
        }

        public void PlayScanPulse()
        {
            if (audioSource != null && _chirpClip != null)
            {
                audioSource.PlayOneShot(_chirpClip, volume * 1.2f);
            }
        }

        public void SetFeedbackSignal(int intensityLevel) // 0 to 4
        {
            _currentIntensityLevel = Mathf.Clamp(intensityLevel, 0, 4);
            switch (_currentIntensityLevel)
            {
                case 4: // Critical / 4 bars
                    _beepInterval = 0.22f;
                    break;
                case 3: // Strong / 3 bars
                    _beepInterval = 0.45f;
                    break;
                case 2: // Medium / 2 bars
                    _beepInterval = 0.8f;
                    break;
                case 1: // Weak / 1 bar
                    _beepInterval = 1.3f;
                    break;
                default:
                    _beepInterval = 0f;
                    break;
            }

            _beepTimer = 0.05f; // Trigger first beep quickly
        }

        public void StopFeedback()
        {
            _currentIntensityLevel = 0;
            _beepTimer = 0f;
        }

        private void PlayBeep()
        {
            if (audioSource != null && _beepClip != null)
            {
                audioSource.PlayOneShot(_beepClip, volume * 0.8f);
            }
        }

        private static AudioClip CreateToneClip(float frequency, float duration)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin((float)i / samples * Mathf.PI);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
            }

            AudioClip clip = AudioClip.Create("ScannerTone", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateChirpClip(float startFreq, float endFreq, float duration)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / samples;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, progress);
                float envelope = Mathf.Sin(progress * Mathf.PI);
                data[i] = Mathf.Sin(2f * Mathf.PI * currentFreq * t) * envelope;
            }

            AudioClip clip = AudioClip.Create("ScannerChirp", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

