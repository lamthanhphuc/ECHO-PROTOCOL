using System;
using EchoProtocol.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using EchoProtocol.AI.AED;
using EchoProtocol.AI.Common.AED;
using EchoProtocol.Gameplay;
using EchoProtocol.Networking.Authority;
using UnityEngine;

namespace EchoProtocol.Networking
{
    /// <summary>
    /// Projects Fusion's replicated ActivePlayers collection into lobby UI state.
    /// The projection is never used as the authoritative member store.
    /// </summary>
    public sealed class LobbyManager : MonoBehaviour
    {
        public const string GameSceneName = "SciFi";

        [SerializeField] private NetworkBootstrap _bootstrap;
        private bool _matchStartInProgress;
        private CancellationTokenSource _preMatchCancellation;
        private BackendAdaptiveInputSnapshotProvider _snapshotProvider;
        private Task<bool> _lobbyBindingTask;
        private float _nextLobbyBindingAttempt;
        public bool IsStartingMatch => _matchStartInProgress;
        private string _localOperatorName = string.Empty;
        private LobbyPlayerState _namedPlayer;

        public string LocalOperatorName => _localOperatorName;

        public bool SetLocalOperatorName(string name)
        {
            var normalized = LobbyPlayerState.NormalizeOperatorName(name);
            if (normalized.Length == 0) return false;
            _localOperatorName = normalized;
            _namedPlayer = null;
            return true;
        }

        private void Update()
        {
            // Player objects can spawn after StartGame completes. Submit once per owned object.
            if (_localOperatorName.Length != 0 && IsInRoom
                && TryGetLocalPlayerState(out var player, false) && player != _namedPlayer
                && player.RequestOperatorName(_localOperatorName))
                _namedPlayer = player;

            var runner = _bootstrap?.Runner;
            if (_bootstrap?.State != NetworkSessionState.InLobby
                || runner == null || !runner.IsRunning || !runner.IsServer
                || !runner.IsSceneAuthority || !runner.SessionInfo.IsValid
                || _matchStartInProgress || Time.realtimeSinceStartup < _nextLobbyBindingAttempt
                || _lobbyBindingTask is { IsCompleted: false }) return;

            var authority = MatchAuthorityRuntime.EnsureExists(_bootstrap);
            if (authority.IsCompletingMatch || !authority.RequiresFreshHostBinding) return;
            _nextLobbyBindingAttempt = Time.realtimeSinceStartup + 3f;
            _lobbyBindingTask = PrepareLobbyBindingAsync(runner, authority);
        }

        public event Action<RoomInfoViewModel> OnRoomUpdated;
        public event Action<string> OnLobbyError;
        public event Action<LobbySelectionResult> OnSelectionRequestCompleted;

        public RoomInfoViewModel CurrentState { get; private set; } = new RoomInfoViewModel();
        public bool IsInRoom => _bootstrap != null && _bootstrap.HasRunningRunner;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindAnyObjectByType<NetworkBootstrap>();
        }

        private void OnEnable()
        {
            if (_bootstrap == null) _bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            if (_bootstrap == null) return;

            _bootstrap.PlayerJoined += HandlePlayerChanged;
            _bootstrap.PlayerLeft += HandlePlayerChanged;
            _bootstrap.SessionStateChanged += HandleSessionStateChanged;
            LobbyPlayerState.AnyStateChanged += RefreshFromRunner;
            LobbyPlayerState.LocalSelectionRequestCompleted += HandleSelectionResult;
            RefreshFromRunner();
        }

        private void OnDisable()
        {
            if (_bootstrap == null) return;
            _bootstrap.PlayerJoined -= HandlePlayerChanged;
            _bootstrap.PlayerLeft -= HandlePlayerChanged;
            _bootstrap.SessionStateChanged -= HandleSessionStateChanged;
            LobbyPlayerState.AnyStateChanged -= RefreshFromRunner;
            LobbyPlayerState.LocalSelectionRequestCompleted -= HandleSelectionResult;
        }

        public async void CreateRoom(string roomName, int maxPlayers = 4)
        {
            if (!ValidateRoomName(roomName)) return;
            var created = await _bootstrap.CreateRoomAsync(roomName, maxPlayers);
            if (!created) ReportError(_bootstrap.LastError);
        }

        public async void JoinRoom(string roomName)
        {
            if (!ValidateRoomName(roomName)) return;
            var joined = await _bootstrap.JoinRoomAsync(roomName);
            if (!joined) ReportError(_bootstrap.LastError);
        }

        public async void LeaveRoom()
        {
            if (_bootstrap == null || _bootstrap.IsBusy) return;
            ClearPreparedSnapshot();
            await _bootstrap.ShutdownRunnerAsync();
            RefreshFromRunner();
        }

        public void SetReady(bool isReady)
        {
            if (!IsInRoom)
            {
                ReportError("Cannot set ready - not connected to a room.");
                return;
            }

            var runner = _bootstrap.Runner;
            if (!runner.TryGetPlayerObject(runner.LocalPlayer, out var playerObject)
                || !playerObject.TryGetComponent<LobbyPlayerState>(out var playerState))
            {
                ReportError("Local lobby player state is not available yet.");
                return;
            }

            if (!playerState.RequestReady(isReady))
            {
                ReportError("Ready request rejected: local player does not own this network object.");
            }
        }

        public bool RequestTeam(int teamId)
        {
            if (!TryGetLocalPlayerState(out var state)) return false;
            return state.RequestTeam(teamId);
        }

        public bool RequestTool(int toolId)
        {
            if (!TryGetLocalPlayerState(out var state)) return false;
            return state.RequestTool(toolId);
        }

        public bool TryGetLocalPlayerState(out LobbyPlayerState state, bool reportError = true)
        {
            state = null;
            var runner = _bootstrap?.Runner;
            if (runner == null || !runner.IsRunning
                || !runner.TryGetPlayerObject(runner.LocalPlayer, out var playerObject)
                || !playerObject.TryGetComponent(out state))
            {
                if (reportError) ReportError("Local lobby player state is not available yet.");
                return false;
            }
            return true;
        }

        public bool TryStartMatch()
        {
            if (_matchStartInProgress)
            {
                Debug.LogWarning("[LobbyManager] Start match ignored: a match start is already in progress.");
                return false;
            }

            if (_bootstrap == null || _bootstrap.State != NetworkSessionState.InLobby)
            {
                ReportError("Start rejected: session is not in the lobby.");
                return false;
            }

            var runner = _bootstrap.Runner;
            if (runner == null || !runner.IsRunning || !runner.IsServer || !runner.IsSceneAuthority)
            {
                ReportError("Only the authoritative host can start the match.");
                return false;
            }

            var state = BuildStateFromRunner();
            if (!state.CanStartMatch)
            {
                ReportError("Start rejected: 1-4 players are required and every non-host player must be ready.");
                return false;
            }

            if (!_bootstrap.TryBeginMatchStartGate())
            {
                ReportError("Start rejected: the room roster could not be frozen.");
                return false;
            }

            _matchStartInProgress = true;

            string loadingAttempt;
            try
            {
                loadingAttempt = MatchLoadingCoordinator.EnsureExists(_bootstrap).BeginHost(
                    runner, LobbyMapCatalog.SceneName(LobbyMapCatalog.Selected(runner)));
            }
            catch (Exception error)
            {
                _bootstrap.CancelMatchStartGate();
                _matchStartInProgress = false;
                ReportError($"Could not announce match loading: {error.Message}");
                return false;
            }

            RuntimeLog.Log(
                RuntimeLogCategory.Lobby,
                $"[LobbyManager] Host validated {state.CurrentPlayers} ready players. Confirming backend authority.");

            _ = StartMatchAsync(runner, loadingAttempt);
            return true;
        }

        private async Task StartMatchAsync(NetworkRunner runner, string loadingAttempt)
        {
            string selectedScene = LobbyMapCatalog.SceneName(LobbyMapCatalog.Selected(runner));
            var loading = MatchLoadingCoordinator.Instance;
            MatchAuthorityRuntime authority = null;
            bool backendAccepted = false, sceneRequested = false, backendAbortAttempted = false;
            try
            {
                // Every frozen peer must have displayed the overlay before backend/scene work starts.
                if (loading == null || !await loading.WaitForAnnouncementsAsync(runner, loadingAttempt))
                {
                    ReportError("Match loading was not acknowledged by every player. Retry from the lobby.");
                    return;
                }
                authority =
                    MatchAuthorityRuntime.EnsureExists(
                        _bootstrap);

                if (authority.IsCompletingMatch)
                {
                    ReportError(
                        "Previous match is still being finalized.");
                    return;
                }

                if (!await EnsureLobbyBindingAsync(runner, authority))
                {
                    ReportError("Cannot prepare backend authority for this lobby.");
                    return;
                }
                if (!loading.IsCurrent(runner, loadingAttempt)) return;

                float bindingDeadline = Time.realtimeSinceStartup + 8f;
                string rosterError = "Active roster is not committed.";
                while (!TryValidateCommittedRoster(runner, authority, out rosterError)
                       && loading.IsCurrent(runner, loadingAttempt)
                       && Time.realtimeSinceStartup < bindingDeadline)
                {
                    await Task.Delay(100);
                }

                if (!loading.IsCurrent(runner, loadingAttempt)) return;
                if (!TryValidateCommittedRoster(runner, authority, out rosterError))
                {
                    ReportError($"Backend player binding timed out: {rosterError}");
                    return;
                }

                var settings = ScenarioConfigAuthorityRuntime.EnsureExists().RuntimeSettings;
                var needsSnapshot =
                    authority.RequestedScenarioResolutionMode ==
                        ScenarioResolutionMode.Adaptive;
                if (needsSnapshot)
                {
                    if (settings == null)
                    {
                        ReportError("AED pre-match rejected: AEDRuntimeSettings is missing.");
                        return;
                    }
                    if (authority.MatchId == Guid.Empty || !authority.IsHostBinding
                        || !BuildStateFromRunner().CanStartMatch
                        || authority.BoundPlayerCount != BuildStateFromRunner().CurrentPlayers)
                    {
                        ReportError("AED pre-match rejected: host, ready room or bound roster changed.");
                        return;
                    }
                    _preMatchCancellation = new CancellationTokenSource();
                    var matchId = authority.MatchId;
                    var decisionId = ScenarioConfigAuthorityRuntime.CreateDecisionId(
                        matchId, ScenarioDecisionPoint.PreMatch, "PRE_MATCH", 0);
                    var api = new AEDSnapshotApiService();
                    var decision = await api.ResolvePreMatchAsync(matchId, decisionId,
                        authority.ExperimentCondition, _preMatchCancellation.Token);
                    if (decision == null || !Guid.TryParse(decision.snapshotId, out var snapshotId)
                        || snapshotId == Guid.Empty || decision.matchId != matchId.ToString("D"))
                    {
                        ReportError("AED pre-match resolve failed; match remains in lobby.");
                        return;
                    }
                    Debug.Log(
                        $"[AED_P8][PREMATCH_BACKEND] " +
                        $"match={matchId:D} " +
                        $"result={decision.resolutionResult} " +
                        $"fixedFallback={decision.usedFixedFallback} " +
                        $"fallbackReason={decision.fallbackReasonCode} " +
                        $"candidate={decision.candidateValidationStatus}");

                    if (settings.ExtendedPolicyGameplayEnabled &&
                        decision.usedFixedFallback)
                    {
                        ReportError(
                            "AED gameplay rejected: Backend issued fixed fallback. " +
                            decision.fallbackReasonCode);
                        return;
                    }

                    _snapshotProvider = new BackendAdaptiveInputSnapshotProvider();
                    if (!await _snapshotProvider.PrepareAsync(matchId, decisionId,
                            _preMatchCancellation.Token))
                    {
                        ReportError(
                            "AED snapshot rejected: " + _snapshotProvider.LastReason);
                        return;
                    }
                    var request = new ScenarioResolutionRequest(decisionId, matchId,
                        ScenarioResolutionMode.Adaptive, ScenarioDecisionPoint.PreMatch,
                        "PRE_MATCH", authority.ExperimentCondition);
                    if (!_snapshotProvider.TryGetSnapshot(request, out var snapshot,
                            out var currency, out _)
                        || snapshot.SnapshotId != snapshotId
                        || snapshot.SnapshotContentFingerprint != decision.snapshotContentFingerprint
                        || snapshot.RosterIdentity != decision.rosterIdentity
                        || !currency.IsCurrent || _preMatchCancellation.IsCancellationRequested
                        || authority.MatchId != matchId || !BuildStateFromRunner().CanStartMatch
                        || authority.BoundPlayerCount != BuildStateFromRunner().CurrentPlayers)
                    {
                        ReportError("AED pre-match snapshot changed before match start.");
                        return;
                    }
                    if (settings.ExtendedPolicyGameplayEnabled
                        && authority.Difficulty != MatchDifficulty.Normal)
                    {
                        ReportError("AED v2 gameplay requires Normal difficulty.");
                        return;
                    }
                    settings.TryBuildPolicyConfig(out var policy, out _);
                    settings.TryBuildEvidencePolicy(out var evidence, out _);
                    var gate = AEDInputGate.Evaluate(snapshot, request, policy,
                        evidence, currency);
                    if (gate.Status == AEDInputGateStatus.Invalid)
                    {
                        ReportError("AED pre-match input invalid: "
                            + string.Join(",", gate.Reasons));
                        return;
                    }
                    Debug.Log(
                        $"[AED_V2][BASELINE] match={matchId:D} " +
                        $"snapshot={snapshot.SnapshotId:D} " +
                        $"validity={snapshot.SnapshotValidity} " +
                        $"missing={snapshot.MissingProfileUserIds.Count} " +
                        $"plan={AEDv2Plan.Normal().Fingerprint()}");

                    if (decision.resolutionResult == "ADAPTIVE_CONFIG_ISSUED")
                    {
                        if (!ScenarioConfigAuthorityRuntime.EnsureExists()
                                .TryStageBackendPreMatch(matchId, decision))
                        {
                            ReportError(
                                "AED Backend candidate rejected by Unity validation.");
                            return;
                        }

                        Debug.Log(
                            $"[AED_P8][PREMATCH_STAGED] " +
                            $"match={matchId:D} " +
                            $"config={decision.config.scenarioConfigId} " +
                            $"version={decision.config.scenarioConfigVersion} " +
                            $"budget={decision.config.supportItemBudget}");
                    }
                    await authority.StageV02PreMatchAsync(_preMatchCancellation.Token);
                }

                Debug.LogWarning(
                    $"[NETDIAG][BeforeBackendStart] " +
                    $"SameRunner={ReferenceEquals(runner, _bootstrap?.Runner)}; " +
                    $"Running={runner != null && runner.IsRunning}; " +
                    $"Server={runner != null && runner.IsServer}; " +
                    $"SceneAuthority={runner != null && runner.IsSceneAuthority}; " +
                    $"SessionValid={runner != null && runner.SessionInfo.IsValid}; " +
                    $"BootstrapState={_bootstrap?.State}");
                // SessionInfo.IsValid is transiently false while Photon acknowledges
                // any pending IsOpen/IsVisible/custom-property update.
                var sessionReadyDeadline = Time.realtimeSinceStartup + 10f;
                while (ReferenceEquals(runner, _bootstrap?.Runner)
                       && runner != null
                       && runner.IsRunning
                       && runner.IsServer
                       && _bootstrap.State == NetworkSessionState.InLobby
                       && !runner.SessionInfo.IsValid
                       && Time.realtimeSinceStartup < sessionReadyDeadline)
                {
                    await Task.Delay(100);
                }

                if (!loading.IsCurrent(runner, loadingAttempt)) return;
                if (runner == null
                    || !ReferenceEquals(runner, _bootstrap?.Runner)
                    || !runner.IsRunning
                    || !runner.IsServer
                    || !runner.IsSceneAuthority
                    || _bootstrap.State != NetworkSessionState.InLobby
                    || !runner.SessionInfo.IsValid)
                {
                    ReportError("Fusion room information is not ready. Match was not started; retry after room synchronization.");
                    return;
                }
                var (accepted, error) =
                    await authority.StartMatchAsync();
                Debug.LogWarning(
                    $"[NETDIAG][BackendStartResult] Accepted={accepted}; " +
                    $"SameRunner={ReferenceEquals(runner, _bootstrap?.Runner)}; " +
                    $"Running={runner != null && runner.IsRunning}; " +
                    $"Server={runner != null && runner.IsServer}; " +
                    $"SceneAuthority={runner != null && runner.IsSceneAuthority}; " +
                    $"SessionValid={runner != null && runner.SessionInfo.IsValid}; " +
                    $"BootstrapState={_bootstrap?.State}");
                if (!accepted)
                {
                    ReportError($"Backend rejected match start: {error}");
                    return;
                }
                backendAccepted = true;
                if (!loading.IsCurrent(runner, loadingAttempt)) return;
                loading.BackendAuthorized(runner, loadingAttempt);

                if (runner == null || runner != _bootstrap?.Runner || !runner.IsRunning
                    || !runner.IsServer || !runner.IsSceneAuthority
                    || _bootstrap.State != NetworkSessionState.InLobby)
                {
                    ReportError("Match start aborted: authoritative runner is no longer available.");
                    if (_bootstrap != null) await _bootstrap.ShutdownRunnerAsync();
                    return;
                }

                var cloudReadyDeadline = Time.realtimeSinceStartup + 10f;
                while (ReferenceEquals(runner, _bootstrap?.Runner)
                       && runner != null
                       && runner.IsRunning
                       && runner.IsServer
                       && _bootstrap.State == NetworkSessionState.InLobby
                       && !runner.SessionInfo.IsValid
                       && Time.realtimeSinceStartup < cloudReadyDeadline)
                {
                    await Task.Delay(100);
                }
                if (!loading.IsCurrent(runner, loadingAttempt)) return;
                if (!_bootstrap.CloseRoomForMatchStart())
                {
                    ReportError("Fusion room was not ready after backend start; aborting backend match without shutting down the Lobby.");
                    if (ReferenceEquals(runner, _bootstrap?.Runner))
                    {
                        backendAbortAttempted = true;
                        bool backendAborted = await authority.EndAsync("FUSION_ROOM_NOT_READY");
                        if (!backendAborted)
                            Debug.LogWarning("[LobbyManager] Backend match abort was not confirmed; check backend state before retry.");
                    }
                    return;
                }

                RuntimeLog.Log(
                RuntimeLogCategory.Lobby,
                $"[LobbyManager] Backend confirmed match. Loading '{selectedScene}'.");
                loading.BeginSceneTransition(runner, loadingAttempt);
                sceneRequested = true;
                await runner.LoadScene(
                    selectedScene,
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                ReportError($"Match scene load failed: {exception.Message}");
                Debug.LogException(exception);
                // A failed scene has no gameplay snapshot for a graceful Host handoff.
                // The cinematic recovery action uses the guarded failed-loading abort path.
                if (_bootstrap != null && (loading == null || !loading.Snapshot.IsVisible))
                    await _bootstrap.ShutdownRunnerAsync();
            }
            finally
            {
                // A timeout/disconnect while awaiting backend approval must not start a hidden match later.
                if (backendAccepted && !sceneRequested && !backendAbortAttempted && authority != null
                    && runner != null && runner == _bootstrap?.Runner && runner.IsRunning && runner.IsServer)
                {
                    try { await authority.EndAsync("MATCH_LOADING_CANCELLED"); }
                    catch (Exception error) { Debug.LogWarning($"[LobbyManager] Loading cancellation cleanup: {error.Message}"); }
                }

                if (_bootstrap == null ||
                    _bootstrap.State != NetworkSessionState.InMatch)
                {
                    ClearPreparedSnapshot();

                    ScenarioConfigAuthorityRuntime.Instance
                        ?.ClearStagedBackendPreMatch(Guid.Empty);
                    MatchAuthorityRuntime.Instance?.ClearStagedV02PreMatch();
                }
                _preMatchCancellation?.Dispose();
                _preMatchCancellation = null;
                _bootstrap?.CancelMatchStartGate();
                _matchStartInProgress = false;
            }
        }

        private Task<bool> EnsureLobbyBindingAsync(NetworkRunner runner, MatchAuthorityRuntime authority)
        {
            if (_lobbyBindingTask is { IsCompleted: false }) return _lobbyBindingTask;
            if (!authority.RequiresFreshHostBinding) return Task.FromResult(true);
            _lobbyBindingTask = PrepareLobbyBindingAsync(runner, authority);
            return _lobbyBindingTask;
        }

        private async Task<bool> PrepareLobbyBindingAsync(NetworkRunner runner, MatchAuthorityRuntime authority)
        {
            try
            {
                if (runner == null || !runner.IsRunning || !runner.IsServer
                    || !runner.IsSceneAuthority || !runner.SessionInfo.IsValid) return false;
                if (!await authority.PrepareNextMatchAsync(
                        runner.SessionInfo.Name, runner.SessionInfo.MaxPlayers)) return false;

                if (this == null || runner != _bootstrap?.Runner || !runner.IsRunning
                    || !runner.IsServer || _bootstrap.State != NetworkSessionState.InLobby
                    || !_bootstrap.UpdateRoomProperties(authority.BuildBindingSessionProperties()))
                {
                    await authority.EndAsync("LOBBY_BINDING_NOT_PUBLISHED");
                    return false;
                }

                if (TryGetLocalPlayerState(out var localPlayerState, false))
                    authority.TrySubmitLocalIdentity(localPlayerState, force: true);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[LobbyManager] Backend lobby binding failed: {exception.Message}");
                return false;
            }
        }

        private static bool TryValidateCommittedRoster(
            NetworkRunner runner,
            MatchAuthorityRuntime authority,
            out string error)
        {
            error = string.Empty;

            if (runner == null
                || !runner.IsRunning
                || !runner.IsServer
                || authority == null
                || !authority.IsHostBinding)
            {
                error = "authoritative session is unavailable";
                return false;
            }

            var playerObjects =
                new HashSet<NetworkId>();

            var backendUsers =
                new HashSet<Guid>();

            var count = 0;

            foreach (var player in
                     runner.ActivePlayers)
            {
                count++;

                if (!runner.TryGetPlayerObject(
                        player,
                        out var playerObject)
                    || playerObject == null
                    || !playerObject.IsValid
                    || playerObject.InputAuthority != player
                    || !playerObjects.Add(playerObject.Id)
                    || !playerObject.TryGetComponent<LobbyPlayerState>(
                        out var lobbyState)
                    || lobbyState.Disconnected
                    || !lobbyState.HasVerifiedBackendIdentity
                    || !Guid.TryParse(
                        lobbyState.BackendUserId.ToString(),
                        out var backendUserId)
                    || !backendUsers.Add(backendUserId))
                {
                    error =
                        $"player {player} has no unique committed PlayerObject/backend identity";
                    return false;
                }

                var actorNumber =
                    runner.GetPlayerActorId(player)
                    ?? (runner.LocalPlayer == player
                        ? player.PlayerId
                        : 0);

                if (actorNumber < 1
                    || lobbyState.FusionActorNumber != actorNumber
                    || !authority.IsPlayerBound(
                        actorNumber,
                        backendUserId.ToString("D")))
                {
                    error =
                        $"player {player} actor/backend binding is not current";
                    return false;
                }

                if (player != runner.LocalPlayer
                    && !lobbyState.IsReady)
                {
                    error =
                        $"non-host player {player} is not ready";
                    return false;
                }
            }

            if (count is < 1 or > 4)
            {
                error =
                    $"active roster size {count} is outside the supported 1-4 range";
                return false;
            }

            if (authority.BoundPlayerCount != count)
            {
                error =
                    $"backend roster count {authority.BoundPlayerCount} does not match active Fusion roster {count}";
                return false;
            }

            return true;
        }

        /// <summary>Rebuilds the display snapshot exclusively from Fusion ActivePlayers.</summary>
        public void RefreshFromRunner()
        {
            CurrentState = BuildStateFromRunner();
            OnRoomUpdated?.Invoke(CurrentState);
        }

        private RoomInfoViewModel BuildStateFromRunner()
        {
            var runner = _bootstrap?.Runner;
            if (runner == null || !runner.IsRunning)
            {
                return new RoomInfoViewModel();
            }

            var members = new List<LobbyMemberViewModel>();
            var localReady = false;
            foreach (var player in runner.ActivePlayers)
            {
                var isReady = false;
                var teamId = 0;
                var characterId = 0;
                var characterOutfitId = 0;
                var petId = 0;
                var petSkinId = 0;
                var toolId = 0;
                var operatorName = string.Empty;
                if (runner.TryGetPlayerObject(player, out var playerObject)
                    && playerObject.TryGetComponent<LobbyPlayerState>(out var playerState))
                {
                    isReady = playerState.IsReady;
                    teamId = playerState.TeamId;
                    characterId = playerState.CharacterId;
                    characterOutfitId = playerState.CharacterOutfitId;
                    petId = playerState.PetId;
                    petSkinId = playerState.PetSkinId;
                    toolId = playerState.ToolId;
                    operatorName = playerState.OperatorName.ToString();
                }

                var isLocal = player == runner.LocalPlayer;
                if (isLocal) localReady = isReady;
                members.Add(new LobbyMemberViewModel
                {
                    PlayerRef = player,
                    ActorId = runner.GetPlayerActorId(player) ?? player.PlayerId,
                    IsLocal = isLocal,
                    IsReady = isReady,
                    TeamId = teamId,
                    CharacterId = characterId,
                    CharacterOutfitId = characterOutfitId,
                    PetId = petId,
                    PetSkinId = petSkinId,
                    ToolId = toolId,
                    OperatorName = operatorName,
                });
            }
            members.Sort((left, right) => left.ActorId.CompareTo(right.ActorId));

            var session = runner.SessionInfo;
            var supportedRoster =
                members.Count is >= 1 and <= 4;

            var requiredPlayersReady =
                members.TrueForAll(
                    member => member.PlayerRef == runner.LocalPlayer
                              || member.IsReady);

            return new RoomInfoViewModel
            {
                RoomName = session.IsValid ? session.Name : _bootstrap.CurrentSessionName,
                MaxPlayers = session.IsValid ? session.MaxPlayers : 0,
                CurrentPlayers = members.Count,
                IsHost = runner.IsServer,
                IsReady = localReady,
                CanStartMatch = runner.IsServer
                                && !_bootstrap.IsMatchStartPending
                                && supportedRoster
                                && requiredPlayersReady,
                Members = members,
            };
        }

        private void HandlePlayerChanged(PlayerRef player)
        {
            _preMatchCancellation?.Cancel();
            RefreshFromRunner();
        }

        private void ClearPreparedSnapshot()
        {
            _preMatchCancellation?.Cancel();
            _snapshotProvider?.Dispose();
            _snapshotProvider = null;
        }

        private void HandleSelectionResult(LobbySelectionResult result)
        {
            OnSelectionRequestCompleted?.Invoke(result);
            RefreshFromRunner();
        }

        private void HandleSessionStateChanged(NetworkSessionState state, string message)
        {
            if (state == NetworkSessionState.InLobby && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == NetworkBootstrap.LobbySceneName)
                _matchStartInProgress = false;
            if (state == NetworkSessionState.Disconnected || state == NetworkSessionState.Failed)
            {
                // A disconnected session always rebuilds to an empty, not-ready state.
            }
            RefreshFromRunner();
        }

        private bool ValidateRoomName(string roomName)
        {
            if (!string.IsNullOrWhiteSpace(roomName)) return true;
            ReportError("Room name is required.");
            return false;
        }

        private void ReportError(string message)
        {
            if (_matchStartInProgress) MatchLoadingCoordinator.Instance?.AbortHost(_bootstrap?.Runner, message);
            Debug.LogWarning($"[LobbyManager] {message}");
            OnLobbyError?.Invoke(message);
        }
    }
}
