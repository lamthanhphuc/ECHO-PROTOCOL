using System;
using System.Collections.Generic;
using EchoProtocol.Networking;
using EchoProtocol.RelayA;
using EchoProtocol.RelayB;
using Fusion;
using UnityEngine;

namespace EchoProtocol.MatchFlow
{
    [DisallowMultipleComponent]
    public class Zone2MissionDirector : MonoBehaviour
    {
        private static Zone2MissionDirector _instance;
        public static Zone2MissionDirector Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<Zone2MissionDirector>(FindObjectsInactive.Include);
                    if (_instance == null)
                    {
                        var go = GameObject.Find("zone2_GamePlay");
                        if (go != null)
                        {
                            _instance = go.AddComponent<Zone2MissionDirector>();
                        }
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Security Terminal")]
        [SerializeField] private SecurityTerminalDownload securityTerminal;

        [Header("Relays (Exactly 4)")]
        [SerializeField] private RelayAController relayA1;
        [SerializeField] private RelayAController relayA2;
        [SerializeField] private RelayBController relayB1;
        [SerializeField] private RelayBController relayB2;

        [Header("Zone Exit Distribution Panels (Shared System)")]
        [SerializeField] private PowerControlUIController distributionPanel1;
        [SerializeField] private PowerControlUIController distributionPanel2;

        [Header("Zone Exit Door Blockers")]
        [SerializeField] private GameObject doorBlocker1;
        [SerializeField] private GameObject doorBlocker2;

        [Header("Security Hold Pressure")]
        [SerializeField, Min(1f)] private float relayRepairRetryWindowSeconds = 300f;

        private Zone2MissionStage _offlineStage = Zone2MissionStage.FindSecurityTerminal;
        private int _offlineRelayMask;
        private bool _offlineTerminalDiscovered;
        private bool _offlineSecurityHoldComplete;
        private bool _offlineZoneDoorsUnlocked;
        private float _offlineRelayRepairDeadline = -1f;
        private string _offlineAuthCode;
        private bool _hasAuthoritativePresentation;
        private Zone2MissionStage _presentedStage;
        private int _presentedRelayMask;
        private bool _presentedTerminalDiscovered;
        private bool _presentedSecurityHoldComplete;
        private bool _presentedDoorsUnlocked;
        private string _presentedAuthorizationCode = string.Empty;

        public event Action<Zone2MissionStage> StageChanged;
        public event Action<int> RelayProgressChanged;
        public event Action<string> AuthorizationCodeRevealed;
        public event Action ZoneDoorsUnlockedEvent;

        public SecurityTerminalDownload SecurityTerminal => securityTerminal;
        public RelayAController RelayA1 => relayA1;
        public RelayAController RelayA2 => relayA2;
        public RelayBController RelayB1 => relayB1;
        public RelayBController RelayB2 => relayB2;
        public PowerControlUIController DistributionPanel1 => distributionPanel1;
        public PowerControlUIController DistributionPanel2 => distributionPanel2;
        public GameObject DoorBlocker1 => doorBlocker1;
        public GameObject DoorBlocker2 => doorBlocker2;

        public Zone2MissionStage CurrentStage
        {
            get
            {
                var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
                if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
                {
                    return matchState.Zone2Stage;
                }
                return _offlineStage;
            }
        }

        public int RelayCompletionMask
        {
            get
            {
                var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
                if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
                {
                    return matchState.RelayCompletionMask;
                }
                return _offlineRelayMask;
            }
        }

        public int CompletedRelayCount
        {
            get
            {
                int mask = RelayCompletionMask;
                int count = 0;
                if ((mask & (1 << (int)RelaySlot.RelayA_1)) != 0) count++;
                if ((mask & (1 << (int)RelaySlot.RelayA_2)) != 0) count++;
                if ((mask & (1 << (int)RelaySlot.RelayB_1)) != 0) count++;
                if ((mask & (1 << (int)RelaySlot.RelayB_2)) != 0) count++;
                return count;
            }
        }

        public int PowerRelaysOnline
        {
            get
            {
                int mask = RelayCompletionMask;
                int count = 0;
                if ((mask & (1 << (int)RelaySlot.RelayA_1)) != 0) count++;
                if ((mask & (1 << (int)RelaySlot.RelayA_2)) != 0) count++;
                return count;
            }
        }

        public int DataRelaysOnline
        {
            get
            {
                int mask = RelayCompletionMask;
                int count = 0;
                if ((mask & (1 << (int)RelaySlot.RelayB_1)) != 0) count++;
                if ((mask & (1 << (int)RelaySlot.RelayB_2)) != 0) count++;
                return count;
            }
        }

        public bool AreAllRelaysOnline => CompletedRelayCount >= 4;
        public float RelayRepairRetryWindowSeconds => Mathf.Max(1f, relayRepairRetryWindowSeconds);
        public float RelayRepairRemainingSeconds
        {
            get
            {
                var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
                if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
                    return matchState.RelayRepairWindowRemainingSeconds;
                return _offlineRelayRepairDeadline > 0f && !_offlineSecurityHoldComplete
                    ? Mathf.Max(0f, _offlineRelayRepairDeadline - Time.time) : 0f;
            }
        }
        public bool IsRelayRepairWindowRunning => RelayRepairRemainingSeconds > 0f && !IsSecurityHoldComplete;

        public bool IsSecurityTerminalDiscovered
        {
            get
            {
                var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
                if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
                {
                    return matchState.SecurityTerminalDiscovered;
                }
                return _offlineTerminalDiscovered;
            }
        }

        public bool IsSecurityHoldComplete
        {
            get
            {
                var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
                if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
                {
                    return matchState.SecurityHoldCompleted;
                }
                return _offlineSecurityHoldComplete || (securityTerminal != null && securityTerminal.IsComplete);
            }
        }

        public bool AreZoneDoorsUnlocked
        {
            get
            {
                var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
                if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
                {
                    return matchState.ZoneDoorsUnlocked;
                }
                return _offlineZoneDoorsUnlocked;
            }
        }

        public string AuthorizationCode
        {
            get
            {
                var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
                if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
                {
                    // Multiplayer uses only the server-owned replicated code.
                    // Empty means authorization has not been granted yet.
                    return matchState.PowerAuthorizationCode.ToString();
                }

                // In offline mode, do not reveal code until security hold is complete
                if (!_offlineSecurityHoldComplete && _offlineStage < Zone2MissionStage.AuthorizationCodeGranted)
                {
                    return string.Empty;
                }

                if (string.IsNullOrEmpty(_offlineAuthCode))
                {
                    _offlineAuthCode = UnityEngine.Random.Range(0, 10000).ToString("D4");
                }
                return _offlineAuthCode;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            ResolveReferences();
            SubscribeRelayEvents();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            UnsubscribeRelayEvents();
        }

        private void Start()
        {
            // Initial sync of door presentation
            ApplyDoorState(AreZoneDoorsUnlocked);
        }

        private void Update()
        {
            if (TryGetNetworkMatch(out _))
            {
                return;
            }

            if (_offlineRelayRepairDeadline <= 0f
                || _offlineSecurityHoldComplete
                || _offlineStage == Zone2MissionStage.Zone2Completed)
            {
                return;
            }

            if (Time.time >= _offlineRelayRepairDeadline)
            {
                ResetRelayRepairProgressForRetry();
            }
        }

        public void ResolveReferences()
        {
            if (securityTerminal == null)
            {
                securityTerminal = GetComponentInChildren<SecurityTerminalDownload>(true);
                if (securityTerminal == null) securityTerminal = FindAnyObjectByType<SecurityTerminalDownload>(FindObjectsInactive.Include);
            }

            var relayAs = OrderedCandidates(GetComponentsInChildren<RelayAController>(true));
            if (relayAs.Count < 2) relayAs = OrderedCandidates(FindObjectsByType<RelayAController>(FindObjectsInactive.Include));
            AssignDistinct(relayAs, ref relayA1, ref relayA2);

            var relayBs = OrderedCandidates(GetComponentsInChildren<RelayBController>(true));
            if (relayBs.Count < 2) relayBs = OrderedCandidates(FindObjectsByType<RelayBController>(FindObjectsInactive.Include));
            AssignDistinct(relayBs, ref relayB1, ref relayB2);

            var panels = OrderedCandidates(GetComponentsInChildren<PowerControlUIController>(true));
            if (panels.Count < 2) panels = OrderedCandidates(FindObjectsByType<PowerControlUIController>(FindObjectsInactive.Include));
            AssignDistinct(panels, ref distributionPanel1, ref distributionPanel2);

            // Door blockers
            ResolveDoorBlockers();
        }

        private void ResolveDoorBlockers()
        {
            if (doorBlocker1 != null && doorBlocker2 != null)
            {
                return;
            }

            var doorZone = GameObject.Find("DoorZone");
            if (doorZone == null)
            {
                var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
                for (int i = 0; i < allTransforms.Length; i++)
                {
                    if (allTransforms[i].name == "DoorZone")
                    {
                        doorZone = allTransforms[i].gameObject;
                        break;
                    }
                }
            }

            if (doorZone != null)
            {
                var snaps = doorZone.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < snaps.Length; i++)
                {
                    if (snaps[i].name == "LP_Bay_Door_snaps")
                    {
                        if (doorBlocker1 == null) doorBlocker1 = snaps[i].gameObject;
                        else if (doorBlocker2 == null && snaps[i].gameObject != doorBlocker1)
                        {
                            doorBlocker2 = snaps[i].gameObject;
                            break;
                        }
                    }
                }
            }
        }

        private static List<T> OrderedCandidates<T>(T[] candidates) where T : Component
        {
            var ordered = new List<T>(candidates ?? Array.Empty<T>());
            ordered.RemoveAll(candidate => candidate == null);
            ordered.Sort((left, right) => string.CompareOrdinal(HierarchyKey(left.transform), HierarchyKey(right.transform)));
            return ordered;
        }

        private static void AssignDistinct<T>(IReadOnlyList<T> candidates, ref T first, ref T second) where T : Component
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (first == null && candidates[i] != second) first = candidates[i];
                else if (second == null && candidates[i] != first) second = candidates[i];
                if (first != null && second != null) return;
            }
        }

        private static string HierarchyKey(Transform value)
        {
            var parts = new List<string>();
            for (var current = value; current != null; current = current.parent)
            {
                parts.Add($"{current.GetSiblingIndex():D6}:{current.name}");
            }
            parts.Reverse();
            var position = value.position;
            return $"{value.gameObject.scene.path}|{string.Join("/", parts)}|{position.x:R},{position.y:R},{position.z:R}";
        }

        private void SubscribeRelayEvents()
        {
            if (relayA1 != null) relayA1.RelayAOnline += OnRelayA1Online;
            if (relayA2 != null) relayA2.RelayAOnline += OnRelayA2Online;
            if (relayB1 != null) relayB1.RelayBOnline += OnRelayB1Online;
            if (relayB2 != null) relayB2.RelayBOnline += OnRelayB2Online;

            if (securityTerminal != null)
            {
                securityTerminal.DownloadCompleted += OnSecurityHoldCompleted;
            }
        }

        private void UnsubscribeRelayEvents()
        {
            if (relayA1 != null) relayA1.RelayAOnline -= OnRelayA1Online;
            if (relayA2 != null) relayA2.RelayAOnline -= OnRelayA2Online;
            if (relayB1 != null) relayB1.RelayBOnline -= OnRelayB1Online;
            if (relayB2 != null) relayB2.RelayBOnline -= OnRelayB2Online;

            if (securityTerminal != null)
            {
                securityTerminal.DownloadCompleted -= OnSecurityHoldCompleted;
            }
        }

        private void OnRelayA1Online() => ReportRelayOnline(RelaySlot.RelayA_1);
        private void OnRelayA2Online() => ReportRelayOnline(RelaySlot.RelayA_2);
        private void OnRelayB1Online() => ReportRelayOnline(RelaySlot.RelayB_1);
        private void OnRelayB2Online() => ReportRelayOnline(RelaySlot.RelayB_2);

        public void ReportRelayOnline(RelaySlot slot)
        {
            if ((int)slot < 0 || (int)slot > 3) return;

            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
            {
                if (matchState.Object.HasStateAuthority)
                {
                    matchState.TryReportRelayOnline(slot);
                }
                return;
            }

            // Offline / Standalone:
            int bit = 1 << (int)slot;
            if ((_offlineRelayMask & bit) == 0)
            {
                _offlineRelayMask |= bit;
                RelayProgressChanged?.Invoke(CompletedRelayCount);

                if (AreAllRelaysOnline && (_offlineStage == Zone2MissionStage.RepairRelays || _offlineStage == Zone2MissionStage.FindSecurityTerminal))
                {
                    _offlineRelayRepairDeadline = Time.time + RelayRepairRetryWindowSeconds;
                    SetOfflineStage(Zone2MissionStage.SecurityHoldReady);
                }
            }
        }

        public void DiscoverSecurityTerminal()
        {
            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
            {
                matchState.RequestDiscoverSecurityTerminal();
                return;
            }

            // Offline / Standalone:
            if (_offlineStage == Zone2MissionStage.FindSecurityTerminal)
            {
                _offlineTerminalDiscovered = true;
                SetOfflineStage(Zone2MissionStage.RepairRelays);
            }
        }

        public void OnSecurityHoldCompleted(SecurityTerminalDownload terminal)
        {
            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
            {
                return;
            }

            // Offline / Standalone:
            if (!AreAllRelaysOnline)
            {
                Debug.LogWarning("[Zone2MissionDirector] Cannot complete Security Hold before all 4 relays are online.");
                return;
            }

            if (!_offlineSecurityHoldComplete)
            {
                _offlineSecurityHoldComplete = true;
                _offlineRelayRepairDeadline = -1f;
                if (string.IsNullOrEmpty(_offlineAuthCode))
                {
                    _offlineAuthCode = UnityEngine.Random.Range(0, 10000).ToString("D4");
                }
                SetOfflineStage(Zone2MissionStage.AuthorizationCodeGranted);
                AuthorizationCodeRevealed?.Invoke(_offlineAuthCode);

                var flow = FindAnyObjectByType<MatchFlowController>();
                if (flow != null)
                {
                    flow.NotifySecurityHoldComplete();
                }
            }
        }

        public bool SubmitAccessCode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != 4) return false;

            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
            {
                // Legacy compatibility cannot identify an exact authorization panel.
                return false;
            }

            // Offline / Standalone:
            if (!AreAllRelaysOnline || !IsSecurityHoldComplete)
            {
                return false;
            }

            if (_offlineStage != Zone2MissionStage.AuthorizationCodeGranted
                && _offlineStage != Zone2MissionStage.UnlockZoneDoors
                && _offlineStage != Zone2MissionStage.Zone2Completed)
            {
                return false;
            }

            string expectedCode = AuthorizationCode;
            if (string.IsNullOrEmpty(expectedCode))
            {
                return false;
            }

            if (code == expectedCode)
            {
                _offlineZoneDoorsUnlocked = true;
                SetOfflineStage(Zone2MissionStage.Zone2Completed);
                ApplyDoorState(true);
                RefreshBothPanels();
                ZoneDoorsUnlockedEvent?.Invoke();

                var flow = FindAnyObjectByType<MatchFlowController>();
                if (flow != null)
                {
                    flow.NotifyPowerPuzzleComplete();
                }

                return true;
            }

            return false;
        }

        public bool TryGetRelaySlot(RelayAController controller, out RelaySlot slot)
        {
            if (controller == relayA1) { slot = RelaySlot.RelayA_1; return true; }
            if (controller == relayA2) { slot = RelaySlot.RelayA_2; return true; }
            slot = default;
            return false;
        }

        public bool TryGetRelaySlot(RelayBController controller, out RelaySlot slot)
        {
            if (controller == relayB1) { slot = RelaySlot.RelayB_1; return true; }
            if (controller == relayB2) { slot = RelaySlot.RelayB_2; return true; }
            slot = default;
            return false;
        }

        public bool TryGetDistributionPanelIndex(PowerControlUIController panel, out int panelIndex)
        {
            if (panel == distributionPanel1) { panelIndex = 0; return true; }
            if (panel == distributionPanel2) { panelIndex = 1; return true; }
            panelIndex = -1;
            return false;
        }

        public bool RequestRelayAcquire(RelayAController controller) =>
            TryGetNetworkMatch(out var matchState) && TryGetRelaySlot(controller, out var slot) && matchState.RequestAcquireRelay(slot);

        public bool RequestRelayAcquire(RelayBController controller) =>
            TryGetNetworkMatch(out var matchState) && TryGetRelaySlot(controller, out var slot) && matchState.RequestAcquireRelay(slot);

        public void RequestRelayRelease(RelayAController controller)
        {
            if (TryGetNetworkMatch(out var matchState) && TryGetRelaySlot(controller, out var slot)) matchState.RequestReleaseRelay(slot);
        }

        public void RequestRelayRelease(RelayBController controller)
        {
            if (TryGetNetworkMatch(out var matchState) && TryGetRelaySlot(controller, out var slot)) matchState.RequestReleaseRelay(slot);
        }

        public bool RequestRelayAControls(RelayAController controller, float generatorOutput, float frequencyRegulator, float loadDistribution) =>
            TryGetNetworkMatch(out var matchState) && TryGetRelaySlot(controller, out var slot)
            && matchState.RequestRelayAControls(slot, generatorOutput, frequencyRegulator, loadDistribution);

        public bool RequestRelayAStart(RelayAController controller) =>
            TryGetNetworkMatch(out var matchState) && TryGetRelaySlot(controller, out var slot) && matchState.RequestRelayAStart(slot);

        public bool RequestRelayAEmergencyStop(RelayAController controller) =>
            TryGetNetworkMatch(out var matchState) && TryGetRelaySlot(controller, out var slot) && matchState.RequestRelayAEmergencyStop(slot);

        public bool RequestRelayBControls(RelayBController controller, int channel, float frequency, float phase) =>
            TryGetNetworkMatch(out var matchState) && TryGetRelaySlot(controller, out var slot)
            && matchState.RequestRelayBControls(slot, channel, frequency, phase);

        public bool RequestRelayBScan(RelayBController controller) =>
            TryGetNetworkMatch(out var matchState) && TryGetRelaySlot(controller, out var slot) && matchState.RequestRelayBScan(slot);

        public bool RequestRelayBStartSync(RelayBController controller) =>
            TryGetNetworkMatch(out var matchState) && TryGetRelaySlot(controller, out var slot) && matchState.RequestRelayBStartSync(slot);

        public bool RequestRelayBCancelSync(RelayBController controller) =>
            TryGetNetworkMatch(out var matchState) && TryGetRelaySlot(controller, out var slot) && matchState.RequestRelayBCancelSync(slot);

        public bool CanLocalPlayerOperateRelay(RelayAController controller) =>
            !TryGetNetworkMatch(out var matchState) || (TryGetRelaySlot(controller, out var slot)
                && CanLocalPlayerOperateRelay(matchState, slot));

        public bool CanLocalPlayerOperateRelay(RelayBController controller) =>
            !TryGetNetworkMatch(out var matchState) || (TryGetRelaySlot(controller, out var slot)
                && CanLocalPlayerOperateRelay(matchState, slot));

        public bool CanLocalPlayerOperateSecurityTerminal(SecurityTerminalDownload terminal)
        {
            if (!TryGetNetworkMatch(out var matchState))
            {
                return terminal == securityTerminal;
            }

            if (terminal != securityTerminal
                || matchState.Runner == null
                || matchState.CurrentPhase != NetworkMatchPhase.Zone2Objective
                || !matchState.AreAllRelaysOnline
                || matchState.SecurityHoldCompleted
                || (matchState.Zone2Stage != Zone2MissionStage.SecurityHoldReady
                    && matchState.Zone2Stage != Zone2MissionStage.SecurityHold))
            {
                return false;
            }

            return matchState.SecurityHoldParticipantCount < 4
                || matchState.SecurityHoldOperator == matchState.Runner.LocalPlayer
                || matchState.SecurityHoldOperator2 == matchState.Runner.LocalPlayer
                || matchState.SecurityHoldOperator3 == matchState.Runner.LocalPlayer
                || matchState.SecurityHoldOperator4 == matchState.Runner.LocalPlayer;
        }

        public bool RequestStartSecurityHold(SecurityTerminalDownload terminal)
        {
            return terminal == securityTerminal && TryGetNetworkMatch(out var matchState)
                && matchState.RequestStartSecurityHold();
        }

        public void RequestCancelSecurityHold(SecurityTerminalDownload terminal)
        {
            if (terminal == securityTerminal && TryGetNetworkMatch(out var matchState)) matchState.RequestCancelSecurityHold();
        }

        public Zone2AccessSubmissionDisposition SubmitAccessCode(PowerControlUIController panel, string code)
        {
            if (TryGetNetworkMatch(out var matchState))
            {
                return TryGetDistributionPanelIndex(panel, out var panelIndex)
                    ? matchState.RequestSubmitZoneAccessCode(panelIndex, code)
                    : Zone2AccessSubmissionDisposition.Rejected;
            }
            return SubmitAccessCode(code)
                ? Zone2AccessSubmissionDisposition.Accepted
                : Zone2AccessSubmissionDisposition.Rejected;
        }

        private static bool CanLocalPlayerOperateRelay(NetworkMatchState matchState, RelaySlot slot)
        {
            if (matchState == null || matchState.Object == null || !matchState.Object.IsValid || matchState.Runner == null)
            {
                return false;
            }

            int slotIndex = (int)slot;
            if (slotIndex < 0 || slotIndex > 3
                || matchState.CurrentPhase != NetworkMatchPhase.Zone2Objective
                || matchState.Zone2Stage != Zone2MissionStage.RepairRelays
                || (matchState.RelayCompletionMask & (1 << slotIndex)) != 0)
            {
                return false;
            }

            PlayerRef current = slot switch
            {
                RelaySlot.RelayA_1 => matchState.RelayA1Operator,
                RelaySlot.RelayA_2 => matchState.RelayA2Operator,
                RelaySlot.RelayB_1 => matchState.RelayB1Operator,
                RelaySlot.RelayB_2 => matchState.RelayB2Operator,
                _ => PlayerRef.None,
            };

            return current.IsNone || current == matchState.Runner.LocalPlayer;
        }

        private static bool TryGetNetworkMatch(out NetworkMatchState matchState)
        {
            matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            return matchState != null && matchState.Object != null && matchState.Object.IsValid;
        }

        public void ApplyDoorState(bool unlocked)
        {
            if (doorBlocker1 == null || doorBlocker2 == null)
            {
                ResolveDoorBlockers();
            }

            SetBlockerState(doorBlocker1, unlocked);
            SetBlockerState(doorBlocker2, unlocked);
        }

        private void SetBlockerState(GameObject blocker, bool unlocked)
        {
            if (blocker == null) return;

            if (blocker.activeSelf == unlocked)
            {
                blocker.SetActive(!unlocked);
            }

            EnsurePassageColliders(blocker, !unlocked);
        }

        private void EnsurePassageColliders(GameObject blocker, bool active)
        {
            if (blocker == null) return;
            var colliders = blocker.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = active;
            }

            // The bay-door prefab puts its passage-wide collider on the wall sibling.
            Transform wall = blocker.transform.parent != null
                ? blocker.transform.parent.Find("LP_Bay_Door_Wall_snaps")
                : null;
            if (wall != null)
            {
                BoxCollider passageCollider = null;
                var wallColliders = wall.GetComponents<BoxCollider>();
                for (int i = 0; i < wallColliders.Length; i++)
                {
                    if (passageCollider == null || wallColliders[i].size.z > passageCollider.size.z)
                    {
                        passageCollider = wallColliders[i];
                    }
                }

                if (passageCollider != null)
                {
                    passageCollider.enabled = active;
                }
            }

            var obstacles = blocker.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
            for (int i = 0; i < obstacles.Length; i++)
            {
                obstacles[i].enabled = active;
            }
        }

        public void RefreshBothPanels()
        {
            if (distributionPanel1 != null) distributionPanel1.RefreshDisplay();
            if (distributionPanel2 != null) distributionPanel2.RefreshDisplay();
        }

        public void ResetRelayRepairProgressForRetry()
        {
            _offlineRelayMask = 0;
            _offlineSecurityHoldComplete = false;
            _offlineRelayRepairDeadline = -1f;
            securityTerminal?.ResetDownload();
            relayA1?.ResetForRetry();
            relayA2?.ResetForRetry();
            relayB1?.ResetForRetry();
            relayB2?.ResetForRetry();
            RelayProgressChanged?.Invoke(0);
            SetOfflineStage(Zone2MissionStage.RepairRelays);
            RefreshBothPanels();
        }

        public void SetOfflineStage(Zone2MissionStage nextStage)
        {
            if (_offlineStage == nextStage) return;
            _offlineStage = nextStage;
            StageChanged?.Invoke(_offlineStage);
            RefreshBothPanels();
        }

        public void OnAuthoritativeStateChanged(
            Zone2MissionStage stage,
            int relayMask,
            bool terminalDiscovered,
            bool securityHoldComplete,
            bool doorsUnlocked,
            string code)
        {
            bool relayRetryReset =
                _hasAuthoritativePresentation
                && _presentedRelayMask != 0
                && relayMask == 0
                && stage == Zone2MissionStage.RepairRelays
                && !securityHoldComplete
                && !doorsUnlocked;

            if (relayRetryReset)
            {
                ApplyRelayRetryPresentationReset();
            }

            var matchState = NetworkMatchState.Instance;
            if (matchState != null && matchState.Object != null && matchState.Object.IsValid
                && matchState.HasInitializedZone2RelayRuntime)
            {
                ApplyRelayPresentation(matchState, relayMask);
            }

            bool first = !_hasAuthoritativePresentation;
            bool stageChanged = first || _presentedStage != stage;
            bool relayMaskChanged = first || _presentedRelayMask != relayMask;
            bool terminalChanged = first || _presentedTerminalDiscovered != terminalDiscovered;
            bool securityChanged = first || _presentedSecurityHoldComplete != securityHoldComplete;
            bool doorsChanged = first || _presentedDoorsUnlocked != doorsUnlocked;
            bool codeChanged = !string.Equals(_presentedAuthorizationCode, code, StringComparison.Ordinal);

            if (doorsChanged) ApplyDoorState(doorsUnlocked);
            if (stageChanged) StageChanged?.Invoke(stage);
            if (relayMaskChanged) RelayProgressChanged?.Invoke(CountRelays(relayMask));
            if (codeChanged && !string.IsNullOrEmpty(code)) AuthorizationCodeRevealed?.Invoke(code);
            if (doorsUnlocked && (!_hasAuthoritativePresentation || !_presentedDoorsUnlocked)) ZoneDoorsUnlockedEvent?.Invoke();
            if (stageChanged || relayMaskChanged || terminalChanged || securityChanged || doorsChanged || codeChanged) RefreshBothPanels();

            _hasAuthoritativePresentation = true;
            _presentedStage = stage;
            _presentedRelayMask = relayMask;
            _presentedTerminalDiscovered = terminalDiscovered;
            _presentedSecurityHoldComplete = securityHoldComplete;
            _presentedDoorsUnlocked = doorsUnlocked;
            _presentedAuthorizationCode = code ?? string.Empty;
        }

        private void ApplyRelayRetryPresentationReset()
        {
            securityTerminal?.ResetDownload();
            relayA1?.ResetForRetry();
            relayA2?.ResetForRetry();
            relayB1?.ResetForRetry();
            relayB2?.ResetForRetry();
        }

        private void ApplyRelayPresentation(NetworkMatchState matchState, int relayMask)
        {
            relayA1?.ApplyAuthoritativeAttemptSeed(matchState.RelayA1AttemptSeed);
            relayA1?.ApplyAuthoritativeControls(matchState.RelayA1Controls.x, matchState.RelayA1Controls.y, matchState.RelayA1Controls.z);
            relayA1?.ApplyAuthoritativeRunningState(matchState.RelayA1Running);
            relayA2?.ApplyAuthoritativeAttemptSeed(matchState.RelayA2AttemptSeed);
            relayA2?.ApplyAuthoritativeControls(matchState.RelayA2Controls.x, matchState.RelayA2Controls.y, matchState.RelayA2Controls.z);
            relayA2?.ApplyAuthoritativeRunningState(matchState.RelayA2Running);

            if (relayB1 != null)
            {
                relayB1.ApplyAuthoritativeAttempt(matchState.RelayB1PresetIndex, matchState.RelayB1AttemptSeed);
                relayB1.ApplyAuthoritativeControls(matchState.RelayB1Channel, matchState.RelayB1Frequency, matchState.RelayB1Phase);
                relayB1.ApplyAuthoritativeSyncState(matchState.RelayB1Synchronizing);
            }
            if (relayB2 != null)
            {
                relayB2.ApplyAuthoritativeAttempt(matchState.RelayB2PresetIndex, matchState.RelayB2AttemptSeed);
                relayB2.ApplyAuthoritativeControls(matchState.RelayB2Channel, matchState.RelayB2Frequency, matchState.RelayB2Phase);
                relayB2.ApplyAuthoritativeSyncState(matchState.RelayB2Synchronizing);
            }

            if ((relayMask & (1 << (int)RelaySlot.RelayA_1)) != 0) relayA1?.ApplyOnlineFromAuthority();
            if ((relayMask & (1 << (int)RelaySlot.RelayA_2)) != 0) relayA2?.ApplyOnlineFromAuthority();
            if ((relayMask & (1 << (int)RelaySlot.RelayB_1)) != 0) relayB1?.ApplyOnlineFromAuthority();
            if ((relayMask & (1 << (int)RelaySlot.RelayB_2)) != 0) relayB2?.ApplyOnlineFromAuthority();
        }

        private static int CountRelays(int mask) =>
            (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1) + ((mask >> 3) & 1);

        private void OnValidate()
        {
            if (relayA1 != null && relayA2 != null && relayA1 == relayA2)
            {
                Debug.LogWarning("[Zone2MissionDirector] RelayA1 and RelayA2 reference the same object!", this);
            }
            if (relayB1 != null && relayB2 != null && relayB1 == relayB2)
            {
                Debug.LogWarning("[Zone2MissionDirector] RelayB1 and RelayB2 reference the same object!", this);
            }
            if (distributionPanel1 != null && distributionPanel2 != null && distributionPanel1 == distributionPanel2)
            {
                Debug.LogWarning("[Zone2MissionDirector] DistributionPanel1 and DistributionPanel2 reference the same object!", this);
            }
            if (doorBlocker1 != null && doorBlocker2 != null && doorBlocker1 == doorBlocker2)
            {
                Debug.LogWarning("[Zone2MissionDirector] DoorBlocker1 and DoorBlocker2 reference the same object!", this);
            }
        }
    }
}
