using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    public class HUDTeammateStatus : MonoBehaviour
    {
        [Serializable]
        public class TeammateSlotUI
        {
            public GameObject root;
            public Image background;
            public Image accentBar;
            public Text nameText;
            public Image statusBadgeBg;
            public Text statusText;
            public Image coreCarryIcon;
            public Image healthFill;
            public Text distanceText;

            [HideInInspector] public PlayerDownState boundPlayer;
            [HideInInspector] public PlayerEnergyCoreCarrier boundCarrier;
            [HideInInspector] public string simulatedName;
            [HideInInspector] public bool isSimulated;
        }

        [Header("UI Slots (Up to 4 players)")]
        [SerializeField] private TeammateSlotUI[] slots = new TeammateSlotUI[4];

        [Header("Options")]
        [SerializeField] private bool simulateTeammatesIfSolo = false;
        [SerializeField] private CanvasGroup panelCanvasGroup;

        [Header("Colors")]
        [SerializeField] private Color healthyColor = new Color(0f, 0.9f, 0.45f, 1f);
        [SerializeField] private Color carryingColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] private Color downedColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color eliminatedColor = new Color(0.5f, 0.55f, 0.6f, 0.8f);
        [SerializeField] private Color escapedColor = new Color(1f, 0.84f, 0f, 1f);

        private readonly List<PlayerDownState> _discoveredPlayers = new List<PlayerDownState>();
        private Camera _mainCamera;
        private MatchFlowController _matchFlow;
        private float _refreshTimer;

        public event Action<PlayerDownState> TeammateDowned;
        public IReadOnlyList<PlayerDownState> DiscoveredPlayers => _discoveredPlayers;

        private void Awake()
        {
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = GetComponent<CanvasGroup>();
                if (panelCanvasGroup == null)
                {
                    panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i]?.root != null)
                    {
                        slots[i].root.SetActive(false);
                    }
                }
            }
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            _matchFlow = FindAnyObjectByType<MatchFlowController>();
            RefreshDiscoveredPlayers();
        }

        private void Update()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = 1.0f;
                RefreshDiscoveredPlayers();
            }

            UpdateSlotsDisplay();
        }

        public void RefreshDiscoveredPlayers()
        {
            _discoveredPlayers.Clear();
            PlayerDownState[] allDownStates = FindObjectsByType<PlayerDownState>(FindObjectsSortMode.None);
            if (allDownStates != null)
            {
                for (int i = 0; i < allDownStates.Length; i++)
                {
                    if (allDownStates[i] != null)
                    {
                        _discoveredPlayers.Add(allDownStates[i]);
                    }
                }
            }

            // Bind to slots
            int realPlayerCount = _discoveredPlayers.Count;
            bool hasTeammates = realPlayerCount > 1 || simulateTeammatesIfSolo;

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = hasTeammates ? 1f : 0f;
                panelCanvasGroup.interactable = hasTeammates;
                panelCanvasGroup.blocksRaycasts = hasTeammates;
            }

            if (!hasTeammates)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i]?.root != null) slots[i].root.SetActive(false);
                }
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;

                if (i < realPlayerCount)
                {
                    slots[i].boundPlayer = _discoveredPlayers[i];
                    slots[i].boundCarrier = _discoveredPlayers[i].GetComponent<PlayerEnergyCoreCarrier>();
                    slots[i].isSimulated = false;
                    slots[i].simulatedName = null;
                    if (slots[i].root != null) slots[i].root.SetActive(true);
                }
                else if (simulateTeammatesIfSolo && realPlayerCount <= 1)
                {
                    // Provide 3 simulated teammates if solo testing to showcase complete 4-player HUD
                    slots[i].boundPlayer = null;
                    slots[i].boundCarrier = null;
                    slots[i].isSimulated = true;
                    slots[i].simulatedName = i switch
                    {
                        1 => "Đồng đội 2 (Alex)",
                        2 => "Đồng đội 3 (Kael)",
                        3 => "Đồng đội 4 (Elena)",
                        _ => $"Đồng đội {i + 1}"
                    };
                    if (slots[i].root != null) slots[i].root.SetActive(true);
                }
                else
                {
                    slots[i].boundPlayer = null;
                    slots[i].boundCarrier = null;
                    slots[i].isSimulated = false;
                    if (slots[i].root != null) slots[i].root.SetActive(false);
                }
            }
        }

        private void UpdateSlotsDisplay()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;

            for (int i = 0; i < slots.Length; i++)
            {
                TeammateSlotUI slot = slots[i];
                if (slot == null || slot.root == null || !slot.root.activeSelf) continue;

                if (slot.boundPlayer != null)
                {
                    UpdateRealPlayerSlot(slot, i);
                }
                else if (slot.isSimulated)
                {
                    UpdateSimulatedSlot(slot, i);
                }
            }
        }

        private void UpdateRealPlayerSlot(TeammateSlotUI slot, int index)
        {
            PlayerDownState p = slot.boundPlayer;
            string displayName = index == 0 ? $"Bạn (P{index + 1})" : $"P{index + 1} ({p.name})";
            if (slot.nameText != null) slot.nameText.text = displayName;

            bool isCarrying = slot.boundCarrier != null && slot.boundCarrier.IsCarrying;
            if (slot.coreCarryIcon != null) slot.coreCarryIcon.gameObject.SetActive(isCarrying);

            bool matchWon = _matchFlow != null && _matchFlow.Phase == MatchPhase.Win;

            if (matchWon && !p.IsEliminated)
            {
                ApplySlotStatus(slot, "ĐÃ THOÁT", escapedColor);
                if (slot.healthFill != null) slot.healthFill.fillAmount = 1f;
            }
            else if (p.IsDowned)
            {
                float bleedout = p.BleedoutRemaining;
                bool flash = Mathf.PingPong(Time.time * 3.5f, 1f) > 0.3f;
                Color statusCol = flash ? downedColor : new Color(0.8f, 0.1f, 0.1f, 0.6f);

                ApplySlotStatus(slot, $"HẤP HỐI ({bleedout:F0}s)", statusCol);
                if (slot.healthFill != null)
                {
                    slot.healthFill.fillAmount = p.Bleedout01;
                    slot.healthFill.color = downedColor;
                }
            }
            else if (p.IsEliminated)
            {
                ApplySlotStatus(slot, "TỬ VONG", eliminatedColor);
                if (slot.healthFill != null)
                {
                    slot.healthFill.fillAmount = 0f;
                    slot.healthFill.color = eliminatedColor;
                }
            }
            else if (isCarrying)
            {
                ApplySlotStatus(slot, "VÁC CORE", carryingColor);
                if (slot.healthFill != null)
                {
                    slot.healthFill.fillAmount = Mathf.Clamp01(p.Health / 100f);
                    slot.healthFill.color = carryingColor;
                }
            }
            else
            {
                ApplySlotStatus(slot, "KHỎE MẠNH", healthyColor);
                if (slot.healthFill != null)
                {
                    slot.healthFill.fillAmount = Mathf.Clamp01(p.Health / 100f);
                    slot.healthFill.color = healthyColor;
                }
            }

            // Distance text
            if (slot.distanceText != null)
            {
                if (index == 0 || _mainCamera == null)
                {
                    slot.distanceText.gameObject.SetActive(false);
                }
                else
                {
                    slot.distanceText.gameObject.SetActive(true);
                    float dist = Vector3.Distance(_mainCamera.transform.position, p.transform.position);
                    slot.distanceText.text = $"{dist:F0}m";
                }
            }
        }

        private void UpdateSimulatedSlot(TeammateSlotUI slot, int index)
        {
            if (slot.nameText != null) slot.nameText.text = slot.simulatedName;

            bool matchWon = _matchFlow != null && _matchFlow.Phase == MatchPhase.Win;
            if (matchWon)
            {
                ApplySlotStatus(slot, "ĐÃ THOÁT", escapedColor);
                if (slot.healthFill != null) slot.healthFill.fillAmount = 1f;
                return;
            }

            switch (index)
            {
                case 1: // Teammate 2: Carrying Core
                    ApplySlotStatus(slot, "VÁC CORE", carryingColor);
                    if (slot.coreCarryIcon != null) slot.coreCarryIcon.gameObject.SetActive(true);
                    if (slot.healthFill != null)
                    {
                        slot.healthFill.fillAmount = 0.85f;
                        slot.healthFill.color = carryingColor;
                    }
                    if (slot.distanceText != null) { slot.distanceText.gameObject.SetActive(true); slot.distanceText.text = "14m"; }
                    break;

                case 2: // Teammate 3: Healthy
                    ApplySlotStatus(slot, "KHỎE MẠNH", healthyColor);
                    if (slot.coreCarryIcon != null) slot.coreCarryIcon.gameObject.SetActive(false);
                    if (slot.healthFill != null)
                    {
                        slot.healthFill.fillAmount = 1.0f;
                        slot.healthFill.color = healthyColor;
                    }
                    if (slot.distanceText != null) { slot.distanceText.gameObject.SetActive(true); slot.distanceText.text = "22m"; }
                    break;

                case 3: // Teammate 4: Healthy
                    ApplySlotStatus(slot, "KHỎE MẠNH", healthyColor);
                    if (slot.coreCarryIcon != null) slot.coreCarryIcon.gameObject.SetActive(false);
                    if (slot.healthFill != null)
                    {
                        slot.healthFill.fillAmount = 0.95f;
                        slot.healthFill.color = healthyColor;
                    }
                    if (slot.distanceText != null) { slot.distanceText.gameObject.SetActive(true); slot.distanceText.text = "31m"; }
                    break;
            }
        }

        private void ApplySlotStatus(TeammateSlotUI slot, string status, Color color)
        {
            if (slot.statusText != null)
            {
                slot.statusText.text = status;
                slot.statusText.color = color;
            }

            if (slot.statusBadgeBg != null)
            {
                slot.statusBadgeBg.color = new Color(color.r, color.g, color.b, 0.2f);
            }

            if (slot.accentBar != null)
            {
                slot.accentBar.color = color;
            }
        }
    }
}
