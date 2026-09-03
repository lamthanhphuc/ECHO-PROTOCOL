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

        public void BindInventory(PlayerInventory playerInv, PlayerEnergyCoreCarrier coreCarrier)
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= RefreshSlots;
            }

            inventory = playerInv;
            carrier = coreCarrier;

            if (inventory != null)
            {
                inventory.InventoryChanged += RefreshSlots;
            }

            RefreshSlots();
        }

        private void OnDestroy()
        {
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
            if (inventory == null || carrier == null)
            {
                ResolveReferences();
            }

            UpdateCooldown();
            UpdateCarryState();
        }

        private void ResolveReferences()
        {
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

        public void TriggerToolCooldown(float durationSeconds)
        {
            _cooldownDuration = Mathf.Max(0.1f, durationSeconds);
            _cooldownTimer = _cooldownDuration;
        }

        private void UpdateCooldown()
        {
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
            bool isCarrying = carrier != null && carrier.IsCarrying;

            if (slot1LockOverlay != null) slot1LockOverlay.SetActive(isCarrying);
            if (slot2LockOverlay != null) slot2LockOverlay.SetActive(isCarrying);

            if (isCarrying)
            {
                if (slot1LockText != null) slot1LockText.text = "VÁC CORE";
                if (slot2LockText != null) slot2LockText.text = "VÁC CORE";
            }

            if (toolLockedOverlay != null)
            {
                bool toolLocked = (inventory != null && inventory.IsTeamToolLocked) || isCarrying;
                toolLockedOverlay.SetActive(toolLocked);
            }
        }

        public void RefreshSlots()
        {
            if (inventory == null)
            {
                UpdateSlotView(null, slot1Icon, slot1NameText, "1: Trống");
                UpdateSlotView(null, slot2Icon, slot2NameText, "2: Trống");
                UpdateSlotView(null, toolIcon, toolNameText, "Tool: Trống");
                return;
            }

            // Slot 1
            InventoryItemDefinition item1 = inventory.GetNormalSlot(0);
            UpdateSlotView(item1, slot1Icon, slot1NameText, "1: Trống");

            // Slot 2
            InventoryItemDefinition item2 = inventory.GetNormalSlot(1);
            UpdateSlotView(item2, slot2Icon, slot2NameText, "2: Trống");

            // Team Tool Slot
            InventoryItemDefinition toolItem = inventory.TeamToolSlot;
            UpdateSlotView(toolItem, toolIcon, toolNameText, "Tool: Trống");
        }

        private void UpdateSlotView(InventoryItemDefinition item, Image icon, Text nameLabel, string emptyLabel)
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
                        icon.sprite = HUDTextureUtility.CircleFilled;
                        icon.color = item.ItemType == InventoryItemType.EnergyCore
                            ? new Color(0f, 0.9f, 1f, 0.9f)
                            : new Color(0.2f, 0.8f, 0.5f, 0.9f);
                    }
                }

                if (nameLabel != null)
                {
                    nameLabel.text = item.DisplayName;
                }
            }
            else
            {
                if (icon != null) icon.gameObject.SetActive(false);
                if (nameLabel != null) nameLabel.text = $"<color=#78909C>{emptyLabel}</color>";
            }
        }
    }
}
