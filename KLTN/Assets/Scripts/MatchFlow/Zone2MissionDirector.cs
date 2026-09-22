using System;
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

        private Zone2MissionStage _offlineStage = Zone2MissionStage.FindSecurityTerminal;
        private int _offlineRelayMask;
        private bool _offlineTerminalDiscovered;
        private bool _offlineSecurityHoldComplete;
        private bool _offlineZoneDoorsUnlocked;
        private string _offlineAuthCode;

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
                if (matchState != null && matchState.Object != null && matchState.Object.IsValid && !string.IsNullOrEmpty(matchState.PowerAuthorizationCode.ToString()))
                {
                    return matchState.PowerAuthorizationCode.ToString();
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

        public void ResolveReferences()
        {
            if (securityTerminal == null)
            {
                securityTerminal = GetComponentInChildren<SecurityTerminalDownload>(true);
                if (securityTerminal == null) securityTerminal = FindAnyObjectByType<SecurityTerminalDownload>(FindObjectsInactive.Include);
            }

            var relayAs = GetComponentsInChildren<RelayAController>(true);
            if (relayAs.Length >= 2)
            {
                if (relayA1 == null) relayA1 = relayAs[0];
                if (relayA2 == null) relayA2 = relayAs[1];
            }
            else
            {
                var sceneRelayAs = FindObjectsByType<RelayAController>(FindObjectsInactive.Include);
                if (sceneRelayAs.Length >= 2)
                {
                    if (relayA1 == null) relayA1 = sceneRelayAs[0];
                    if (relayA2 == null) relayA2 = sceneRelayAs[1];
                }
                else if (sceneRelayAs.Length == 1 && relayA1 == null)
                {
                    relayA1 = sceneRelayAs[0];
                }
            }

            var relayBs = GetComponentsInChildren<RelayBController>(true);
            if (relayBs.Length >= 2)
            {
                if (relayB1 == null) relayB1 = relayBs[0];
                if (relayB2 == null) relayB2 = relayBs[1];
            }
            else
            {
                var sceneRelayBs = FindObjectsByType<RelayBController>(FindObjectsInactive.Include);
                if (sceneRelayBs.Length >= 2)
                {
                    if (relayB1 == null) relayB1 = sceneRelayBs[0];
                    if (relayB2 == null) relayB2 = sceneRelayBs[1];
                }
                else if (sceneRelayBs.Length == 1 && relayB1 == null)
                {
                    relayB1 = sceneRelayBs[0];
                }
            }

            var panels = GetComponentsInChildren<PowerControlUIController>(true);
            if (panels.Length >= 2)
            {
                if (distributionPanel1 == null) distributionPanel1 = panels[0];
                if (distributionPanel2 == null) distributionPanel2 = panels[1];
            }
            else
            {
                var scenePanels = FindObjectsByType<PowerControlUIController>(FindObjectsInactive.Include);
                if (scenePanels.Length >= 2)
                {
                    if (distributionPanel1 == null) distributionPanel1 = scenePanels[0];
                    if (distributionPanel2 == null) distributionPanel2 = scenePanels[1];
                }
                else if (scenePanels.Length == 1 && distributionPanel1 == null)
                {
                    distributionPanel1 = scenePanels[0];
                }
            }

            // Door blockers
            if (doorBlocker1 == null || doorBlocker2 == null)
            {
                var doorZone = GameObject.Find("DoorZone");
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
            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
            {
                if (matchState.Object.HasStateAuthority)
                {
                    matchState.TryReportRelayOnline(slot);
                }
                else
                {
                    matchState.RpcReportRelayOnline(matchState.Runner.LocalPlayer, (int)slot);
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
                    SetOfflineStage(Zone2MissionStage.SecurityHoldReady);
                }
            }
        }

        public void DiscoverSecurityTerminal()
        {
            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
            {
                if (matchState.Object.HasStateAuthority)
                {
                    matchState.TryDiscoverSecurityTerminal(PlayerRef.None);
                }
                else
                {
                    matchState.RpcDiscoverSecurityTerminal(matchState.Runner.LocalPlayer);
                }
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
                if (matchState.Object.HasStateAuthority)
                {
                    matchState.TryCompleteSecurityHold(default);
                }
                return;
            }

            // Offline / Standalone:
            if (!_offlineSecurityHoldComplete)
            {
                _offlineSecurityHoldComplete = true;
                if (string.IsNullOrEmpty(_offlineAuthCode))
                {
                    _offlineAuthCode = UnityEngine.Random.Range(0, 10000).ToString("D4");
                }
                SetOfflineStage(Zone2MissionStage.AuthorizationCodeGranted);
                AuthorizationCodeRevealed?.Invoke(_offlineAuthCode);
            }
        }

        public bool SubmitAccessCode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != 4) return false;

            var matchState = NetworkMatchState.Instance ?? FindAnyObjectByType<NetworkMatchState>();
            if (matchState != null && matchState.Object != null && matchState.Object.IsValid)
            {
                if (matchState.Object.HasStateAuthority)
                {
                    return matchState.TrySubmitZoneAccessCode(PlayerRef.None, code);
                }
                else
                {
                    if (code == matchState.PowerAuthorizationCode.ToString())
                    {
                        matchState.RpcSubmitZoneAccessCode(matchState.Runner.LocalPlayer, code);
                        return true;
                    }
                    return false;
                }
            }

            // Offline / Standalone:
            if (code == AuthorizationCode)
            {
                _offlineZoneDoorsUnlocked = true;
                SetOfflineStage(Zone2MissionStage.Zone2Completed);
                ApplyDoorState(true);
                RefreshBothPanels();
                ZoneDoorsUnlockedEvent?.Invoke();
                return true;
            }

            return false;
        }

        public void ApplyDoorState(bool unlocked)
        {
            if (doorBlocker1 != null && doorBlocker1.activeSelf == unlocked)
            {
                doorBlocker1.SetActive(!unlocked);
            }
            if (doorBlocker2 != null && doorBlocker2.activeSelf == unlocked)
            {
                doorBlocker2.SetActive(!unlocked);
            }

            // Also ensure colliders or obstacles on blockers are disabled
            EnsurePassageColliders(doorBlocker1, !unlocked);
            EnsurePassageColliders(doorBlocker2, !unlocked);
        }

        private void EnsurePassageColliders(GameObject blocker, bool active)
        {
            if (blocker == null) return;
            var colliders = blocker.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = active;
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
            ApplyDoorState(doorsUnlocked);
            RefreshBothPanels();
            StageChanged?.Invoke(stage);
            RelayProgressChanged?.Invoke(CompletedRelayCount);
            if (doorsUnlocked)
            {
                ZoneDoorsUnlockedEvent?.Invoke();
            }
        }

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
