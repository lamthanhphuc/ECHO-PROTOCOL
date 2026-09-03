using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerEnergyCoreCarrier : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Transform dropOrigin;

    [Header("Carry Penalty")]
    [SerializeField] private float carrySpeedMultiplier = 0.72f;
    [SerializeField] private bool blockSprintWhileCarrying = false;
    [SerializeField] private bool lockTeamToolWhileCarrying = true;

    [Header("Drop")]
    [SerializeField] private float dropForwardDistance = 1.35f;
    [SerializeField] private float dropHeightOffset = -0.35f;

    [Header("Noise")]
    [SerializeField] private float carryNoiseInterval = 2.5f;
    [SerializeField] private float carryNoiseMoveThreshold = 0.05f;
    [SerializeField] private UnityEvent carryNoiseEmitted;

    private InputAction _dropAction;
    private EnergyCorePickup _carriedCore;
    private InventoryItemDefinition _carriedCoreItem;
    private Vector3 _lastPosition;
    private float _noiseTimer;

    public event Action<PlayerEnergyCoreCarrier> CarryStateChanged;
    public event Action<PlayerEnergyCoreCarrier> CarryNoiseEmitted;

    public bool IsCarrying => _carriedCore != null;
    public EnergyCorePickup CarriedCore => _carriedCore;
    public InventoryItemDefinition CarriedCoreItem => _carriedCoreItem;

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }

        if (movement == null)
        {
            movement = GetComponent<PlayerMovement>();
        }

        if (dropOrigin == null && Camera.main != null)
        {
            dropOrigin = Camera.main.transform;
        }

        _lastPosition = transform.position;
        BindInput();
    }

    private void OnEnable()
    {
        if (_dropAction == null)
        {
            BindInput();
        }

        if (_dropAction != null)
        {
            _dropAction.performed += OnDropPerformed;
            _dropAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (_dropAction != null)
        {
            _dropAction.performed -= OnDropPerformed;
            _dropAction.Disable();
        }
    }

    private void Update()
    {
        if (IsCarrying && _dropAction == null && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            DropCore();
            return;
        }

        UpdateCarryNoise();
    }

    public bool CanPickupCore(EnergyCorePickup core)
    {
        if (core == null || IsCarrying || inventory == null || core.CoreItem == null)
        {
            return false;
        }

        return inventory.CanAdd(core.CoreItem);
    }

    public bool TryPickupCore(EnergyCorePickup core)
    {
        if (!CanPickupCore(core) || !inventory.TryAdd(core.CoreItem))
        {
            return false;
        }

        _carriedCore = core;
        _carriedCoreItem = core.CoreItem;
        _noiseTimer = carryNoiseInterval;
        _lastPosition = transform.position;

        core.SetCarried(true);
        ApplyCarryState(true);
        CarryStateChanged?.Invoke(this);
        return true;
    }

    public bool DropCore()
    {
        if (!IsCarrying)
        {
            return false;
        }

        EnergyCorePickup droppedCore = _carriedCore;
        InventoryItemDefinition droppedItem = _carriedCoreItem;

        ClearCarriedCoreState(removeFromInventory: true);
        droppedCore.PlaceInWorld(GetDropPosition(), GetDropRotation());

        if (inventory != null && droppedItem != null && inventory.Contains(droppedItem))
        {
            inventory.TryRemove(droppedItem);
        }

        return true;
    }

    public bool PlaceCoreInSectorBox(SectorBox sectorBox)
    {
        if (!IsCarrying || sectorBox == null || !sectorBox.CanAcceptCore(this))
        {
            return false;
        }

        EnergyCorePickup placedCore = _carriedCore;
        InventoryItemDefinition placedItem = _carriedCoreItem;

        ClearCarriedCoreState(removeFromInventory: true);
        if (inventory != null && placedItem != null && inventory.Contains(placedItem))
        {
            inventory.TryRemove(placedItem);
        }

        if (placedCore != null)
        {
            Destroy(placedCore.gameObject);
        }

        sectorBox.AcceptPlacedCore();
        return true;
    }

    private void ClearCarriedCoreState(bool removeFromInventory)
    {
        InventoryItemDefinition item = _carriedCoreItem;
        _carriedCore = null;
        _carriedCoreItem = null;
        ApplyCarryState(false);

        if (removeFromInventory && inventory != null && item != null && inventory.Contains(item))
        {
            inventory.TryRemove(item);
        }

        CarryStateChanged?.Invoke(this);
    }

    private void ApplyCarryState(bool carrying)
    {
        if (movement != null)
        {
            movement.SetExternalSpeedMultiplier(carrying ? carrySpeedMultiplier : 1f);
        }

        if (inventory != null && lockTeamToolWhileCarrying)
        {
            inventory.SetTeamToolLocked(carrying);
        }
    }

    private void UpdateCarryNoise()
    {
        if (!IsCarrying)
        {
            _lastPosition = transform.position;
            return;
        }

        float movedDistance = Vector3.Distance(transform.position, _lastPosition);
        _lastPosition = transform.position;

        if (movedDistance <= carryNoiseMoveThreshold)
        {
            return;
        }

        _noiseTimer -= Time.deltaTime;
        if (_noiseTimer > 0f)
        {
            return;
        }

        _noiseTimer = carryNoiseInterval;
        carryNoiseEmitted?.Invoke();
        CarryNoiseEmitted?.Invoke(this);
    }

    private void BindInput()
    {
        if (inputActions == null)
        {
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap("Player", false);
        _dropAction = playerMap?.FindAction("Drop", false);
    }

    private void OnDropPerformed(InputAction.CallbackContext context)
    {
        DropCore();
    }

    private Vector3 GetDropPosition()
    {
        Transform origin = dropOrigin != null ? dropOrigin : transform;
        return origin.position + origin.forward * dropForwardDistance + Vector3.up * dropHeightOffset;
    }

    private Quaternion GetDropRotation()
    {
        Transform origin = dropOrigin != null ? dropOrigin : transform;
        return Quaternion.LookRotation(origin.forward, Vector3.up);
    }
}
