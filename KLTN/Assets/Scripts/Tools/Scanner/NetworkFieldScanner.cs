using System;
using System.Collections.Generic;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.Networking;
using EchoProtocol.Networking.Authority;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoProtocol.Tools.Scanner
{
    [DisallowMultipleComponent]
    public class NetworkFieldScanner : NetworkBehaviour
    {
        public static event Action<NetworkFieldScanner, FieldScannerMode> AnyModeChanged;
        public static event Action<NetworkFieldScanner> AnyScanPulseTriggered;

        public event Action<CoreScanResult> LocalCoreResultReceived;
        public event Action<MotionScanResult> LocalMotionResultReceived;
        public event Action<FieldScannerMode> LocalModeChanged;
        public event Action LocalScanCleared;

        [SerializeField] private FieldScannerTuning _tuning = new FieldScannerTuning();

        [Networked, OnChangedRender(nameof(HandleModeChanged))]
        private FieldScannerMode NetworkMode { get; set; }

        [Networked]
        public TickTimer ScanCooldownTimer { get; private set; }

        [Networked, OnChangedRender(nameof(HandlePulseTriggered))]
        private uint NetworkScanPulseOrdinal { get; set; }

        private FieldScannerMode _localMode = FieldScannerMode.Core;
        private uint _localScanPulseOrdinal;

        public FieldScannerMode CurrentMode
        {
            get
            {
                if (Object != null && Object.IsValid)
                {
                    return NetworkMode;
                }
                return _localMode;
            }
            private set
            {
                _localMode = value;
                if (Object != null && Object.IsValid && Object.HasStateAuthority)
                {
                    NetworkMode = value;
                }
            }
        }

        public uint ScanPulseOrdinal
        {
            get
            {
                if (Object != null && Object.IsValid)
                {
                    return NetworkScanPulseOrdinal;
                }
                return _localScanPulseOrdinal;
            }
            private set
            {
                _localScanPulseOrdinal = value;
                if (Object != null && Object.IsValid && Object.HasStateAuthority)
                {
                    NetworkScanPulseOrdinal = value;
                }
            }
        }

        private InputAction _scanAction;
        private InputAction _switchModeAction;
        private uint _nextSequence;
        private uint _lastProcessedSequence;
        private float _localResultTimer;
        private float _localCooldownTimer;
        private float _localActiveScanTimer;
        private float _realtimeScanTimer;
        private bool _hasLocalActiveResult;
        private CoreScanResult _lastLocalCoreResult = CoreScanResult.Empty;
        private MotionScanResult _lastLocalMotionResult = MotionScanResult.Empty;

        public FieldScannerTuning Tuning => _tuning;
        public bool HasActiveResult => _hasLocalActiveResult && _localResultTimer > 0f;
        public float ResultRemainingTime => Mathf.Max(0f, _localResultTimer);
        public CoreScanResult CurrentCoreResult => HasActiveResult ? _lastLocalCoreResult : CoreScanResult.Empty;
        public MotionScanResult CurrentMotionResult => HasActiveResult ? _lastLocalMotionResult : MotionScanResult.Empty;

        public float ActiveRemainingTime
        {
            get
            {
                if (Runner != null && Runner.IsRunning && Object != null && Object.IsValid)
                {
                    float rem = ScanCooldownTimer.RemainingTime(Runner) ?? 0f;
                    return Mathf.Max(0f, rem - _tuning.ScanCooldown);
                }
                return Mathf.Max(0f, _localActiveScanTimer);
            }
        }

        public bool IsScanActive => ActiveRemainingTime > 0.02f;

        public float LocalCooldownRemaining
        {
            get
            {
                if (Runner != null && Runner.IsRunning && Object != null && Object.IsValid)
                {
                    float rem = ScanCooldownTimer.RemainingTime(Runner) ?? 0f;
                    return Mathf.Min(rem, _tuning.ScanCooldown);
                }
                return Mathf.Max(0f, _localCooldownTimer);
            }
        }

        public bool IsCooldownReady
        {
            get
            {
                if (Runner != null && Runner.IsRunning && Object != null && Object.IsValid)
                {
                    return ScanCooldownTimer.ExpiredOrNotRunning(Runner);
                }
                return _localActiveScanTimer <= 0f && _localCooldownTimer <= 0f;
            }
        }

        private void EnsureTuningDefaults()
        {
            if (_tuning == null)
            {
                _tuning = new FieldScannerTuning();
            }
            if (_tuning.ActiveDuration < 1.0f)
            {
                _tuning.ActiveDuration = 10.0f;
            }
            if (_tuning.ScanCooldown < 10.0f)
            {
                _tuning.ScanCooldown = 60.0f;
            }
            if (_tuning.CoreRange < 30.0f)
            {
                _tuning.CoreRange = 50.0f;
            }
            if (_tuning.MotionRange < 20.0f)
            {
                _tuning.MotionRange = 35.0f;
            }
        }

        private void Awake()
        {
            EnsureTuningDefaults();
            _switchModeAction = new InputAction("ScannerSwitchMode", InputActionType.Button);
            _switchModeAction.AddBinding("<Mouse>/rightButton");
            _switchModeAction.AddBinding("<Keyboard>/b");
            _switchModeAction.AddBinding("<Keyboard>/t");

            _scanAction = new InputAction("ScannerScan", InputActionType.Button);
            _scanAction.AddBinding("<Mouse>/leftButton");
            // NOTE: Key F is Flashlight toggle in ECHO PROTOCOL. Scanner only uses LMB (Scan) and RMB (Mode).
        }

        public bool IsLocalControllingPlayer()
        {
            if (Object == null || !Object.IsValid) return true;
            if (Object.HasInputAuthority) return true;
            if (Object.HasStateAuthority && !Object.InputAuthority.IsValid) return true;
            return false;
        }

        private void OnEnable()
        {
            EnsureTuningDefaults();
            if (IsLocalControllingPlayer())
            {
                _switchModeAction?.Enable();
                _scanAction?.Enable();
            }
        }

        private void OnDisable()
        {
            _switchModeAction?.Disable();
            _scanAction?.Disable();

            if (IsLocalControllingPlayer())
            {
                EchoProtocol.UI.HUD.HUDFieldScanner.Instance?.SetVisible(false);
            }
        }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                CurrentMode = FieldScannerMode.Core;
                ScanPulseOrdinal = 0;
            }

            if (IsLocalControllingPlayer())
            {
                _switchModeAction?.Enable();
                _scanAction?.Enable();
            }
            else
            {
                _switchModeAction?.Disable();
                _scanAction?.Disable();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _switchModeAction?.Disable();
            _scanAction?.Disable();
            ClearLocalResult();

            if (IsLocalControllingPlayer())
            {
                EchoProtocol.UI.HUD.HUDFieldScanner.Instance?.SetVisible(false);
            }
        }

        private void OnDestroy()
        {
            _switchModeAction?.Dispose();
            _scanAction?.Dispose();
        }

        private void Update()
        {
            if (_localActiveScanTimer > 0f)
            {
                _localActiveScanTimer -= Time.deltaTime;
                if (_localActiveScanTimer <= 0f)
                {
                    _localActiveScanTimer = 0f;
                    ClearLocalResult();
                }
            }
            else if (_localCooldownTimer > 0f)
            {
                _localCooldownTimer -= Time.deltaTime;
                if (_localCooldownTimer <= 0f)
                {
                    _localCooldownTimer = 0f;
                }
            }

            if (_hasLocalActiveResult)
            {
                _localResultTimer -= Time.deltaTime;
                if (_localResultTimer <= 0f)
                {
                    ClearLocalResult();
                }
            }

            bool isLocal = IsLocalControllingPlayer();
            if (isLocal)
            {
                bool equipped = IsScannerEquipped() && !IsCarryingCore();
                var hud = EchoProtocol.UI.HUD.HUDFieldScanner.EnsureInstance();
                if (hud != null)
                {
                    hud.BindScanner(this);
                    hud.SetVisible(equipped);
                }
            }

            if (!isLocal)
            {
                return;
            }

            if (!IsScannerEquipped())
            {
                return;
            }

            if (IsCarryingCore())
            {
                if (_hasLocalActiveResult) ClearLocalResult();
                return;
            }

            // Realtime continuous scanning ONLY while scanner is actively activated (10s window, 20Hz updates)
            if (IsScanActive)
            {
                _realtimeScanTimer -= Time.deltaTime;
                if (_realtimeScanTimer <= 0f)
                {
                    _realtimeScanTimer = 0.05f;
                    PerformRealtimeScan();
                }
            }
            else
            {
                if (_hasLocalActiveResult)
                {
                    ClearLocalResult();
                }
            }

            bool lmbPressed = false;
            bool rmbPressed = false;

            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                lmbPressed |= UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
                rmbPressed |= UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame;
            }
            if (_scanAction != null && _scanAction.WasPerformedThisFrame())
            {
                lmbPressed = true;
            }
            if (_switchModeAction != null && _switchModeAction.WasPerformedThisFrame())
            {
                rmbPressed = true;
            }

            if (rmbPressed)
            {
                RequestToggleMode();
            }

            if (lmbPressed)
            {
                RequestScan();
            }
        }

        private void PerformRealtimeScan()
        {
            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // Align scanning orientation with player camera view for intuitive radar tracking
                forward = mainCam.transform.forward;
            }

            if (CurrentMode == FieldScannerMode.Core)
            {
                var coreCandidates = CollectCoreCandidates();
                var result = FieldScannerCoreDetector.Evaluate(
                    origin,
                    forward,
                    coreCandidates,
                    _tuning);

                _lastLocalCoreResult = result;
                _lastLocalMotionResult = MotionScanResult.Empty;
                _hasLocalActiveResult = true;
                _localResultTimer = 1.0f;
            }
            else
            {
                var motionTargets = CollectMotionTargets();
                var result = FieldScannerMotionDetector.Evaluate(
                    origin,
                    forward,
                    motionTargets,
                    _tuning);

                _lastLocalMotionResult = result;
                _lastLocalCoreResult = CoreScanResult.Empty;
                _hasLocalActiveResult = true;
                _localResultTimer = 1.0f;
            }
        }

        public bool IsScannerEquipped()
        {
            var lobbyState = GetComponent<LobbyPlayerState>();
            if (lobbyState != null && lobbyState.Object != null && lobbyState.Object.IsValid && lobbyState.ToolId == 1)
            {
                return true;
            }

            var inventory = GetComponent<PlayerInventory>();
            if (inventory != null && inventory.TeamToolSlot != null)
            {
                string id = (inventory.TeamToolSlot.ItemId ?? string.Empty).ToLowerInvariant();
                string name = (inventory.TeamToolSlot.DisplayName ?? string.Empty).ToLowerInvariant();
                return id.Contains("scan") || name.Contains("scan");
            }

            return false;
        }

        public bool IsCarryingCore()
        {
            var lobbyState = GetComponent<LobbyPlayerState>();
            if (lobbyState != null && lobbyState.Object != null && lobbyState.Object.IsValid && lobbyState.CarriedCoreId.IsValid)
            {
                return true;
            }

            var carrier = GetComponent<PlayerEnergyCoreCarrier>();
            if (carrier != null && carrier.IsCarrying)
            {
                return true;
            }

            return false;
        }

        public bool RequestScan()
        {
            if (!IsScannerEquipped())
            {
                return false;
            }

            if (IsCarryingCore())
            {
                return false;
            }

            if (!IsCooldownReady || IsScanActive)
            {
                return false;
            }

            var lifeState = GetComponent<NetworkPlayerLifeState>();
            if (lifeState != null && lifeState.Object != null && lifeState.Object.IsValid && !lifeState.CanInitiateAction)
            {
                return false;
            }

            if (Object != null && Object.IsValid && Runner != null && Runner.IsRunning)
            {
                if (Object.HasInputAuthority)
                {
                    RpcRequestScan(CurrentMode, NextSequence());
                }
                else if (!Object.HasStateAuthority)
                {
                    return false;
                }
            }

            return ExecuteScanLocally(CurrentMode);
        }

        public bool RequestToggleMode()
        {
            FieldScannerMode nextMode = CurrentMode == FieldScannerMode.Core
                ? FieldScannerMode.Motion
                : FieldScannerMode.Core;
            bool ok = RequestSetMode(nextMode);
            if (ok && IsScanActive)
            {
                _realtimeScanTimer = 0f;
                PerformRealtimeScan();
            }
            return ok;
        }

        public bool RequestSetMode(FieldScannerMode newMode)
        {
            if (!IsScannerEquipped() || IsCarryingCore())
            {
                return false;
            }

            if (Object != null && Object.IsValid && Runner != null && Runner.IsRunning)
            {
                if (Object.HasInputAuthority)
                {
                    RpcRequestSetMode(newMode);
                }
                else if (!Object.HasStateAuthority)
                {
                    return false;
                }
            }

            CurrentMode = newMode;
            ClearLocalResult();
            LocalModeChanged?.Invoke(CurrentMode);
            AnyModeChanged?.Invoke(this, CurrentMode);
            return true;
        }

        private bool ExecuteScanLocally(FieldScannerMode requestedMode)
        {
            _localActiveScanTimer = _tuning.ActiveDuration;
            _localCooldownTimer = _tuning.ScanCooldown;
            ScanPulseOrdinal++;
            AnyScanPulseTriggered?.Invoke(this);

            PerformRealtimeScan();

            if (requestedMode == FieldScannerMode.Core)
            {
                LocalCoreResultReceived?.Invoke(_lastLocalCoreResult);
            }
            else
            {
                LocalMotionResultReceived?.Invoke(_lastLocalMotionResult);
            }

            return true;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestSetMode(FieldScannerMode newMode, RpcInfo info = default)
        {
            if (!TryResolveRequester(info.Source, out _))
            {
                return;
            }

            if (IsCarryingCore())
            {
                return;
            }

            CurrentMode = newMode;
            AnyModeChanged?.Invoke(this, CurrentMode);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RpcRequestScan(FieldScannerMode requestedMode, uint sequence, RpcInfo info = default)
        {
            if (!TryResolveRequester(info.Source, out var requester))
            {
                return;
            }

            if (!IsScannerEquipped() || IsCarryingCore() || !IsCooldownReady || IsScanActive)
            {
                return;
            }

            var lifeState = GetComponent<NetworkPlayerLifeState>();
            if (lifeState != null && lifeState.Object != null && lifeState.Object.IsValid && !lifeState.CanInitiateAction)
            {
                return;
            }

            // Accept scan: Total duration = ActiveDuration (10s) + ScanCooldown (60s)
            float totalDuration = _tuning.ActiveDuration + _tuning.ScanCooldown;
            ScanCooldownTimer = TickTimer.CreateFromSeconds(Runner, totalDuration);
            ScanPulseOrdinal++;
            AnyScanPulseTriggered?.Invoke(this);

            // Authoritative electronic noise
            HostRuntimeNoiseService.EnsureExists(MatchAuthorityRuntime.Instance)
                ?.TryAccept(
                    requester,
                    RuntimeNoiseType.FIELD_SCANNER,
                    RuntimeNoiseSourceOccurrenceKey.ForTeamTool(Object.Id.ToString(), "FIELD_SCANNER", sequence),
                    transform.position,
                    out _);

            if (sequence > _lastProcessedSequence) _lastProcessedSequence = sequence;

            // Compute authoritative result
            if (requestedMode == FieldScannerMode.Core)
            {
                var coreCandidates = CollectCoreCandidates();
                var result = FieldScannerCoreDetector.Evaluate(
                    transform.position,
                    transform.forward,
                    coreCandidates,
                    _tuning);

                RpcReceiveCoreResult(
                    requester,
                    result.HasTarget,
                    (int)result.SignalBars,
                    (int)result.Direction,
                    result.RawDistance,
                    result.SignalScore,
                    result.TargetId);
            }
            else
            {
                var motionTargets = CollectMotionTargets();
                var result = FieldScannerMotionDetector.Evaluate(
                    transform.position,
                    transform.forward,
                    motionTargets,
                    _tuning);

                var b0 = result.GetBlip(0);
                var b1 = result.GetBlip(1);
                var b2 = result.GetBlip(2);

                RpcReceiveMotionResult(
                    requester,
                    result.HasMotion,
                    result.BlipCount,
                    b0.IsValid, (int)b0.Direction, (int)b0.Intensity, b0.Distance, b0.TargetId,
                    b1.IsValid, (int)b1.Direction, (int)b1.Intensity, b1.Distance, b1.TargetId,
                    b2.IsValid, (int)b2.Direction, (int)b2.Intensity, b2.Distance, b2.TargetId);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RpcReceiveCoreResult(
            [RpcTarget] PlayerRef targetPlayer,
            bool hasTarget,
            int signalBars,
            int direction,
            float distance,
            float score,
            int targetId)
        {
            _lastLocalCoreResult = new CoreScanResult
            {
                HasTarget = hasTarget,
                SignalBars = (ScannerSignalStrength)signalBars,
                Direction = (RelativeDirectionSector)direction,
                RawDistance = distance,
                SignalScore = score,
                TargetId = targetId
            };
            _lastLocalMotionResult = MotionScanResult.Empty;
            _hasLocalActiveResult = true;
            _localResultTimer = _tuning.ResultLifetime;

            LocalCoreResultReceived?.Invoke(_lastLocalCoreResult);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RpcReceiveMotionResult(
            [RpcTarget] PlayerRef targetPlayer,
            bool hasMotion,
            int blipCount,
            bool b0Valid, int b0Dir, int b0Int, float b0Dist, int b0Id,
            bool b1Valid, int b1Dir, int b1Int, float b1Dist, int b1Id,
            bool b2Valid, int b2Dir, int b2Int, float b2Dist, int b2Id)
        {
            MotionScanResult result = new MotionScanResult
            {
                HasMotion = hasMotion,
                BlipCount = blipCount,
                Blip0 = new MotionBlip { IsValid = b0Valid, Direction = (RelativeDirectionSector)b0Dir, Intensity = (MotionBlipIntensity)b0Int, Distance = b0Dist, TargetId = b0Id },
                Blip1 = new MotionBlip { IsValid = b1Valid, Direction = (RelativeDirectionSector)b1Dir, Intensity = (MotionBlipIntensity)b1Int, Distance = b1Dist, TargetId = b1Id },
                Blip2 = new MotionBlip { IsValid = b2Valid, Direction = (RelativeDirectionSector)b2Dir, Intensity = (MotionBlipIntensity)b2Int, Distance = b2Dist, TargetId = b2Id }
            };

            _lastLocalMotionResult = result;
            _lastLocalCoreResult = CoreScanResult.Empty;
            _hasLocalActiveResult = true;
            _localResultTimer = _tuning.ResultLifetime;

            LocalMotionResultReceived?.Invoke(_lastLocalMotionResult);
        }

        private void ClearLocalResult()
        {
            _hasLocalActiveResult = false;
            _localResultTimer = 0f;
            _lastLocalCoreResult = CoreScanResult.Empty;
            _lastLocalMotionResult = MotionScanResult.Empty;
            LocalScanCleared?.Invoke();
        }

        private void HandleModeChanged()
        {
            if (Object != null && Object.IsValid)
            {
                _localMode = NetworkMode;
            }
            ClearLocalResult();
            LocalModeChanged?.Invoke(CurrentMode);
            AnyModeChanged?.Invoke(this, CurrentMode);
        }

        private void HandlePulseTriggered()
        {
            if (Object != null && Object.IsValid)
            {
                _localScanPulseOrdinal = NetworkScanPulseOrdinal;
            }
            AnyScanPulseTriggered?.Invoke(this);
        }

        private uint NextSequence()
        {
            _nextSequence++;
            if (_nextSequence == 0) _nextSequence = 1;
            return _nextSequence;
        }

        private bool TryResolveRequester(PlayerRef rpcSource, out PlayerRef effectiveRequester)
        {
            if (Object == null)
            {
                effectiveRequester = PlayerRef.None;
                return false;
            }

            return RpcRequesterResolver.TryResolveEffectiveRequester(
                rpcSource,
                Object.InputAuthority,
                Object.HasStateAuthority,
                Object.HasInputAuthority,
                out effectiveRequester);
        }

        private List<ICoreScanCandidate> CollectCoreCandidates()
        {
            List<ICoreScanCandidate> candidates = new List<ICoreScanCandidate>();

            // 1. NetworkPickupItems
            NetworkPickupItem[] networkItems = FindObjectsByType<NetworkPickupItem>(FindObjectsInactive.Exclude);
            for (int i = 0; i < networkItems.Length; i++)
            {
                var netItem = networkItems[i];
                if (netItem != null)
                {
                    candidates.Add(new NetworkPickupItemCandidateAdapter(netItem));
                }
            }

            // 2. Local EnergyCorePickup fallbacks if no network items found
            if (candidates.Count == 0)
            {
                EnergyCorePickup[] pickups = FindObjectsByType<EnergyCorePickup>(FindObjectsInactive.Exclude);
                for (int i = 0; i < pickups.Length; i++)
                {
                    if (pickups[i] != null)
                    {
                        candidates.Add(new EnergyCorePickupCandidateAdapter(pickups[i]));
                    }
                }
            }

            return candidates;
        }

        private List<IMotionScannable> CollectMotionTargets()
        {
            List<IMotionScannable> targets = new List<IMotionScannable>();

            // 1. Registered IMotionScannables
            var active = MotionScannableTarget.ActiveTargets;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] != null && active[i].IsActiveTarget)
                {
                    targets.Add(active[i]);
                }
            }

            // 2. Fallback for Stalkers without MotionScannableTarget component attached
            if (targets.Count == 0)
            {
                var stalkers = FindObjectsByType<EchoProtocol.AI.Stalker.StalkerController>(FindObjectsInactive.Exclude);
                for (int i = 0; i < stalkers.Length; i++)
                {
                    if (stalkers[i] != null && stalkers[i].gameObject.activeInHierarchy)
                    {
                        targets.Add(new StalkerMotionScannableAdapter(stalkers[i]));
                    }
                }
            }

            return targets;
        }

        private sealed class NetworkPickupItemCandidateAdapter : ICoreScanCandidate
        {
            private readonly NetworkPickupItem _item;

            public NetworkPickupItemCandidateAdapter(NetworkPickupItem item)
            {
                _item = item;
            }

            public int TargetId => _item != null ? _item.gameObject.GetHashCode() : 0;
            public Vector3 WorldPosition => _item != null ? _item.transform.position : Vector3.zero;

            public bool IsAvailableInWorld
            {
                get
                {
                    if (_item == null || !_item.gameObject.activeInHierarchy) return false;
                    // Only Available or Dropped states, not Carried, not Placed
                    return (_item.State == NetworkItemState.Available || _item.State == NetworkItemState.Dropped)
                        && !_item.PlacedSectorId.IsValid
                        && _item.Holder == PlayerRef.None;
                }
            }
        }

        private sealed class EnergyCorePickupCandidateAdapter : ICoreScanCandidate
        {
            private readonly EnergyCorePickup _pickup;

            public EnergyCorePickupCandidateAdapter(EnergyCorePickup pickup)
            {
                _pickup = pickup;
            }

            public int TargetId => _pickup != null ? _pickup.gameObject.GetHashCode() : 0;
            public Vector3 WorldPosition => _pickup != null ? _pickup.transform.position : Vector3.zero;
            public bool IsAvailableInWorld => _pickup != null && _pickup.gameObject.activeInHierarchy;
        }

        private sealed class StalkerMotionScannableAdapter : IMotionScannable
        {
            private readonly EchoProtocol.AI.Stalker.StalkerController _stalker;
            private readonly UnityEngine.AI.NavMeshAgent _agent;

            public StalkerMotionScannableAdapter(EchoProtocol.AI.Stalker.StalkerController stalker)
            {
                _stalker = stalker;
                _agent = stalker != null ? stalker.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
            }

            public int TargetId => _stalker != null ? _stalker.gameObject.GetHashCode() : 0;
            public Vector3 WorldPosition => _stalker != null ? _stalker.transform.position : Vector3.zero;
            public float CurrentSpeed => _agent != null && _agent.enabled && _agent.isOnNavMesh ? _agent.velocity.magnitude : 0f;
            public bool IsMoving => CurrentSpeed >= 0.2f;
            public bool IsActiveTarget => _stalker != null && _stalker.gameObject.activeInHierarchy;
        }
    }
}
