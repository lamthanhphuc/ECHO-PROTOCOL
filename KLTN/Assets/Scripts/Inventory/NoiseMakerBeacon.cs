using System.Collections;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

/// <summary>
/// Spawned beacon that emits repeated runtime noise, then removes itself.
/// </summary>
public sealed class NoiseMakerBeacon : MonoBehaviour
{
    [SerializeField, Min(0f)] private float activationDelay = 1f;
    [SerializeField, Min(1)] private int totalPulses = 8;
    [SerializeField, Min(0.1f)] private float pulseInterval = 2f;
    [SerializeField, Min(0f)] private float despawnDelayAfterLastPulse = 1f;

    [Header("Warning Light")]
    [SerializeField] private Light warningLight;
    [SerializeField] private Color warningColor = new Color(1f, 0.16f, 0.05f);
    [SerializeField, Min(0f)] private float minimumLightIntensity = 1.5f;
    [SerializeField, Min(0f)] private float maximumLightIntensity = 5f;
    [SerializeField, Min(0.1f)] private float warningLightRange = 7f;
    [SerializeField, Min(0.1f)] private float warningPulseSpeed = 5f;

    private PlayerRef _actor;
    private string _streamKey;
    private long _baseSequence;
    private NetworkObject _networkObject;
    private Coroutine _routine;

    private void Awake()
    {
            EchoProtocol.Audio.GameAudioRuntime.RegisterEmitter(this);
        _networkObject = GetComponent<NetworkObject>();

        if (warningLight == null)
        {
            warningLight = GetComponentInChildren<Light>(true);
        }

        if (warningLight != null)
        {
            warningLight.type = LightType.Point;
            warningLight.color = warningColor;
            warningLight.range = warningLightRange;
            warningLight.shadows = LightShadows.None;
            warningLight.enabled = true;
        }
    }

    private void Update()
    {
        if (warningLight == null)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.time * warningPulseSpeed) + 1f) * 0.5f;
        pulse *= pulse;
        warningLight.intensity = Mathf.Lerp(minimumLightIntensity, maximumLightIntensity, pulse);
    }

    public void Initialize(PlayerRef actor, string streamKey, long baseSequence)
    {
        _actor = actor;
        _streamKey = streamKey;
        _baseSequence = baseSequence;

        if (_routine == null && CanRunGameplay())
        {
            _routine = StartCoroutine(BeaconRoutine());
        }
    }

    private bool CanRunGameplay()
    {
        if (_networkObject == null)
        {
            _networkObject = GetComponent<NetworkObject>();
        }

        return _networkObject == null
            || !_networkObject.IsValid
            || _networkObject.HasStateAuthority;
    }

    private IEnumerator BeaconRoutine()
    {
        yield return new WaitForSeconds(activationDelay);

        for (int i = 0; i < totalPulses; i++)
        {
            var authority =
                MatchAuthorityRuntime.Instance;

            var noiseService =
                authority != null
                    ? HostRuntimeNoiseService.EnsureExists(
                        authority)
                    : null;

            if (noiseService != null)
            {
                var key =
                    RuntimeNoiseSourceOccurrenceKey.ForTeamTool(
                        _streamKey,
                        "NOISE_MAKER",
                        (uint)(_baseSequence + i + 1));

                noiseService.TryAccept(
                    _actor,
                    RuntimeNoiseType.NOISE_MAKER,
                    key,
                    transform.position,
                    out _);
            }

            if (i < totalPulses - 1)
            {
                yield return new WaitForSeconds(
                    pulseInterval);
            }
        }

        yield return new WaitForSeconds(despawnDelayAfterLastPulse);
        if (_networkObject != null
            && _networkObject.IsValid
            && _networkObject.HasStateAuthority
            && _networkObject.Runner != null)
        {
            _networkObject.Runner.Despawn(_networkObject);
            yield break;
        }

        Destroy(gameObject);
    }
}
