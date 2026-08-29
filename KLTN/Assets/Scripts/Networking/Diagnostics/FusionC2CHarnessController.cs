using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking.Diagnostics
{
    public enum FusionC2CHarnessMode
    {
        Host,
        Client
    }

    public sealed class FusionC2CHarnessController : MonoBehaviour
    {
        public const string DefaultSessionName = "EchoProtocol-FND003C2C";

        [SerializeField] private NetworkRunner runnerPrefab;
        [SerializeField] private FusionC2CHarnessMode mode = FusionC2CHarnessMode.Host;
        [SerializeField] private string sessionName = DefaultSessionName;
        [SerializeField] private bool autostart = true;
        [SerializeField] private float initialSnapshotTimeoutSeconds = 5f;

        private NetworkRunner _runner;
        private Task _startTask;
        private FusionC2CHarnessLaunchConfiguration _launchConfiguration;

        public NetworkRunner Runner => _runner;

        public string ResolvedSessionName => ResolveSessionName(sessionName);

        private void Awake()
        {
            if (!TryCreateLaunchConfiguration(
                    Environment.GetCommandLineArgs(),
                    mode,
                    sessionName,
                    autostart,
                    out _launchConfiguration))
            {
                _launchConfiguration = new FusionC2CHarnessLaunchConfiguration(
                    mode,
                    ResolveSessionName(sessionName),
                    false);
            }
        }

        private void Start()
        {
            if (_launchConfiguration.Autostart)
            {
                StartHarness();
            }
        }

        public void StartHarness()
        {
            if (_startTask != null)
            {
                Debug.Log($"C2C|START_IGNORED|reason=AlreadyStartingOrStarted|mode={_launchConfiguration.Mode}|session={_launchConfiguration.SessionName}");
                return;
            }

            _startTask = StartHarnessAsync();
        }

        public async Task StartHarnessAsync()
        {
            if (_runner != null && _runner.IsRunning)
            {
                Debug.Log($"C2C|START_IGNORED|reason=RunnerAlreadyRunning|mode={_launchConfiguration.Mode}|session={_launchConfiguration.SessionName}");
                return;
            }

            Debug.Log($"C2C|START_REQUEST|mode={_launchConfiguration.Mode}|session={_launchConfiguration.SessionName}");

            if (runnerPrefab == null)
            {
                Debug.LogError($"C2C|START_FAIL|mode={_launchConfiguration.Mode}|session={_launchConfiguration.SessionName}|reason=MissingRunnerPrefab");
                return;
            }

            if (_runner == null)
            {
                _runner = Instantiate(runnerPrefab);
                _runner.name = $"C2CNetworkRunner_{_launchConfiguration.Mode}";
            }

            try
            {
                var result = await _runner.StartGame(new StartGameArgs
                {
                    GameMode = ToFusionGameMode(_launchConfiguration.Mode),
                    SessionName = _launchConfiguration.SessionName,
                    EnableClientSessionCreation = false,
                    SceneManager = _runner.GetComponent<INetworkSceneManager>(),
                    ObjectProvider = _runner.GetComponent<INetworkObjectProvider>()
                });

                if (result.Ok)
                {
                    Debug.Log($"C2C|START_OK|mode={_launchConfiguration.Mode}|session={_launchConfiguration.SessionName}");
                    await WaitForInitialSnapshotReadinessAsync();
                    CaptureProbeSnapshot();
                }
                else
                {
                    Debug.LogError($"C2C|START_FAIL|mode={_launchConfiguration.Mode}|session={_launchConfiguration.SessionName}|reason={result.ShutdownReason}|message={Sanitize(result.ErrorMessage)}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"C2C|START_FAIL|mode={_launchConfiguration.Mode}|session={_launchConfiguration.SessionName}|reason=Exception|message={Sanitize(ex.Message)}");
            }
        }

        public async Task ShutdownHarnessAsync()
        {
            if (_runner == null || !_runner.IsRunning)
            {
                return;
            }

            Debug.Log($"C2C|SHUTDOWN_REQUEST|mode={_launchConfiguration.Mode}|session={_launchConfiguration.SessionName}");
            await _runner.Shutdown();
            Debug.Log($"C2C|SHUTDOWN_OK|mode={_launchConfiguration.Mode}|session={_launchConfiguration.SessionName}");
        }

        internal static bool TryCreateLaunchConfiguration(
            string[] args,
            FusionC2CHarnessMode defaultMode,
            string defaultSessionName,
            bool defaultAutostart,
            out FusionC2CHarnessLaunchConfiguration configuration)
        {
            var resolvedMode = defaultMode;
            var resolvedSessionName = ResolveSessionName(defaultSessionName);
            var resolvedAutostart = defaultAutostart;

            if (args != null)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (string.IsNullOrEmpty(arg))
                    {
                        continue;
                    }

                    if (arg.StartsWith("--c2c-mode=", StringComparison.Ordinal))
                    {
                        var value = arg.Substring("--c2c-mode=".Length);
                        if (!TryParseMode(value, out resolvedMode))
                        {
                            configuration = default;
                            return false;
                        }
                    }
                    else if (arg.StartsWith("--c2c-session=", StringComparison.Ordinal))
                    {
                        var value = arg.Substring("--c2c-session=".Length);
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            configuration = default;
                            return false;
                        }

                        resolvedSessionName = value.Trim();
                    }
                    else if (arg.StartsWith("--c2c-autostart=", StringComparison.Ordinal))
                    {
                        var value = arg.Substring("--c2c-autostart=".Length);
                        if (!bool.TryParse(value, out resolvedAutostart))
                        {
                            configuration = default;
                            return false;
                        }
                    }
                }
            }

            configuration = new FusionC2CHarnessLaunchConfiguration(
                resolvedMode,
                resolvedSessionName,
                resolvedAutostart);
            return true;
        }

        internal static bool TryParseMode(string value, out FusionC2CHarnessMode parsedMode)
        {
            if (string.Equals(value, "host", StringComparison.OrdinalIgnoreCase))
            {
                parsedMode = FusionC2CHarnessMode.Host;
                return true;
            }

            if (string.Equals(value, "client", StringComparison.OrdinalIgnoreCase))
            {
                parsedMode = FusionC2CHarnessMode.Client;
                return true;
            }

            parsedMode = default;
            return false;
        }

        internal static string ResolveSessionName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DefaultSessionName
                : value.Trim();
        }

        private static GameMode ToFusionGameMode(FusionC2CHarnessMode harnessMode)
        {
            return harnessMode == FusionC2CHarnessMode.Client
                ? GameMode.Client
                : GameMode.Host;
        }

        private async Task WaitForInitialSnapshotReadinessAsync()
        {
            var role = _launchConfiguration.Mode.ToString();
            var timeoutSeconds = Mathf.Max(0f, initialSnapshotTimeoutSeconds);
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;

            Debug.Log($"C2C|INITIAL_WAIT|role={role}|timeout={timeoutSeconds}");

            while (Time.realtimeSinceStartup <= deadline)
            {
                if (IsInitialSnapshotReady(out var reason))
                {
                    Debug.Log(CreateInitialReadyLog(role, reason));
                    return;
                }

                await Task.Yield();
            }

            Debug.LogWarning(CreateInitialTimeoutLog(role));
        }

        private bool IsInitialSnapshotReady(out string reason)
        {
            if (_runner == null)
            {
                reason = "MissingRunner";
                return false;
            }

            if (!_runner.IsRunning)
            {
                reason = "RunnerNotRunning";
                return false;
            }

            var localPlayer = _runner.LocalPlayer;
            if (!localPlayer.IsRealPlayer)
            {
                reason = "LocalPlayerNotReal";
                return false;
            }

            if (!_runner.TryGetPlayerObject(localPlayer, out var playerObject) || playerObject == null)
            {
                reason = "MissingLocalPlayerObject";
                return false;
            }

            if (_launchConfiguration.Mode == FusionC2CHarnessMode.Client)
            {
                if (!playerObject.HasInputAuthority)
                {
                    reason = "MissingInputAuthority";
                    return false;
                }

                reason = "ClientLocalPlayerObjectReady";
                return true;
            }

            var lifecycle = _runner.GetComponent<FusionPlayerLifecycle>();
            if (lifecycle == null)
            {
                reason = "MissingFusionPlayerLifecycle";
                return false;
            }

            if (!lifecycle.IdentityRegistry.TryGetPlayerId(localPlayer, out var playerId) || !playerId.IsValid)
            {
                reason = "MissingLogicalPlayerId";
                return false;
            }

            if (!lifecycle.EntityRegistry.TryGetEntity(playerId, out var entity) || entity == null)
            {
                reason = "MissingRuntimeEntity";
                return false;
            }

            reason = $"HostLifecycleReady|playerId={playerId}";
            return true;
        }

        private string CreateInitialReadyLog(string role, string reason)
        {
            return $"C2C|INITIAL_READY|role={role}|reason={reason}|{CreateInitialStateSummary()}";
        }

        private string CreateInitialTimeoutLog(string role)
        {
            return $"C2C|INITIAL_TIMEOUT|role={role}|{CreateInitialStateSummary()}";
        }

        private string CreateInitialStateSummary()
        {
            var activeCount = 0;
            var localPlayer = PlayerRef.None;
            var hasLocalObject = false;
            var hasInputAuthority = false;
            var identityRegistryCount = 0;
            var entityRegistryCount = 0;

            if (_runner != null)
            {
                localPlayer = _runner.LocalPlayer;

                foreach (var _ in _runner.ActivePlayers)
                {
                    activeCount++;
                }

                if (localPlayer.IsRealPlayer
                    && _runner.TryGetPlayerObject(localPlayer, out var playerObject)
                    && playerObject != null)
                {
                    hasLocalObject = true;
                    hasInputAuthority = playerObject.HasInputAuthority;
                }

                var lifecycle = _runner.GetComponent<FusionPlayerLifecycle>();
                if (lifecycle != null)
                {
                    identityRegistryCount = lifecycle.IdentityRegistry.Count;
                    entityRegistryCount = lifecycle.EntityRegistry.Count;
                }
            }

            return $"running={(_runner != null && _runner.IsRunning)}|local={localPlayer}|active={activeCount}|localObject={hasLocalObject}|inputAuth={hasInputAuthority}|identityRegistry={identityRegistryCount}|entityRegistry={entityRegistryCount}";
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
        }

        private void CaptureProbeSnapshot()
        {
            var probe = GetComponent<FusionPlayerLifecycleProbe>();
            if (probe != null)
            {
                probe.CaptureSnapshot();
            }
        }
    }

    internal readonly struct FusionC2CHarnessLaunchConfiguration
    {
        public FusionC2CHarnessLaunchConfiguration(FusionC2CHarnessMode mode, string sessionName, bool autostart)
        {
            Mode = mode;
            SessionName = sessionName;
            Autostart = autostart;
        }

        public FusionC2CHarnessMode Mode { get; }

        public string SessionName { get; }

        public bool Autostart { get; }
    }
}
