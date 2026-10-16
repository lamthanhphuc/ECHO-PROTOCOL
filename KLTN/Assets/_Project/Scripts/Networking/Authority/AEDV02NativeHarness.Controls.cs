#if AED_V02_PILOT_HARNESS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Minions;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.Tools.Scanner;
using Fusion;
using UnityEngine;

namespace EchoProtocol.Networking.Authority
{
    public sealed partial class AEDV02NativeHarness
    {
        [Serializable] private sealed class Command { public string id, action, target; }
        [Serializable] private sealed class ControlReceipt
        {
            public string id, action, target, status, utc, match, fingerprint, sessionState;
            public int revision, reconnectAttempt;
            public bool reconnecting;
        }
        private readonly HashSet<Guid> _controlIds = new HashSet<Guid>();
        private float _nextControlPoll;
        private Task _lifecycleCommand;
        private Command _pendingLifecycle;
        private bool _controlledDrive;
        private string _followTarget;

        private void PollNativeControls(NetworkBootstrap bootstrap)
        {
            if (bootstrap == null || string.IsNullOrWhiteSpace(_config.commandsFile)) return;
            if (_lifecycleCommand != null && _lifecycleCommand.IsCompleted)
            {
                ControlResult(_pendingLifecycle, _lifecycleCommand.IsFaulted || _lifecycleCommand.IsCanceled ||
                    _lifecycleCommand is Task<bool> joined && !joined.Result ? "FAILED" : "REQUEST_COMPLETED", bootstrap);
                _lifecycleCommand = null; _pendingLifecycle = null;
            }
            var runner = bootstrap.Runner;
            if (_controlledDrive && runner != null && runner.IsRunning && runner.TryGetPlayerObject(runner.LocalPlayer, out var controlled))
            {
                _driver = controlled.GetComponent<NetworkPlayerMovement>(); bootstrap.RegisterLocalInputProvider(controlled, ReadDriveInput);
                if (!string.IsNullOrEmpty(_followTarget))
                {
                    var target = FindObjectsByType<NetworkObject>(FindObjectsInactive.Exclude)
                        .FirstOrDefault(x => x.Runner == runner && x.IsValid && x.Id.ToString() == _followTarget);
                    _hasDriveGoal = target != null; if (target != null) _driveGoal = target.transform.position;
                }
            }
            if (Time.realtimeSinceStartup < _nextControlPoll) return;
            _nextControlPoll = Time.realtimeSinceStartup + .25f;
            try
            {
                string root = Path.GetFullPath(_config.output).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string path = Path.GetFullPath(_config.commandsFile);
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Control path is outside evidence directory.");
                if (!File.Exists(path)) return;
                if (new FileInfo(path).Length > 65536) throw new InvalidOperationException("Control journal limit exceeded.");
                string content;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream)) content = reader.ReadToEnd();
                int end = content.LastIndexOf('\n');
                if (end < 0) return;
                foreach (string line in content.Substring(0, end).Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    var command = JsonUtility.FromJson<Command>(line);
                    if (command == null || !Guid.TryParse(command.id, out var id) || id == Guid.Empty) throw new FormatException("Invalid command identity.");
                    if (_controlIds.Contains(id)) continue;
                    if (_lifecycleCommand != null) break;
                    _controlIds.Add(id); _config.autoRelief = false;
                    try { ExecuteNativeControl(command, bootstrap); }
                    catch (Exception ex) { ControlResult(command, "REJECTED:" + ex.GetType().Name, bootstrap); }
                    break; // Give normal input/replication a frame before consuming another action.
                }
            }
            catch (Exception ex) { Debug.LogError("[AED_NATIVE][CONTROL_REJECTED] " + ex.GetType().Name); }
        }

        private void ExecuteNativeControl(Command command, NetworkBootstrap bootstrap)
        {
            var runner = bootstrap.Runner;
            switch (command.action)
            {
                case "transport-loss":
                    if (_config.role != "Client" || runner == null || !runner.IsRunning || bootstrap.State != NetworkSessionState.InMatch)
                        throw new InvalidOperationException("Transport-loss requires an active remote Client.");
                    _pendingLifecycle = command; _lifecycleCommand = runner.Shutdown(false, ShutdownReason.Error); return;
                case "shutdown": _pendingLifecycle = command; _lifecycleCommand = bootstrap.Shutdown(); return;
                case "leave": _pendingLifecycle = command; _lifecycleCommand = bootstrap.LeaveToMainMenuAsync(); return;
                case "join":
                    if (_config.role != "Client" || bootstrap.IsBusy || runner != null && runner.IsRunning) throw new InvalidOperationException("Client already connected.");
                    _pendingLifecycle = command; _lifecycleCommand = bootstrap.JoinRoomAsync(_config.session); return;
                case "stop": _hasDriveGoal = false; _followTarget = null; _controlledDrive = true; break;
                case "manual-input":
                    _controlledDrive = false; _hasDriveGoal = false; _followTarget = null; _driver = null;
                    bootstrap.ReleaseNativeInput(); break;
                default:
                    if (runner == null || !runner.IsRunning || !runner.TryGetPlayerObject(runner.LocalPlayer, out var local))
                        throw new InvalidOperationException("Local player unavailable.");
                    var interactor = local.GetComponent<NetworkPlayerInteractor>();
                    switch (command.action)
                    {
                        case "move-marker":
                            if (!AEDV02SceneSource.TryLoad(out var manifest, out _)) throw new InvalidOperationException("Scene manifest unavailable.");
                            var marker = manifest.Markers.Single(x => x.Id == command.target);
                            _followTarget = null;
                            _driveGoal = new Vector3((float)marker.X, (float)marker.Y, (float)marker.Z);
                            _driveStopDistance = .5f; _hasDriveGoal = _controlledDrive = true; break;
                        case "move-target":
                            var goal = FindObjectsByType<NetworkObject>(FindObjectsInactive.Exclude).Single(x => x.Runner == runner && x.IsValid && x.Id.ToString() == command.target);
                            _followTarget = command.target;
                            _driveGoal = goal.transform.position; _driveStopDistance = 1.5f; _hasDriveGoal = _controlledDrive = true; break;
                        case "aim-target":
                            var aimTarget = FindObjectsByType<NetworkObject>(FindObjectsInactive.Exclude).Single(x => x.Runner == runner && x.IsValid && x.Id.ToString() == command.target);
                            var aim = aimTarget.transform.position - local.transform.position;
                            if (aim.sqrMagnitude < .001f) throw new InvalidOperationException("Aim target overlaps player.");
                            var euler = Quaternion.LookRotation(aim).eulerAngles;
                            FindAnyObjectByType<PlayerCamera>()?.SetRotation(euler.y, euler.x); break;
                        case "interact":
                            var target = FindObjectsByType<NetworkInteractable>(FindObjectsInactive.Exclude)
                                .Single(x => x.Runner == runner && x.Object != null && x.Object.IsValid && x.Object.Id.ToString() == command.target);
                            if (interactor == null || !interactor.RequestInteraction(target)) throw new InvalidOperationException("Interaction not submitted."); break;
                        case "hide":
                            if (!ulong.TryParse(command.target, out var spotId) || !global::HidingSpot.TryResolveByStableId(spotId, out var spot) ||
                                spot.InspectPoint == null || Vector3.Distance(local.transform.position, spot.InspectPoint.position) > 2 || !spot.CanInteract(local.gameObject))
                                throw new InvalidOperationException("Cabinet interaction unavailable.");
                            spot.Interact(local.gameObject); break;
                        case "exit-hide": local.GetComponent<global::PlayerHidingController>()?.ExitHiding(); break;
                        case "flashlight": local.GetComponent<NetworkPlayerFlashlight>()?.RequestNativeToggle(); break;
                        case "use-tool": if (interactor == null || !interactor.RequestUseTeamTool()) throw new InvalidOperationException("Tool request rejected."); break;
                        case "drop-tool": if (interactor == null || !interactor.RequestDropTeamTool()) throw new InvalidOperationException("Drop rejected."); break;
                        case "scan-core":
                        case "scan-motion":
                            var scanner = local.GetComponent<NetworkFieldScanner>();
                            if (scanner == null || !scanner.RequestSetMode(command.action == "scan-core" ? FieldScannerMode.Core : FieldScannerMode.Motion) ||
                                !scanner.RequestScan()) throw new InvalidOperationException("Scan request rejected."); break;
                        default: throw new ArgumentException("Unknown native control.");
                    }
                    break;
            }
            ControlResult(command, "REQUEST_SUBMITTED", bootstrap);
        }

        private static void ControlResult(Command command, string status, NetworkBootstrap bootstrap)
        {
            var state = NetworkMatchState.Instance;
            File.AppendAllText(Path.Combine(_config.output, EvidenceRole + "-controls.jsonl"), JsonUtility.ToJson(new ControlReceipt {
                id = command.id, action = command.action, target = command.target, status = status, utc = DateTime.UtcNow.ToString("O"),
                match = state != null && state.Object != null && state.Object.IsValid ? state.AEDV02MatchId.ToString() : string.Empty,
                revision = state != null && state.Object != null && state.Object.IsValid ? state.AEDV02PlanRevision : 0,
                fingerprint = state != null && state.Object != null && state.Object.IsValid ? state.AEDV02PlanFingerprint.ToString() : string.Empty,
                sessionState = bootstrap.State.ToString(), reconnecting = bootstrap.IsReconnecting, reconnectAttempt = bootstrap.ReconnectAttempt
            }) + Environment.NewLine);
            // This journal acknowledges input submission, never Backend approval or a gameplay effect.
        }

        [Serializable] private sealed class WorldReadback
        {
            public string utc, role, mode, match, phase, sessionState;
            public long tick;
            public double[] plan;
            public int revision, reconnectAttempt;
            public bool reconnecting;
            public PlayerReadback[] players;
            public ActorReadback[] stalkers, minions, tools;
            public AEDV02SourceResultDto verifiedSource;
        }
        [Serializable] private sealed class PlayerReadback
        {
            public string id, userId, life, hideSpot, protectionToken;
            public bool hidden, caught, local, flashlight;
            public int toolId, grantsUsed;
            public long slowUntilTick;
            public float x, y, z, protectionSeconds, graceSeconds;
        }
        [Serializable] private sealed class ActorReadback
        {
            public string id, state;
            public int zone, toolId, charges;
            public float x, y, z;
            public double[] actualConsumer;
        }
        private static void WriteNativeWorldReadback(NetworkBootstrap bootstrap)
        {
            var runner = bootstrap.Runner; var state = NetworkMatchState.Instance; var authority = MatchAuthorityRuntime.Instance;
            if (runner == null || !runner.IsRunning) return;
            var players = FindObjectsByType<NetworkPlayerLifeState>(FindObjectsInactive.Exclude)
                .Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid).Select(life =>
                {
                    var movement = life.GetComponent<NetworkPlayerMovement>(); var lobby = life.GetComponent<LobbyPlayerState>(); var p = life.transform.position;
                    return new PlayerReadback { id = life.Object.Id.ToString(), local = life.Object.HasInputAuthority,
                        userId = authority != null && authority.TryResolveVerifiedBackendUserId(life.Object.InputAuthority, out var identity) ? identity : string.Empty,
                        life = life.IsEliminated ? "ELIMINATED" : life.IsDowned ? "DOWNED" : "ALIVE", caught = life.IsCaught,
                        toolId = lobby != null ? lobby.ToolId : 0, hidden = movement != null && movement.IsHidden,
                        flashlight = life.GetComponent<NetworkPlayerFlashlight>()?.IsEmittingLight == true,
                        hideSpot = movement != null ? movement.CurrentHideSpotId.ToString() : "0",
                        slowUntilTick = movement != null ? movement.V02SlowUntilTick : 0,
                        protectionToken = movement != null ? movement.AEDV02HideTokenId.ToString() : string.Empty,
                        grantsUsed = movement != null ? movement.AEDV02HideGrantsUsed : 0,
                        protectionSeconds = movement != null ? movement.AEDV02HideProtectionTimer.RemainingTime(runner) ?? 0 : 0,
                        graceSeconds = movement != null ? movement.AEDV02HideGraceTimer.RemainingTime(runner) ?? 0 : 0,
                        x = p.x, y = p.y, z = p.z };
                }).ToArray();
            var stalkers = FindObjectsByType<StalkerFusionRuntime>(FindObjectsInactive.Exclude)
                .Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid).Select(x =>
                {
                    var values = new double[6]; bool read = true;
                    for (int i = 0; i < values.Length; i++) read &= _config.role == "Host" ? x.TryReadV02ConsumerValue((EchoProtocol.AI.Common.AED.AEDV02Key)i, out values[i]) :
                        x.TryReadReplicatedV02ConsumerValue((EchoProtocol.AI.Common.AED.AEDV02Key)i, out values[i]);
                    var p = x.transform.position;
                    return new ActorReadback { id = x.Object.Id.ToString(), state = x.ReplicatedState.ToString(), x = p.x, y = p.y, z = p.z,
                        actualConsumer = read ? values : Array.Empty<double>() };
                }).ToArray();
            var minions = FindObjectsByType<CreepMinionRuntime>(FindObjectsInactive.Exclude)
                .Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid).Select(x => new ActorReadback {
                    id = x.Object.Id.ToString(), state = x.State.ToString(), zone = (int)x.Zone, x = x.transform.position.x, y = x.transform.position.y, z = x.transform.position.z }).ToArray();
            var tools = FindObjectsByType<NetworkTeamToolPickup>(FindObjectsInactive.Exclude)
                .Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid && x.IsAvailableWorldSource).Select(x => new ActorReadback {
                    id = x.Object.Id.ToString(), toolId = x.ToolId, charges = x.RemainingUses, x = x.transform.position.x, y = x.transform.position.y, z = x.transform.position.z })
                .Concat(FindObjectsByType<NetworkToolPickup>(FindObjectsInactive.Exclude)
                    .Where(x => x.Runner == runner && x.Object != null && x.Object.IsValid && !x.IsPickedUp).Select(x => new ActorReadback {
                        id = x.Object.Id.ToString(), toolId = x.ToolId, charges = x.RemainingUses, x = x.transform.position.x, y = x.transform.position.y, z = x.transform.position.z })).ToArray();
            double[] actualPlan = Array.Empty<double>();
            if (state != null && state.Object != null && state.Object.IsValid && Guid.TryParse(state.AEDV02MatchId.ToString(), out var match) &&
                state.TryReadV02Plan(match, out var plan, out _))
                actualPlan = Enum.GetValues(typeof(EchoProtocol.AI.Common.AED.AEDV02Key)).Cast<EchoProtocol.AI.Common.AED.AEDV02Key>().Select(plan.Get).ToArray();
            var world = new WorldReadback { utc = DateTime.UtcNow.ToString("O"), role = _config.role, mode = _config.mode, plan = actualPlan,
                match = authority != null && authority.MatchId != Guid.Empty ? authority.MatchId.ToString("D") : state != null && state.Object != null && state.Object.IsValid ? state.AEDV02MatchId.ToString() : string.Empty,
                phase = state != null && state.Object != null && state.Object.IsValid ? state.CurrentPhase.ToString() : string.Empty,
                revision = state != null && state.Object != null && state.Object.IsValid ? state.AEDV02PlanRevision : 0,
                sessionState = bootstrap.State.ToString(), reconnecting = bootstrap.IsReconnecting, reconnectAttempt = bootstrap.ReconnectAttempt,
                tick = runner.Tick.Raw, players = players, stalkers = stalkers, minions = minions, tools = tools,
                verifiedSource = _config.role == "Host" ? authority?.LastVerifiedV02Source : null };
            File.AppendAllText(Path.Combine(_config.output, EvidenceRole + "-world.jsonl"), JsonUtility.ToJson(world) + Environment.NewLine);
        }
    }
}
#endif
