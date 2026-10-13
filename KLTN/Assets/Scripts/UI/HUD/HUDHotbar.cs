using EchoProtocol.Networking;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    public class HUDHotbar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerEnergyCoreCarrier carrier;

        [Header("Normal Slot 1")]
        [SerializeField] private GameObject slot1Container;
        [SerializeField] private Image slot1Icon;
        [SerializeField] private Text slot1NameText;
        [SerializeField] private GameObject slot1LockOverlay;
        [SerializeField] private Text slot1LockText;

        [Header("Normal Slot 2")]
        [SerializeField] private GameObject slot2Container;
        [SerializeField] private Image slot2Icon;
        [SerializeField] private Text slot2NameText;
        [SerializeField] private GameObject slot2LockOverlay;
        [SerializeField] private Text slot2LockText;

        [Header("Team Tool Slot")]
        [SerializeField] private GameObject toolContainer;
        [SerializeField] private Image toolIcon;
        [SerializeField] private Text toolNameText;
        [SerializeField] private Image toolCooldownRadial;
        [SerializeField] private Text toolCooldownText;
        [SerializeField] private GameObject toolLockedOverlay;

        private float _cooldownDuration;
        private float _cooldownTimer;
        private LobbyPlayerState _boundNetworkPlayerState;
        private Text _usesLabel;
        private Text _carryLabel;

        private void OnEnable()
        {
            LobbyPlayerState.AnyStateChanged += RefreshSlots;
        }

        private void OnDisable()
        {
            LobbyPlayerState.AnyStateChanged -= RefreshSlots;
        }

        public void BindInventory(PlayerInventory playerInv, PlayerEnergyCoreCarrier coreCarrier)
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= RefreshSlots;
            }

            inventory = playerInv;
            carrier = coreCarrier;
            _boundNetworkPlayerState = playerInv != null
                ? playerInv.GetComponentInParent<LobbyPlayerState>()
                : coreCarrier != null
                    ? coreCarrier.GetComponentInParent<LobbyPlayerState>()
                    : null;

            if (inventory != null)
            {
                inventory.InventoryChanged += RefreshSlots;
            }

            RefreshSlots();
        }

        private void OnDestroy()
        {
            if (_carryLabel != null) Destroy(_carryLabel.gameObject);
            if (inventory != null)
            {
                inventory.InventoryChanged -= RefreshSlots;
            }
        }

        private void Start()
        {
            ResolveReferences();
            RefreshSlots();
        }

        private void Update()
        {
            if (inventory == null
                || carrier == null
                || (IsActiveFusionSession() && !IsValidLocalNetworkPlayer(_boundNetworkPlayerState)))
            {
                ResolveReferences();
            }

            UpdateCooldown();
            UpdateCarryState();
        }

        private void ResolveReferences()
        {
            var playerStates = FindObjectsByType<LobbyPlayerState>(FindObjectsInactive.Exclude);
            for (var i = 0; i < playerStates.Length; i++)
            {
                var state = playerStates[i];
                if (IsValidLocalNetworkPlayer(state))
                {
                    BindInventory(
                        state.GetComponentInChildren<PlayerInventory>(true),
                        state.GetComponentInChildren<PlayerEnergyCoreCarrier>(true));
                    return;
                }
            }

            if (IsActiveFusionSession())
            {
                if (inventory != null || carrier != null || _boundNetworkPlayerState != null)
                {
                    BindInventory(null, null);
                }
                return;
            }

            _boundNetworkPlayerState = null;
            if (inventory == null)
            {
                inventory = FindAnyObjectByType<PlayerInventory>();
                if (inventory != null)
                {
                    inventory.InventoryChanged += RefreshSlots;
                }
            }

            if (carrier == null)
            {
                carrier = FindAnyObjectByType<PlayerEnergyCoreCarrier>();
            }
        }

        private static bool IsValidLocalNetworkPlayer(LobbyPlayerState state)
        {
            return state != null
                && state.Object != null
                && state.Object.IsValid
                && state.Runner != null
                && state.Runner.IsRunning
                && state.Object.HasInputAuthority
                && state.IsGameplayPlayer;
        }

        private static bool IsActiveFusionSession()
        {
            var runners = FindObjectsByType<NetworkRunner>(FindObjectsInactive.Exclude);
            for (var i = 0; i < runners.Length; i++)
            {
                if (runners[i] != null && runners[i].IsRunning)
                {
                    return true;
                }
            }

            return false;
        }

        public void TriggerToolCooldown(float durationSeconds)
        {
            _cooldownDuration = Mathf.Max(0.1f, durationSeconds);
            _cooldownTimer = _cooldownDuration;
        }

        private void UpdateCooldown()
        {
            var interactor = inventory != null ? inventory.GetComponent<NetworkPlayerInteractor>() : null;
            if (interactor != null && PlayerInventory.ResolveToolId(inventory.TeamToolSlot) == LobbyPlayerState.CoreStabilizerToolId)
            {
                _cooldownDuration = CoreStabilizerRules.CooldownSeconds;
                _cooldownTimer = interactor.GetCoreStabilizerCooldownRemaining();
            }

            if (_cooldownTimer > 0f)
            {
                _cooldownTimer = Mathf.Max(0f, _cooldownTimer - Time.deltaTime);
                float progress = _cooldownTimer / _cooldownDuration;

                if (toolCooldownRadial != null)
                {
                    toolCooldownRadial.gameObject.SetActive(true);
                    toolCooldownRadial.fillAmount = progress;
                }

                if (toolCooldownText != null)
                {
                    toolCooldownText.gameObject.SetActive(true);
                    toolCooldownText.text = _cooldownTimer > 1f ? $"{_cooldownTimer:F0}s" : $"{_cooldownTimer:F1}s";
                }
            }
            else
            {
                if (toolCooldownRadial != null && toolCooldownRadial.gameObject.activeSelf)
                {
                    toolCooldownRadial.gameObject.SetActive(false);
                }

                if (toolCooldownText != null && toolCooldownText.gameObject.activeSelf)
                {
                    toolCooldownText.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateCarryState()
        {
            var fuelCell = inventory != null ? EchoProtocol.MatchFlow.Zone3FuelCell.FindCarried(inventory.gameObject) : null;
            bool isCarrying = carrier != null && carrier.IsCarrying || fuelCell != null;

            if (slot1LockOverlay != null) slot1LockOverlay.SetActive(isCarrying);
            if (slot2LockOverlay != null) slot2LockOverlay.SetActive(isCarrying);

            if (isCarrying)
            {
                if (slot1LockText != null) slot1LockText.text = "×";
                if (slot2LockText != null) slot2LockText.text = "×";
            }
            if (_carryLabel == null && slot1Container != null)
            {
                var go = new GameObject("CarriedItemLabel", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(slot1Container.transform.parent, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 8f);
                rect.sizeDelta = new Vector2(400f, 24f);
                _carryLabel = go.GetComponent<Text>();
                _carryLabel.font = slot1NameText != null ? slot1NameText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _carryLabel.fontSize = 14;
                _carryLabel.alignment = TextAnchor.MiddleCenter;
                _carryLabel.color = HUDPresentationStyle.Ink;
                _carryLabel.raycastTarget = false;
            }
            if (_carryLabel != null)
            {
                _carryLabel.gameObject.SetActive(isCarrying);
                _carryLabel.text = fuelCell != null
                    ? EchoProtocol.Settings.GameLanguage.Choose("Đang mang pin nhiên liệu", "Carrying a fuel cell")
                    : EchoProtocol.Settings.GameLanguage.Choose("Đang mang lõi năng lượng", "Carrying an energy core");
            }

            if (toolLockedOverlay != null)
            {
                bool toolLocked = (inventory != null && inventory.IsTeamToolLocked) || isCarrying;
                toolLockedOverlay.SetActive(toolLocked);
            }
        }

        public void RefreshSlots()
        {
            try
            {
                if (_usesLabel != null) _usesLabel.gameObject.SetActive(false);
                if (inventory == null)
                {
                    UpdateSlotView(null, slot1Icon, slot1NameText, "Trống");
                    UpdateSlotView(null, slot2Icon, slot2NameText, "Trống");
                }

                // Slot 1
                InventoryItemDefinition item1 =
                    inventory != null ? inventory.GetNormalSlot(0) : null;
                UpdateSlotView(item1, slot1Icon, slot1NameText, "Trống");

                // Slot 2
                InventoryItemDefinition item2 =
                    inventory != null ? inventory.GetNormalSlot(1) : null;
                UpdateSlotView(item2, slot2Icon, slot2NameText, "Trống");

                // Team Tool Slot
                InventoryItemDefinition toolItem =
                    inventory != null ? inventory.TeamToolSlot : null;
                int networkToolId = _boundNetworkPlayerState != null
                    ? _boundNetworkPlayerState.ToolId
                    : 0;
                var lifeState = inventory != null
                    ? inventory.GetComponent<NetworkPlayerLifeState>()
                    : _boundNetworkPlayerState != null
                        ? _boundNetworkPlayerState.GetComponent<NetworkPlayerLifeState>()
                        : null;
                bool godMode = lifeState != null && lifeState.DebugGodMode;

                UpdateSlotView(toolItem, toolIcon, toolNameText, "Trống", isTeamTool: true);

                if (godMode
                    && toolNameText != null
                    && networkToolId > 0)
                {
                    toolNameText.text =
                        "<color=#FFD54F>GOD</color> "
                        + DebugToolName(networkToolId)
                        + " <color=#00E5FF>∞</color>";
                }
                else if (toolItem != null
                         && toolNameText != null
                         && _boundNetworkPlayerState != null
                         && (networkToolId == LobbyPlayerState.NoiseMakerToolId
                             || networkToolId == LobbyPlayerState.DoorJammerToolId))
                {
                    toolNameText.text =
                        ShortName(toolItem.DisplayName);
                    if (_usesLabel == null && toolContainer != null)
                    {
                        var go = new GameObject("RemainingUses", typeof(RectTransform), typeof(Text));
                        go.transform.SetParent(toolContainer.transform, false);
                        var rect = go.GetComponent<RectTransform>();
                        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
                        rect.anchoredPosition = new Vector2(-6f, 0f);
                        rect.sizeDelta = new Vector2(26f, 18f);
                        _usesLabel = go.GetComponent<Text>();
                        _usesLabel.font = toolNameText.font;
                        _usesLabel.fontSize = 12;
                        _usesLabel.color = HUDPresentationStyle.Ink;
                        _usesLabel.alignment = TextAnchor.MiddleRight;
                        _usesLabel.raycastTarget = false;
                    }
                    if (_usesLabel != null)
                    {
                        _usesLabel.gameObject.SetActive(true);
                        _usesLabel.text = $"×{_boundNetworkPlayerState.TeamToolUsesRemaining}";
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HUDHotbar] RefreshSlots exception: {ex.Message}");
            }
        }

        private static string DebugToolName(int toolId)
        {
            switch (toolId)
            {
                case LobbyPlayerState.FieldScannerToolId:
                    return "Field Scanner";
                case LobbyPlayerState.NoiseMakerToolId:
                    return "Noise Maker";
                case LobbyPlayerState.FirstAidKitToolId:
                    return "First Aid Kit";
                case LobbyPlayerState.DoorJammerToolId:
                    return "Door Jammer";
                case LobbyPlayerState.CoreStabilizerToolId:
                    return "Core Stabilizer";
                default:
                    return "Team Tool";
            }
        }

        private void UpdateSlotView(InventoryItemDefinition item, Image icon, Text nameLabel, string emptyLabel, bool isTeamTool = false)
        {
            try
            {
                if (item != null)
                {
                    if (icon != null)
                    {
                        icon.gameObject.SetActive(true);
                        if (item.Icon != null)
                        {
                            icon.sprite = item.Icon;
                            icon.color = Color.white;
                        }
                        else
                        {
                            icon.sprite = HUDTextureUtility.InventoryFallback(item.ItemType == InventoryItemType.EnergyCore);
                            icon.color = item.ItemType == InventoryItemType.EnergyCore
                                ? HUDPresentationStyle.Accent
                                : HUDPresentationStyle.Muted;
                        }
                    }

                    if (nameLabel != null)
                    {
                        nameLabel.text = ShortName(item.DisplayName);
                    }
                }
                else
                {
                    if (icon != null) icon.gameObject.SetActive(false);
                    if (nameLabel != null) nameLabel.text = $"<color=#9AA5A3>{emptyLabel}</color>";
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HUDHotbar] UpdateSlotView exception: {ex.Message}");
            }
        }

        private static string ShortName(string value)
        {
            return string.IsNullOrEmpty(value) || value.Length <= 24 ? value : value.Substring(0, 23).TrimEnd() + "…";
        }
    }
}
