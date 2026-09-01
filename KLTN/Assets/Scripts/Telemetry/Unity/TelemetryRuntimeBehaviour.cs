using System;
using System.IO;
using EchoProtocol.Auth;
using UnityEngine;

namespace EchoProtocol.Telemetry.Unity
{
    public interface IUnityTelemetryAuthorityProvider : ITelemetryAuthorityContext
    {
    }

    public interface IUnityTelemetryProvenanceProvider : ITelemetryProvenanceProvider
    {
    }

    public sealed class TelemetryRuntimeBehaviour : MonoBehaviour
    {
        private const string RuntimeObjectName = "TelemetryRuntime";

        [Header("Cross-team bindings (must implement the telemetry provider interfaces)")]
        [SerializeField] private MonoBehaviour authorityProvider;
        [SerializeField] private MonoBehaviour provenanceProvider;

        [Header("Implementation tuning")]
        [SerializeField, Min(1)] private int bufferCapacity = 1024;
        [SerializeField, Min(1)] private int occurrenceCapacity = 4096;
        [SerializeField, Min(1)] private int batchSize = 50;
        [SerializeField, Min(0.5f)] private float flushIntervalSeconds = 5f;
        [SerializeField, Min(1)] private int maxRetryAttempts = 6;

        private TelemetrySequenceAllocator _sequenceAllocator;
        private TelemetryEventFactory _eventFactory;
        private TelemetryBuffer _buffer;
        private TelemetryEmitter _emitter;
        private TelemetryBatchSender _batchSender;
        private MatchTelemetryAdapter _matchAdapter;
        private ObjectiveTelemetryAdapter _objectiveAdapter;
        private PlayerTelemetryAdapter _playerAdapter;
        private NoiseTelemetryAdapter _noiseAdapter;
        private MonsterTelemetryAdapter _monsterAdapter;
        private ITelemetryLocalLog _localLog;
        private float _nextFlushAt;
        private bool _initialized;
        private static TelemetryRuntimeBehaviour _instance;

        public bool IsInitialized => _initialized;
        public TelemetryEmitter Emitter => _emitter;
        public TelemetryBuffer Buffer => _buffer;
        public TelemetrySequenceAllocator SequenceAllocator => _sequenceAllocator;
        public MatchTelemetryAdapter MatchAdapter => _matchAdapter;
        public ObjectiveTelemetryAdapter ObjectiveAdapter => _objectiveAdapter;
        public PlayerTelemetryAdapter PlayerAdapter => _playerAdapter;
        public NoiseTelemetryAdapter NoiseAdapter => _noiseAdapter;
        public MonsterTelemetryAdapter MonsterAdapter => _monsterAdapter;
        public string LocalLogPath { get; private set; }

        public static TelemetryRuntimeBehaviour EnsureExists()
        {
            if (_instance == null) _instance = FindAnyObjectByType<TelemetryRuntimeBehaviour>();
            if (_instance == null)
            {
                _instance = new GameObject(RuntimeObjectName).AddComponent<TelemetryRuntimeBehaviour>();
            }
            return _instance;
        }

        public void BindProviders(MonoBehaviour authority, MonoBehaviour provenance)
        {
            if (_initialized) return;
            authorityProvider = authority;
            provenanceProvider = provenance;
            TryInitialize();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (gameObject.name == RuntimeObjectName)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            TryInitialize();
        }

        private void Update()
        {
            if (!_initialized || Time.unscaledTime < _nextFlushAt)
            {
                return;
            }

            _nextFlushAt = Time.unscaledTime + flushIntervalSeconds;
            _batchSender.TryFlush(DateTime.UtcNow);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _initialized)
            {
                _batchSender.TryFlush(DateTime.UtcNow);
            }
        }

        private void OnApplicationQuit()
        {
            if (_initialized)
            {
                _batchSender.TryFlush(DateTime.UtcNow);
            }
        }

        public bool TryInitialize()
        {
            if (_initialized)
            {
                return true;
            }

            var authority = authorityProvider as IUnityTelemetryAuthorityProvider;
            var provenance = provenanceProvider as IUnityTelemetryProvenanceProvider;
            if (authority == null || provenance == null)
            {
                Debug.LogWarning(
                    "[Telemetry] Runtime remains disabled: authoritative match and provenance providers are not bound.");
                return false;
            }

            var authRuntime = AuthRuntime.EnsureExists();
            if (authRuntime == null || !authRuntime.IsInitialized || authRuntime.Client == null)
            {
                Debug.LogWarning("[Telemetry] Runtime remains disabled: Auth/API runtime is unavailable.");
                return false;
            }

            LocalLogPath = Path.Combine(Application.persistentDataPath, "telemetry", "telemetry-v1.1.jsonl");
            _localLog = new TelemetryFileLocalLog(LocalLogPath);
            _sequenceAllocator = new TelemetrySequenceAllocator();
            _buffer = new TelemetryBuffer(
                bufferCapacity,
                new TelemetryRetryPolicy(maxRetryAttempts));
            _eventFactory = new TelemetryEventFactory(
                _sequenceAllocator,
                authority,
                provenance,
                occurrenceCapacity);
            _emitter = new TelemetryEmitter(_eventFactory, _buffer, provenance, _localLog);
            _matchAdapter = new MatchTelemetryAdapter(_emitter);
            _objectiveAdapter = new ObjectiveTelemetryAdapter(_emitter);
            _playerAdapter = new PlayerTelemetryAdapter(_emitter);
            _noiseAdapter = new NoiseTelemetryAdapter(_emitter);
            _monsterAdapter = new MonsterTelemetryAdapter(_emitter);
            _batchSender = new TelemetryBatchSender(
                _buffer,
                new TelemetryApiTransport(authRuntime.Client),
                batchSize,
                _localLog);
            _nextFlushAt = Time.unscaledTime + flushIntervalSeconds;
            _initialized = true;
            return true;
        }

        public bool TryBeginAuthoritativeMatch()
        {
            if (!TryInitialize())
            {
                return false;
            }

            try
            {
                _eventFactory.BeginMatch();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Telemetry] Match telemetry did not start: " + exception.Message);
                return false;
            }
        }

        public bool TryFlushNow()
        {
            return _initialized && _batchSender.TryFlush(DateTime.UtcNow);
        }
    }
}
