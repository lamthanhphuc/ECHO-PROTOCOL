using System;
using EchoProtocol.AI.Listener.Noise;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking.Authority
{
    /// <summary>Host-only acceptance boundary shared by gameplay noise, telemetry and AI consumers.</summary>
    public sealed class HostRuntimeNoiseService : MonoBehaviour
    {
        private static HostRuntimeNoiseService _instance;
        private MatchAuthorityRuntime _authority;
        private readonly RuntimeNoiseSystem _noiseSystem = new RuntimeNoiseSystem();
        private Guid _boundMatchId;

        public event Action<RuntimeNoiseEvent> RuntimeNoiseAccepted
        {
            add { _noiseSystem.RuntimeNoiseAccepted += value; }
            remove { _noiseSystem.RuntimeNoiseAccepted -= value; }
        }

        public int ActiveNoiseCount => _noiseSystem.ActiveCount;
        public int DedupCount => _noiseSystem.DedupCount;
        public NoiseValidationRejectReason LastRejectReason { get; private set; }
        public NoiseSystemDiagnosticReason LastDiagnosticReason { get; private set; }

        public event Action<RuntimeNoiseSystemDiagnostic> RuntimeNoiseDiagnostic
        {
            add { _noiseSystem.DiagnosticEmitted += value; }
            remove { _noiseSystem.DiagnosticEmitted -= value; }
        }

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

        public void BeginMatch(Guid matchId)
        {
            if (matchId == Guid.Empty)
            {
                throw new ArgumentException("Match id must not be empty.", nameof(matchId));
            }

            if (matchId == _boundMatchId)
            {
                return;
            }

            _noiseSystem.ResetForMatch();
            _boundMatchId = matchId;
            LastRejectReason = NoiseValidationRejectReason.None;
            LastDiagnosticReason = NoiseSystemDiagnosticReason.None;
        }

        public bool TryAccept(
            PlayerRef actor,
            RuntimeNoiseType noiseType,
            RuntimeNoiseSourceOccurrenceKey sourceOccurrenceKey,
            Vector3 position,
            out RuntimeNoiseEvent noiseEvent)
        {
            if (_authority == null || !_authority.HasStateAuthority)
            {
                noiseEvent = default;
                LastRejectReason = NoiseValidationRejectReason.NotStateAuthority;
                LastDiagnosticReason = NoiseSystemDiagnosticReason.SubsystemUnavailable;
                return false;
            }

            if (!actor.IsValid)
            {
                noiseEvent = default;
                LastRejectReason = NoiseValidationRejectReason.SourceActionRejected;
                LastDiagnosticReason = NoiseSystemDiagnosticReason.None;
                return false;
            }

            if (_boundMatchId == Guid.Empty || _boundMatchId != _authority.MatchId)
            {
                noiseEvent = default;
                LastRejectReason = NoiseValidationRejectReason.SourceActionRejected;
                LastDiagnosticReason = NoiseSystemDiagnosticReason.SubsystemUnavailable;
                return false;
            }

            LastRejectReason = NoiseValidationRejectReason.None;
            LastDiagnosticReason = NoiseSystemDiagnosticReason.None;
            var emittedAtUtc = DateTime.UtcNow;
            var status = _noiseSystem.TryAccept(
                _boundMatchId,
                new RuntimeNoiseEmissionRequest(
                    sourceOccurrenceKey,
                    noiseType,
                    position,
                    emittedAtUtc,
                    _authority.AuthorityTick ?? 0L,
                    actor.ToString()),
                out noiseEvent);
            LastRejectReason = _noiseSystem.LastRejectReason;
            LastDiagnosticReason = _noiseSystem.LastDiagnosticReason;
            if (status != RuntimeNoiseAcceptStatus.Accepted)
            {
                return false;
            }

            _authority.RecordRuntimeNoise(
                actor,
                noiseEvent.NoiseEventId,
                noiseEvent.EmittedAtUtc,
                noiseEvent.NoiseType.ToString(),
                noiseEvent.Loudness,
                noiseEvent.WorldPosition,
                noiseEvent.HearingRadius);
            return true;
        }

        public void Expire(DateTime nowUtc)
        {
            _noiseSystem.Expire(nowUtc);
        }

        public void EndMatch()
        {
            _noiseSystem.ResetForMatch();
            _boundMatchId = Guid.Empty;
            LastRejectReason = NoiseValidationRejectReason.None;
            LastDiagnosticReason = NoiseSystemDiagnosticReason.None;
        }

        public void ResetForMatch()
        {
            EndMatch();
        }
    }
}
