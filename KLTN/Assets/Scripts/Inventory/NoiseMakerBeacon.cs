using System.Collections;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.AI.Stalker;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;

/// <summary>
/// Gắn vào DistressBeaconDeployed prefab khi được spawn.
/// Sau 1s delay: phát 4 xung noise (mỗi 1.5s), bán kính 22m.
/// Sau khi hết thời gian: tự hủy.
/// </summary>
public sealed class NoiseMakerBeacon : MonoBehaviour
{
    // Cấu hình beacon
    private const float ActivationDelay = 1f;     // giây trước khi bắt đầu phát
    private const int TotalPulses = 4;             // số lần phát noise
    private const float PulseInterval = 1.5f;     // giây giữa các xung

    private PlayerRef _actor;
    private string _streamKey;
    private long _baseSequence;
    private NetworkObject _networkObject;
    private Coroutine _routine;

    private void Awake()
    {
        _networkObject = GetComponent<NetworkObject>();
    }

    /// <summary>
    /// Gọi ngay sau khi Instantiate để inject context.
    /// </summary>
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
        yield return new WaitForSeconds(ActivationDelay);

        var noiseService = FindAnyObjectByType<HostRuntimeNoiseService>();

        for (int i = 0; i < TotalPulses; i++)
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

            // Alert tất cả Stalker trong tầm
            AlertNearbyStalkers();

            if (i < TotalPulses - 1)
            {
                yield return new WaitForSeconds(PulseInterval);
            }
        }

        yield return new WaitForSeconds(0.5f);
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

    private void AlertNearbyStalkers()
    {
        // Bán kính nghe của NOISE_MAKER là 22m (cập nhật trong RuntimeNoiseCatalog)
        const float alertRadius = 22f;
        var stalkers = FindObjectsByType<StalkerController>(FindObjectsInactive.Exclude);
        foreach (var stalker in stalkers)
        {
            if (Vector3.SqrMagnitude(stalker.transform.position - transform.position)
                <= alertRadius * alertRadius)
            {
                stalker.AlertToNoise(transform.position);
            }
        }
    }
}
