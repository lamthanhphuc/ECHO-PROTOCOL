using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
            public TMP_Text nameTmp;
            public Image statusBadgeBg;
            public Text statusText;
            public TMP_Text statusTmp;
            public Image coreCarryIcon;
            public Image healthFill;
            public Text distanceText;
            public TMP_Text distanceTmp;

            [HideInInspector] public PlayerDownState boundPlayer;
            [HideInInspector] public PlayerEnergyCoreCarrier boundCarrier;
            [HideInInspector] public string simulatedName;
            [HideInInspector] public bool isSimulated;

            public void ResolveComponents()
            {
                if (nameTmp == null && nameText != null) nameTmp = nameText.GetComponent<TMP_Text>();
                if (statusTmp == null && statusText != null) statusTmp = statusText.GetComponent<TMP_Text>();
                if (distanceTmp == null && distanceText != null) distanceTmp = distanceText.GetComponent<TMP_Text>();
            }
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
                    if (slots[i] != null)
                    {
                        slots[i].ResolveComponents();
                        if (slots[i].root != null)
                        {
                            slots[i].root.SetActive(false);
                        }
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
            PlayerDownState[] allDownStates = FindObjectsByType<PlayerDownState>();
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
                slots[i].ResolveComponents();

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
                    // Provide 3 simulated teammates if solo testing
                    slots[i].boundPlayer = null;
                    slots[i].boundCarrier = null;
                    slots[i].isSimulated = true;
                    slots[i].simulatedName = i switch
                    {
                        1 => "Operative 2 (Alex)",
                        2 => "Operative 3 (Kael)",
                        3 => "Operative 4 (Elena)",
                        _ => $"Operative {i + 1}"
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
            string displayName = index == 0 ? $"YOU (P{index + 1})" : $"P{index + 1} ({p.name})";
            SetSlotName(slot, displayName);

            bool isCarrying = slot.boundCarrier != null && slot.boundCarrier.IsCarrying;
            if (slot.coreCarryIcon != null) slot.coreCarryIcon.gameObject.SetActive(isCarrying);

            bool matchWon = _matchFlow != null && _matchFlow.Phase == MatchPhase.Win;

            if (matchWon && !p.IsEliminated)
            {
                ApplySlotStatus(slot, "EXTRACTED", escapedColor);
                if (slot.healthFill != null) slot.healthFill.fillAmount = 1f;
                ResetSlotScale(slot);
            }
            else if (p.IsDowned)
            {
                // Emergency Flash for Downed Teammate
                float bleedout = p.BleedoutRemaining;
                bool flash = Mathf.PingPong(Time.time * 6f, 1f) > 0.35f;
                Color statusCol = flash ? downedColor : new Color(0.5f, 0.05f, 0.05f, 0.8f);

                ApplySlotStatus(slot, $"DOWNED ({bleedout:F0}s)", statusCol);
                if (slot.healthFill != null)
                {
                    slot.healthFill.fillAmount = p.Bleedout01;
                    slot.healthFill.color = downedColor;
                }

                // Visual punch scale on emergency
                if (slot.root != null)
                {
                    float punch = 1f + (flash ? 0.035f : 0f);
                    slot.root.transform.localScale = new Vector3(punch, punch, 1f);
                }
            }
            else if (p.IsEliminated)
            {
                ApplySlotStatus(slot, "KIA", eliminatedColor);
                if (slot.healthFill != null)
                {
                    slot.healthFill.fillAmount = 0f;
                    slot.healthFill.color = eliminatedColor;
                }
                ResetSlotScale(slot);
            }
            else if (isCarrying)
            {
                ApplySlotStatus(slot, "CARRYING CORE", carryingColor);
                if (slot.healthFill != null)
                {
                    slot.healthFill.fillAmount = Mathf.Clamp01(p.Health / 100f);
                    slot.healthFill.color = carryingColor;
                }
                ResetSlotScale(slot);
            }
            else
            {
                if (p.Health < 50f)
                {
                    ApplySlotStatus(slot, "INJURED", new Color(1f, 0.7f, 0.1f, 1f));
                }
                else
                {
                    ApplySlotStatus(slot, "OPERATIONAL", healthyColor);
                }

                if (slot.healthFill != null)
                {
                    slot.healthFill.fillAmount = Mathf.Clamp01(p.Health / 100f);
                    slot.healthFill.color = p.Health < 50f ? new Color(1f, 0.7f, 0.1f, 1f) : healthyColor;
                }
                ResetSlotScale(slot);
            }

            // Distance text with danger warning formatting
            if (index == 0 || _mainCamera == null)
            {
                SetDistanceActive(slot, false);
            }
            else
            {
                SetDistanceActive(slot, true);
                float dist = Vector3.Distance(_mainCamera.transform.position, p.transform.position);
                string distStr = $"{dist:F0}m";
                if (p.IsDowned || dist > 35f)
                {
                    distStr = $"<color=#FFB300>{distStr}</color>";
                }
                SetDistanceText(slot, distStr);
            }
        }

        private void UpdateSimulatedSlot(TeammateSlotUI slot, int index)
        {
            SetSlotName(slot, slot.simulatedName);
            ResetSlotScale(slot);

            bool matchWon = _matchFlow != null && _matchFlow.Phase == MatchPhase.Win;
            if (matchWon)
            {
                ApplySlotStatus(slot, "EXTRACTED", escapedColor);
                if (slot.healthFill != null) slot.healthFill.fillAmount = 1f;
                return;
            }

            switch (index)
            {
                case 1: // Teammate 2: Carrying Core
                    ApplySlotStatus(slot, "CARRYING CORE", carryingColor);
                    if (slot.coreCarryIcon != null) slot.coreCarryIcon.gameObject.SetActive(true);
                    if (slot.healthFill != null)
                    {
                        slot.healthFill.fillAmount = 0.85f;
                        slot.healthFill.color = carryingColor;
                    }
                    SetDistanceActive(slot, true);
                    SetDistanceText(slot, "14m");
                    break;

                case 2: // Teammate 3: Operational
                    ApplySlotStatus(slot, "OPERATIONAL", healthyColor);
                    if (slot.coreCarryIcon != null) slot.coreCarryIcon.gameObject.SetActive(false);
                    if (slot.healthFill != null)
                    {
                        slot.healthFill.fillAmount = 1.0f;
                        slot.healthFill.color = healthyColor;
                    }
                    SetDistanceActive(slot, true);
                    SetDistanceText(slot, "22m");
                    break;

                case 3: // Teammate 4: Operational
                    ApplySlotStatus(slot, "OPERATIONAL", healthyColor);
                    if (slot.coreCarryIcon != null) slot.coreCarryIcon.gameObject.SetActive(false);
                    if (slot.healthFill != null)
                    {
                        slot.healthFill.fillAmount = 0.95f;
                        slot.healthFill.color = healthyColor;
                    }
                    SetDistanceActive(slot, true);
                    SetDistanceText(slot, "31m");
                    break;
            }
        }

        private void ApplySlotStatus(TeammateSlotUI slot, string status, Color color)
        {
            if (slot.statusTmp != null)
            {
                slot.statusTmp.text = status;
                slot.statusTmp.color = color;
            }
            if (slot.statusText != null)
            {
                slot.statusText.text = status;
                slot.statusText.color = color;
            }

            if (slot.statusBadgeBg != null)
            {
                slot.statusBadgeBg.color = new Color(color.r, color.g, color.b, 0.22f);
            }

            if (slot.accentBar != null)
            {
                slot.accentBar.color = color;
            }
        }

        private static void SetSlotName(TeammateSlotUI slot, string name)
        {
            if (slot.nameTmp != null) slot.nameTmp.text = name;
            if (slot.nameText != null) slot.nameText.text = name;
        }

        private static void SetDistanceText(TeammateSlotUI slot, string dist)
        {
            if (slot.distanceTmp != null) slot.distanceTmp.text = dist;
            if (slot.distanceText != null) slot.distanceText.text = dist;
        }

        private static void SetDistanceActive(TeammateSlotUI slot, bool active)
        {
            if (slot.distanceTmp != null) slot.distanceTmp.gameObject.SetActive(active);
            if (slot.distanceText != null) slot.distanceText.gameObject.SetActive(active);
        }

        private static void ResetSlotScale(TeammateSlotUI slot)
        {
            if (slot?.root != null && slot.root.transform.localScale != Vector3.one)
            {
                slot.root.transform.localScale = Vector3.one;
            }
        }
    }
}
