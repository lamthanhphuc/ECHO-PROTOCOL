using System;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public const int NormalSlotCount = 2;

    [SerializeField] private InventoryItemDefinition[] normalSlots = new InventoryItemDefinition[NormalSlotCount];
    [SerializeField] private InventoryItemDefinition teamToolSlot;
    [SerializeField] private bool teamToolLocked;

    public event Action InventoryChanged;

    public InventoryItemDefinition TeamToolSlot => teamToolSlot;
    public bool IsTeamToolLocked => teamToolLocked;

    private void OnValidate()
    {
        if (normalSlots == null || normalSlots.Length != NormalSlotCount)
        {
            InventoryItemDefinition[] resizedSlots = new InventoryItemDefinition[NormalSlotCount];
            if (normalSlots != null)
            {
                Array.Copy(normalSlots, resizedSlots, Mathf.Min(normalSlots.Length, resizedSlots.Length));
            }

            normalSlots = resizedSlots;
        }
    }

    public InventoryItemDefinition GetNormalSlot(int index)
    {
        return IsNormalSlotIndexValid(index) ? normalSlots[index] : null;
    }

    public bool CanAdd(InventoryItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        if (item.ItemType == InventoryItemType.TeamTool)
        {
            return teamToolSlot == null;
        }

        if (item.ItemType == InventoryItemType.EnergyCore)
        {
            return AreAllNormalSlotsEmpty();
        }

        return GetFirstEmptyNormalSlotIndex() >= 0;
    }

    public bool TryAdd(InventoryItemDefinition item)
    {
        if (!CanAdd(item))
        {
            return false;
        }

        if (item.ItemType == InventoryItemType.TeamTool)
        {
            teamToolSlot = item;
            InventoryChanged?.Invoke();
            return true;
        }

        if (item.ItemType == InventoryItemType.EnergyCore)
        {
            for (int i = 0; i < normalSlots.Length; i++)
            {
                normalSlots[i] = item;
            }

            InventoryChanged?.Invoke();
            return true;
        }

        int slotIndex = GetFirstEmptyNormalSlotIndex();
        normalSlots[slotIndex] = item;
        InventoryChanged?.Invoke();
        return true;
    }

    public bool Contains(InventoryItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        if (teamToolSlot == item)
        {
            return true;
        }

        return FindNormalSlotIndex(item) >= 0;
    }

    public bool TryRemove(InventoryItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        if (teamToolSlot == item)
        {
            teamToolSlot = null;
            InventoryChanged?.Invoke();
            return true;
        }

        if (item.ItemType == InventoryItemType.EnergyCore)
        {
            bool removedCore = false;
            for (int i = 0; i < normalSlots.Length; i++)
            {
                if (normalSlots[i] == item)
                {
                    normalSlots[i] = null;
                    removedCore = true;
                }
            }

            if (removedCore)
            {
                InventoryChanged?.Invoke();
            }

            return removedCore;
        }

        int slotIndex = FindNormalSlotIndex(item);
        if (slotIndex < 0)
        {
            return false;
        }

        normalSlots[slotIndex] = null;
        InventoryChanged?.Invoke();
        return true;
    }

    public bool TryDropNormalSlot(int index, Vector3 position, Quaternion rotation)
    {
        if (!IsNormalSlotIndexValid(index)
            || normalSlots[index] == null
            || normalSlots[index].ItemType == InventoryItemType.EnergyCore)
        {
            return false;
        }

        InventoryItemDefinition item = normalSlots[index];
        if (!SpawnWorldItem(item, position, rotation))
        {
            return false;
        }

        normalSlots[index] = null;
        InventoryChanged?.Invoke();
        return true;
    }

    public bool TryDropTeamTool(Vector3 position, Quaternion rotation)
    {
        if (teamToolLocked || teamToolSlot == null)
        {
            return false;
        }

        InventoryItemDefinition item = teamToolSlot;
        if (!SpawnWorldItem(item, position, rotation))
        {
            return false;
        }

        teamToolSlot = null;
        InventoryChanged?.Invoke();
        return true;
    }

    public void SetTeamToolLocked(bool locked)
    {
        if (teamToolLocked == locked)
        {
            return;
        }

        teamToolLocked = locked;
        InventoryChanged?.Invoke();
    }

    private int GetFirstEmptyNormalSlotIndex()
    {
        for (int i = 0; i < normalSlots.Length; i++)
        {
            if (normalSlots[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private bool AreAllNormalSlotsEmpty()
    {
        if (normalSlots == null || normalSlots.Length != NormalSlotCount)
        {
            return false;
        }

        for (int i = 0; i < normalSlots.Length; i++)
        {
            if (normalSlots[i] != null)
            {
                return false;
            }
        }

        return true;
    }

    private int FindNormalSlotIndex(InventoryItemDefinition item)
    {
        if (normalSlots == null || item == null)
        {
            return -1;
        }

        for (int i = 0; i < normalSlots.Length; i++)
        {
            if (normalSlots[i] == item)
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsNormalSlotIndexValid(int index)
    {
        return normalSlots != null && index >= 0 && index < normalSlots.Length;
    }

    private static bool SpawnWorldItem(InventoryItemDefinition item, Vector3 position, Quaternion rotation)
    {
        if (item == null || item.WorldPrefab == null)
        {
            return false;
        }

        Instantiate(item.WorldPrefab, position, rotation);
        return true;
    }
}
