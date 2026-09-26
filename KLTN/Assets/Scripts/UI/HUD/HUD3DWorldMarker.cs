using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    public class HUD3DWorldMarker : MonoBehaviour
    {
        [System.Serializable]
        public class MarkerItemUI
        {
            public GameObject root;
            public RectTransform rectTransform;
            public Image icon;
            public Image pulseRing;
            public Text titleText;
            public Text distanceText;
            public Image arrowPointer;
        }

        [Header("Marker Pool (Up to 4 concurrent markers)")]
        [SerializeField] private MarkerItemUI[] markerPool = new MarkerItemUI[4];

        [Header("Settings")]
        [SerializeField] private float edgePadding = 48f;
        [SerializeField] private Color downedTeammateColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color escapeDoorColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] private Color sectorBoxColor = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color terminalColor = new Color(0f, 0.95f, 1f, 1f);
        [SerializeField] private Color distributionPanelColor = new Color(0.2f, 1f, 0.4f, 1f);

        private Camera _mainCamera;
        private MatchFlowController _matchFlow;
        private EscapeDoorCountdown _escapeDoor;
        private PlayerEnergyCoreCarrier _localCarrier;
        private Networking.LobbyPlayerState _localPlayerState;
        private Networking.NetworkSectorBox _networkSectorBox;
        private SectorBox _sectorBox;

        private readonly List<Transform> _targetTransforms = new List<Transform>();
        private readonly List<string> _targetTitles = new List<string>();
        private readonly List<Color> _targetColors = new List<Color>();

        private void Awake()
        {
            if (markerPool != null)
            {
                for (int i = 0; i < markerPool.Length; i++)
                {
                    if (markerPool[i]?.root != null)
                    {
                        markerPool[i].root.SetActive(false);
                    }
                }
            }
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            _matchFlow = FindAnyObjectByType<MatchFlowController>();
            _escapeDoor = FindAnyObjectByType<EscapeDoorCountdown>();
        }

        private void LateUpdate()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            CollectTargets();
            RenderMarkers();
        }

        private void CollectTargets()
        {
            _targetTransforms.Clear();
            _targetTitles.Clear();
            _targetColors.Clear();

            // 1. Downed Teammates (Need Help)
            PlayerDownState[] players = FindObjectsByType<PlayerDownState>();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerDownState p = players[i];
                if (p == null || !p.IsDowned) continue;

                // Check if this is not local player (or if local player is downed, local player doesn't need 3D offscreen marker)
                if (p.GetComponent<PlayerMovement>() != null && p.GetComponent<PlayerMovement>().enabled == false && p.BleedoutRemaining <= 0)
                    continue;

                _targetTransforms.Add(p.transform);
                _targetTitles.Add($"CỨU ĐỒNG ĐỘI ({p.BleedoutRemaining:F0}s)");
                _targetColors.Add(downedTeammateColor);
            }

            // 2. Sector Box (Trạm nạp điện Zone 1) - Only shown when player carries Energy Core
            CollectSectorBoxTarget();

            // 3. Security Terminal / Exit Panel (Zone 2) - Only shown when Zone 2 entry trigger activated
            CollectZone2TerminalTarget();

            // 4. Escape Door during FinalHunt or ExitCountdown
            CollectEscapeDoorTarget();
        }

        private void CollectSectorBoxTarget()
        {
            if (_localCarrier == null)
            {
                var carriers = FindObjectsByType<PlayerEnergyCoreCarrier>(FindObjectsInactive.Exclude);
                for (int i = 0; i < carriers.Length; i++)
                {
                    var pm = carriers[i].GetComponent<PlayerMovement>();
                    if (pm != null && pm.enabled)
                    {
                        _localCarrier = carriers[i];
                        break;
                    }
                }
            }

            if (_localPlayerState == null)
            {
                var states = FindObjectsByType<Networking.LobbyPlayerState>(FindObjectsInactive.Exclude);
                for (int i = 0; i < states.Length; i++)
                {
                    if (states[i].Object != null && states[i].Object.IsValid && states[i].Object.HasInputAuthority)
                    {
                        _localPlayerState = states[i];
                        break;
                    }
                }
            }

            bool isCarryingCore = (_localCarrier != null && _localCarrier.IsCarrying)
                || (_localPlayerState != null && _localPlayerState.CarriedCoreId.IsValid);

            if (!isCarryingCore) return;

            if (_networkSectorBox == null) _networkSectorBox = FindAnyObjectByType<Networking.NetworkSectorBox>();
            if (_sectorBox == null) _sectorBox = FindAnyObjectByType<SectorBox>();

            Transform target = null;
            bool isComplete = false;

            if (_networkSectorBox != null && _networkSectorBox.gameObject.activeInHierarchy)
            {
                target = _networkSectorBox.transform;
                isComplete = _networkSectorBox.IsCoreObjectiveComplete;
            }
            else if (_sectorBox != null && _sectorBox.gameObject.activeInHierarchy)
            {
                target = _sectorBox.transform;
                isComplete = _sectorBox.PlacedCoreCount >= _sectorBox.MaxCoreCapacity;
            }

            if (target != null && !isComplete)
            {
                _targetTransforms.Add(target);
                _targetTitles.Add("TRẠM NẠP ĐIỆN (SECTOR BOX)");
                _targetColors.Add(sectorBoxColor);
            }
        }

        private void CollectZone2TerminalTarget()
        {
            bool zone2Active = EchoProtocol.Networking.StalkerZone2EntryTrigger.Zone2Triggered;
            var director = EchoProtocol.MatchFlow.Zone2MissionDirector.Instance;
            if (director != null && director.CurrentStage >= EchoProtocol.MatchFlow.Zone2MissionStage.FindSecurityTerminal)
            {
                zone2Active = true;
            }

            if (!zone2Active) return;

            if (director != null)
            {
                var stage = director.CurrentStage;
                if (stage < EchoProtocol.MatchFlow.Zone2MissionStage.AuthorizationCodeGranted)
                {
                    var terminal = director.SecurityTerminal;
                    if (terminal != null && !terminal.IsComplete)
                    {
                        _targetTransforms.Add(terminal.transform);
                        _targetTitles.Add("MÁY BẢO MẬT (SECURITY TERMINAL)");
                        _targetColors.Add(terminalColor);
                    }
                }
                else if (stage < EchoProtocol.MatchFlow.Zone2MissionStage.Zone2Completed)
                {
                    var panel = director.DistributionPanel1 != null ? director.DistributionPanel1 : director.DistributionPanel2;
                    if (panel != null)
                    {
                        _targetTransforms.Add(panel.transform);
                        _targetTitles.Add("BẢNG ĐIỀU KHIỂN CỬA (DISTRIBUTION PANEL)");
                        _targetColors.Add(distributionPanelColor);
                    }
                }
            }
            else
            {
                var terminal = FindAnyObjectByType<SecurityTerminalDownload>();
                if (terminal != null && !terminal.IsComplete)
                {
                    _targetTransforms.Add(terminal.transform);
                    _targetTitles.Add("MÁY BẢO MẬT (SECURITY TERMINAL)");
                    _targetColors.Add(terminalColor);
                }
            }
        }

        private void CollectEscapeDoorTarget()
        {
            if (_escapeDoor == null) _escapeDoor = FindAnyObjectByType<EscapeDoorCountdown>();
            if (_escapeDoor != null && _matchFlow != null &&
                (_matchFlow.Phase == MatchPhase.FinalHunt || _matchFlow.Phase == MatchPhase.ExitCountdown))
            {
                _targetTransforms.Add(_escapeDoor.transform);
                string doorTitle = _escapeDoor.IsComplete ? "CỬA THOÁT HIỂM [SẴN SÀNG]" : "CỬA THOÁT HIỂM [ĐANG MỞ]";
                _targetTitles.Add(doorTitle);
                _targetColors.Add(escapeDoorColor);
            }
        }

        private void RenderMarkers()
        {
            Vector3 camPos = _mainCamera.transform.position;
            Vector3 camForward = _mainCamera.transform.forward;

            for (int i = 0; i < markerPool.Length; i++)
            {
                MarkerItemUI marker = markerPool[i];
                if (marker == null || marker.root == null) continue;

                if (i >= _targetTransforms.Count)
                {
                    marker.root.SetActive(false);
                    continue;
                }

                Transform target = _targetTransforms[i];
                if (target == null)
                {
                    marker.root.SetActive(false);
                    continue;
                }

                marker.root.SetActive(true);

                Vector3 worldPos = target.position + Vector3.up * 1.0f;
                float distance = Vector3.Distance(camPos, worldPos);

                Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);
                bool isBehind = Vector3.Dot(camForward, worldPos - camPos) <= 0f;

                if (isBehind)
                {
                    screenPos.x = Screen.width - screenPos.x;
                    screenPos.y = Screen.height - screenPos.y;
                }

                Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 fromCenter = new Vector2(screenPos.x - screenCenter.x, screenPos.y - screenCenter.y);

                bool isOffScreen = screenPos.x < edgePadding || screenPos.x > (Screen.width - edgePadding) ||
                                  screenPos.y < edgePadding || screenPos.y > (Screen.height - edgePadding) || isBehind;

                if (isOffScreen)
                {
                    // Clamp to edge
                    float minX = edgePadding;
                    float maxX = Screen.width - edgePadding;
                    float minY = edgePadding;
                    float maxY = Screen.height - edgePadding;

                    float slope = fromCenter.y / (fromCenter.x != 0f ? fromCenter.x : 0.0001f);

                    if (fromCenter.x > 0f)
                    {
                        screenPos.x = maxX;
                        screenPos.y = screenCenter.y + (maxX - screenCenter.x) * slope;
                    }
                    else
                    {
                        screenPos.x = minX;
                        screenPos.y = screenCenter.y + (minX - screenCenter.x) * slope;
                    }

                    if (screenPos.y > maxY)
                    {
                        screenPos.y = maxY;
                        screenPos.x = screenCenter.x + (maxY - screenCenter.y) / slope;
                    }
                    else if (screenPos.y < minY)
                    {
                        screenPos.y = minY;
                        screenPos.x = screenCenter.x + (minY - screenCenter.y) / slope;
                    }

                    screenPos.x = Mathf.Clamp(screenPos.x, minX, maxX);
                    screenPos.y = Mathf.Clamp(screenPos.y, minY, maxY);

                    // Show arrow pointing outward
                    if (marker.arrowPointer != null)
                    {
                        marker.arrowPointer.gameObject.SetActive(true);
                        float angle = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;
                        marker.arrowPointer.rectTransform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
                    }
                }
                else
                {
                    if (marker.arrowPointer != null)
                    {
                        marker.arrowPointer.gameObject.SetActive(false);
                    }
                }

                marker.rectTransform.position = screenPos;

                // Visual content
                Color color = _targetColors[i];
                if (marker.icon != null) marker.icon.color = color;
                if (marker.titleText != null)
                {
                    marker.titleText.text = _targetTitles[i];
                    marker.titleText.color = color;
                }

                if (marker.distanceText != null)
                {
                    marker.distanceText.text = $"{distance:F0}m";
                }

                // Pulsate ring
                if (marker.pulseRing != null)
                {
                    float pulse = Mathf.PingPong(Time.time * 2.5f, 1f);
                    marker.pulseRing.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.4f, pulse);
                    marker.pulseRing.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.9f, 0.1f, pulse));
                }
            }
        }
    }
}
