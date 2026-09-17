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

    private PlayerRef _actor;
    private string _streamKey;
    private long _baseSequence;
    private NetworkObject _networkObject;
    private Coroutine _routine;

    private void Awake()
    {
        _networkObject = GetComponent<NetworkObject>();
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

        var noiseService = FindAnyObjectByType<HostRuntimeNoiseService>();

        for (int i = 0; i < totalPulses; i++)
        {
            if (noiseService != null)
            {
                var key = RuntimeNoiseSourceOccurrenceKey.ForTeamTool(
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
                yield return new WaitForSeconds(pulseInterval);
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
