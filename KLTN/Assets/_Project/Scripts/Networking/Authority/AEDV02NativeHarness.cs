#if AED_V02_PILOT_HARNESS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.AI.Stalker.Presentation;
using EchoProtocol.Auth;
using EchoProtocol.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

namespace EchoProtocol.Networking.Authority
{
    public sealed partial class AEDV02NativeHarness : MonoBehaviour
    {
        [Serializable] private sealed class Config
        {
            public string role, apiUrl, username, session, output, commandsFile, label;
            public string mode = "Adaptive";
            public string backendEnvironment = "Local";
            public int playerCount = 2;
            public bool gameplay, cabinet, autoRelief;
            public int timeoutSeconds = 600;
        }
        [Serializable] private sealed class Observation
        {
            public string role, utc, matchId, decisionId, fingerprint, actionFingerprint, roster, scene, policy, phase;
            public int revision, players;
            public bool consumerObserved;
            public double chaseSpeed;
            public string consumerKind, consumerFingerprint, stalkerId;
            public string windowId, decisionPoint;
            public int decisionPhaseOrdinal;
            public int consumerRevision, animatorState;
            public float presentedSpeed;
            public double[] consumerValues;
        }
        private static Config _config;
        private static string EvidenceRole => string.IsNullOrWhiteSpace(_config.label) ? _config.role : _config.label;
        private static string _password;
        private float _deadline, _nextRead;
        private bool _started;
        private Vector3 _driveGoal;
        private bool _hasDriveGoal;
        private NetworkPlayerMovement _driver;
        private bool _wasDowned;
        private float _nextAction;
        private float _driveStopDistance = .5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Configure()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-aedHarnessConfig");
            if (index < 0) return;
            bool allowProduction = Environment.GetEnvironmentVariable("ECHO_AED_ALLOW_PRODUCTION") == "1";
            Environment.SetEnvironmentVariable("ECHO_AED_ALLOW_PRODUCTION", null);
            try
            {
                if (!Debug.isDebugBuild || index + 1 >= args.Length) throw new InvalidOperationException("Invalid harness launch.");
                Application.runInBackground = true;
                _config = JsonUtility.FromJson<Config>(File.ReadAllText(args[index + 1]));
                _password = Environment.GetEnvironmentVariable("ECHO_AED_PILOT_PASSWORD");
                Environment.SetEnvironmentVariable("ECHO_AED_PILOT_PASSWORD", null);
                if (_config == null || _config.role is not ("Host" or "Client") || string.IsNullOrWhiteSpace(_config.output) ||
                    string.IsNullOrWhiteSpace(_config.username) || string.IsNullOrWhiteSpace(_password) ||
                    string.IsNullOrWhiteSpace(_config.session) || _config.timeoutSeconds < 30 || _config.timeoutSeconds > 1800)
                    throw new InvalidOperationException("Harness config incomplete.");
                if (_config.mode is not ("Fixed" or "Adaptive") || _config.playerCount < 2 || _config.playerCount > 4)
                    throw new InvalidOperationException("Harness mode/player count invalid.");
                if (EvidenceRole is not ("Host" or "Client" or "Client2" or "Client3") ||
                    (_config.role == "Host") != (EvidenceRole == "Host")) throw new InvalidOperationException("Invalid evidence role.");
                AEDPilotTokenPrefs.UseMemory = true;
                // SciFi scene load can stall Fusion updates longer than the normal 10-second timeout.
                Fusion.NetworkProjectConfig.Global.Network.ConnectionTimeout = 90f;
                var api = Resources.Load<EchoProtocol.Api.ApiConfiguration>("ApiConfiguration");
                if (api == null) throw new InvalidOperationException("ApiConfiguration missing.");
                api.ConfigurePilotBackend(_config.apiUrl, _config.backendEnvironment, allowProduction);
                Debug.Log($"[AED_NATIVE][BACKEND_TARGET] environment={_config.backendEnvironment} api={api.BaseUrl}");
                Directory.CreateDirectory(_config.output);
            }
            catch (Exception ex) { _config = null; _password = null; Debug.LogError("[AED_NATIVE][CONFIG_FAILED] " + ex.GetType().Name); Application.Quit(2); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (_config == null) return;
            var settings = ScenarioConfigAuthorityRuntime.EnsureExists().RuntimeSettings;
            if (settings == null) { Fail("RUNTIME_SETTINGS_MISSING"); return; }
            settings.ConfigurePilotHarness(_config.gameplay, _config.cabinet);
            var go = new GameObject("AEDV02 Native Harness");
            DontDestroyOnLoad(go);
            go.AddComponent<AEDV02NativeHarness>().StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            var auth = AuthRuntime.EnsureExists();
            bool done = false, success = false;
            auth.AuthService.Login(_config.username, _password, result => { success = result.IsSuccess && result.Data?.success == true; done = true; });
            _password = null;
            float deadline = Time.realtimeSinceStartup + 30;
            while (!done && Time.realtimeSinceStartup < deadline) yield return null;
            if (!done || !success) { Fail("AUTH_FAILED"); yield break; }
            done = false; success = false;
            auth.AuthService.GetCurrentUser(result => { success = result.IsSuccess && result.Data?.success == true; done = true; });
            deadline = Time.realtimeSinceStartup + 30;
            while (!done && Time.realtimeSinceStartup < deadline) yield return null;
            if (!done || !success) { Fail("IDENTITY_FAILED"); yield break; }
            SceneManager.LoadScene("MainMenu");
            yield return null;
            if (ScenarioConfigAuthorityRuntime.EnsureExists().RuntimeSettings?.PacingV02GameplayEnabled != _config.gameplay)
            { Fail("GAMEPLAY_GATE_LOST"); yield break; }
            deadline = Time.realtimeSinceStartup + 30;
            while (NetworkBootstrap.Instance == null && Time.realtimeSinceStartup < deadline) yield return null;
            var bootstrap = NetworkBootstrap.Instance;
            if (bootstrap == null) { Fail("BOOTSTRAP_MISSING"); yield break; }
            Task<bool> session = _config.role == "Host"
                ? bootstrap.CreateRoomAsync(_config.session, _config.playerCount, MatchDifficulty.Normal, _config.mode == "Fixed" ? ScenarioResolutionMode.Fixed : ScenarioResolutionMode.Adaptive)
                : bootstrap.JoinRoomAsync(_config.session);
            deadline = Time.realtimeSinceStartup + 60;
            while (!session.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            if (!session.IsCompleted || session.IsFaulted || !session.Result) { Fail("SESSION_FAILED"); yield break; }
            File.WriteAllText(Path.Combine(_config.output, EvidenceRole + "-room-ready.txt"), _config.session);
            var harness = FindAnyObjectByType<AEDV02NativeHarness>();
            harness._deadline = Time.realtimeSinceStartup + _config.timeoutSeconds;
            harness._started = true;
        }

        private void Update()
        {
            if (!_started) return;
            if (Time.realtimeSinceStartup >= _deadline) { _started = false; Fail("TRANSACTION_NOT_PROVEN_BEFORE_DEADLINE"); return; }
            var bootstrap = NetworkBootstrap.Instance;
            var runner = bootstrap?.Runner;
            PollNativeControls(bootstrap);
            if (runner == null || !runner.IsRunning) return;
            if (_config.autoRelief && bootstrap.State == NetworkSessionState.InMatch) DriveRelief(bootstrap);
            if (bootstrap.State == NetworkSessionState.InLobby)
            {
                if (runner.TryGetPlayerObject(runner.LocalPlayer, out var local) && local.TryGetComponent<LobbyPlayerState>(out var lobby) && !lobby.IsReady)
                    lobby.RequestReady(true);
                var members = FindObjectsByType<LobbyPlayerState>(FindObjectsInactive.Exclude).Where(x => x.Runner == runner).ToArray();
                if (_config.role == "Host" && members.Length == _config.playerCount && members.All(x => x.IsReady))
                    FindAnyObjectByType<LobbyManager>()?.TryStartMatch();
            }
            if (Time.realtimeSinceStartup < _nextRead) return;
            _nextRead = Time.realtimeSinceStartup + 1;
            WriteNativeWorldReadback(bootstrap);
            var state = NetworkMatchState.Instance;
            if (state == null || state.Object == null || !state.Object.IsValid ||
                !Guid.TryParse(state.AEDV02MatchId.ToString(), out var match) || !state.TryReadV02Plan(match, out var plan, out var revision)) return;
            var stalkers = FindObjectsByType<StalkerFusionRuntime>(FindObjectsInactive.Exclude).Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid)
                .OrderBy(x => x.Object.Id.ToString(), StringComparer.Ordinal).ToArray();
            var values = new double[6];
            bool observed = stalkers.Length > 0;
            foreach (var stalker in stalkers)
                for (int key = 0; key < values.Length; key++)
                {
                    bool read = _config.role == "Host" ? stalker.TryReadV02ConsumerValue((AEDV02Key)key, out values[key]) :
                        stalker.TryReadReplicatedV02ConsumerValue((AEDV02Key)key, out values[key]);
                    observed &= read && stalker.V02ConsumerMatchId.ToString() == match.ToString("D") &&
                        stalker.V02ConsumerRevision == revision && stalker.V02ConsumerFingerprint.ToString() == plan.Fingerprint() &&
                        Math.Abs(values[key] - plan.Get((AEDV02Key)key)) < .000001;
                }
            float presentedSpeed = 0; int animatorState = 0;
            if (_config.role == "Client")
            {
                var presenter = stalkers.FirstOrDefault()?.GetComponentInChildren<StalkerAnimatorPresenter>(true);
                observed &= presenter != null && presenter.TryReadNativePresentation(revision, plan.Fingerprint(), out presentedSpeed, out animatorState);
            }
            var record = new Observation { role = _config.role, utc = DateTime.UtcNow.ToString("O"), matchId = match.ToString("D"),
                decisionId = state.AEDV02DecisionId.ToString(), revision = revision, fingerprint = plan.Fingerprint(),
                actionFingerprint = state.AEDV02ActionFingerprint.ToString(), roster = state.AEDV02RosterIdentity.ToString(),
                scene = state.AEDV02SceneFingerprint.ToString(), policy = AEDV02PilotPolicy.Version, phase = state.CurrentPhase.ToString(),
                players = runner.ActivePlayers.Count(), consumerObserved = observed, chaseSpeed = values[(int)AEDV02Key.ChaseSpeed],
                consumerValues = values, consumerKind = _config.role == "Host" ? "StalkerFusionRuntime" : "StalkerAnimatorPresenter",
                consumerRevision = stalkers.FirstOrDefault()?.V02ConsumerRevision ?? 0,
                consumerFingerprint = stalkers.FirstOrDefault()?.V02ConsumerFingerprint.ToString(), stalkerId = stalkers.FirstOrDefault()?.Object.Id.ToString(),
                presentedSpeed = presentedSpeed, animatorState = animatorState };
            record.windowId = state.AEDV02WindowId.ToString(); record.decisionPoint = state.AEDV02DecisionPoint.ToString();
            record.decisionPhaseOrdinal = state.AEDV02DecisionPhaseOrdinal;
            File.AppendAllText(Path.Combine(_config.output, EvidenceRole + "-readback.jsonl"), JsonUtility.ToJson(record) + Environment.NewLine);
            Debug.Log($"[AED_NATIVE][READBACK] role={record.role} match={record.matchId} decision={record.decisionId} revision={revision} fingerprint={record.fingerprint} consumer={observed}");
        }

        private void DriveRelief(NetworkBootstrap bootstrap)
        {
            var runner = bootstrap.Runner;
            if (!runner.TryGetPlayerObject(runner.LocalPlayer, out var local) ||
                !local.TryGetComponent<NetworkPlayerLifeState>(out var life) || !local.TryGetComponent<NetworkPlayerMovement>(out var movement)) return;
            _driver = movement;
            _wasDowned |= life.IsDowned;
            _hasDriveGoal = false;
            _driveStopDistance = .5f;
            var stalker = FindObjectsByType<StalkerFusionRuntime>(FindObjectsInactive.Exclude).FirstOrDefault(x => x.Runner == runner);
            if (stalker == null) return;
            var interactor = local.GetComponent<NetworkPlayerInteractor>();
            if (_config.role == "Host" && (!_wasDowned || NetworkMatchState.Instance?.AEDV02PlanRevision > 1))
            {
                // Real movement into the existing AI's perception/attack path; no damage or telemetry injection.
                _driveGoal = stalker.transform.position; _hasDriveGoal = true;
            }
            else if (_config.role == "Client")
            {
                var ally = FindObjectsByType<NetworkPlayerLifeState>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(x => x.Runner == runner && x.Object != local && x.IsDowned);
                var lobby = local.GetComponent<LobbyPlayerState>();
                if (lobby != null && lobby.ToolId == LobbyPlayerState.FirstAidKitToolId && ally != null)
                {
                    _driveGoal = ally.transform.position; _hasDriveGoal = true;
                    _driveStopDistance = 1.6f;
                    if (Vector3.Distance(local.transform.position, _driveGoal) < 2 && Time.realtimeSinceStartup >= _nextAction)
                    {
                        var aim = _driveGoal - local.transform.position;
                        FindAnyObjectByType<PlayerCamera>()?.SetRotation(Quaternion.LookRotation(new Vector3(aim.x, 0, aim.z)).eulerAngles.y, 35);
                        interactor?.RequestUseTeamTool(); _nextAction = Time.realtimeSinceStartup + 1;
                    }
                }
                else if (lobby != null && lobby.ToolId == 0)
                {
                    var kit = FindObjectsByType<NetworkTeamToolPickup>(FindObjectsInactive.Exclude)
                        .Where(x => x.Runner == runner && x.IsAvailableWorldSource && x.ToolId == LobbyPlayerState.FirstAidKitToolId)
                        .OrderBy(x => Vector3.SqrMagnitude(x.transform.position - local.transform.position)).FirstOrDefault();
                    if (kit != null)
                    {
                        _driveGoal = kit.transform.position; _hasDriveGoal = true;
                        if (Vector3.Distance(local.transform.position, _driveGoal) < 2 && Time.realtimeSinceStartup >= _nextAction)
                        { interactor?.RequestInteraction(kit); _nextAction = Time.realtimeSinceStartup + 1; }
                    }
                }
            }
            if (!_hasDriveGoal && !life.IsDowned)
            {
                var entry = FindObjectsByType<AEDZoneEntryMarker>(FindObjectsInactive.Exclude).Where(x => x.Zone == 1)
                    .OrderByDescending(x => Vector3.SqrMagnitude(x.transform.position - stalker.transform.position)).FirstOrDefault();
                if (entry != null) { _driveGoal = entry.transform.position; _hasDriveGoal = true; }
            }
            if (_hasDriveGoal && !life.IsDowned && !life.IsCaught && interactor != null && Time.realtimeSinceStartup >= _nextAction)
            {
                var door = FindObjectsByType<NetworkSlidingDoor>(FindObjectsInactive.Exclude)
                    .Where(x => x.Runner == runner && x.CurrentState == NetworkDoorState.Closed && !x.HasActiveJammer &&
                        Vector3.Distance(local.transform.position, x.TraversalBlockerCenter) < 2.5f)
                    .OrderBy(x => Vector3.SqrMagnitude(x.transform.position - local.transform.position)).FirstOrDefault();
                if (door != null)
                {
                    // Ordinary interaction RPC still enforces reach, LOS, locks and authority; the bot cannot unlock a route.
                    interactor.RequestInteraction(door); _nextAction = Time.realtimeSinceStartup + 1;
                }
            }
            bootstrap.RegisterLocalInputProvider(local, ReadDriveInput);
        }

        private NetworkPlayerInput ReadDriveInput()
        {
            if (_driver == null) return default;
            var camera = FindAnyObjectByType<PlayerCamera>();
            var idle = new NetworkPlayerInput { LookYaw = camera != null ? camera.Yaw : _driver.transform.eulerAngles.y,
                LookPitch = camera != null ? camera.Pitch : 0 };
            if (!_hasDriveGoal) return idle;
            var start = _driver.transform.position;
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(start, out var from, 2, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(_driveGoal, out var to, 2, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                return idle;
            var next = path.corners.Skip(1).FirstOrDefault(x => Vector3.Distance(start, x) > .5f);
            var direction = next == default ? Vector3.zero : next - start;
            direction.y = 0;
            float yaw = direction.sqrMagnitude < .01f ? _driver.transform.eulerAngles.y : Quaternion.LookRotation(direction).eulerAngles.y;
            bool arrived = Vector3.Distance(start, _driveGoal) <= _driveStopDistance;
            if (!arrived) camera?.SetRotation(yaw);
            return new NetworkPlayerInput { Move = arrived || direction.sqrMagnitude < .01f ? Vector2.zero : Vector2.up,
                LookYaw = arrived ? idle.LookYaw : yaw, LookPitch = camera != null ? camera.Pitch : idle.LookPitch, SprintHeld = true };
        }

        private static void Fail(string reason)
        {
            Debug.LogError("[AED_NATIVE][FAILED] " + reason);
            if (_config != null && !string.IsNullOrWhiteSpace(_config.output))
                File.WriteAllText(Path.Combine(_config.output, EvidenceRole + "-failure.txt"), reason);
            Application.Quit(3);
        }
    }
}
#endif
