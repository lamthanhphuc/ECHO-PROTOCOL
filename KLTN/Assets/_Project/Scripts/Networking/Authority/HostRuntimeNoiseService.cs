using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking.Authority
{
    /// <summary>Host-only acceptance boundary shared by gameplay noise, telemetry and AI consumers.</summary>
    public sealed class HostRuntimeNoiseService : MonoBehaviour
    {
        private static HostRuntimeNoiseService _instance;
        private MatchAuthorityRuntime _authority;
        private ulong _nextNoiseOrdinal;

        public static HostRuntimeNoiseService EnsureExists(MatchAuthorityRuntime authority)
        {
            if (_instance == null) _instance = FindAnyObjectByType<HostRuntimeNoiseService>();
            if (_instance == null)
            {
                _instance = new GameObject("HostRuntimeNoiseService").AddComponent<HostRuntimeNoiseService>();
            }

            _instance._authority = authority;
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool TryAccept(
            PlayerRef actor,
            string noiseType,
            double loudness,
            Vector3 position,
            double hearingRadius)
        {
            if (_authority == null || !_authority.HasStateAuthority || !actor.IsValid)
            {
                return false;
            }

            _nextNoiseOrdinal++;
            if (_nextNoiseOrdinal == 0) _nextNoiseOrdinal = 1;
            var noiseEventId = $"{_authority.MatchId:N}:{_authority.AuthorityTick ?? 0}:{_nextNoiseOrdinal}";
            return _authority.RecordRuntimeNoise(
                actor,
                noiseEventId,
                noiseType,
                loudness,
                position,
                hearingRadius);
        }
    }
}
