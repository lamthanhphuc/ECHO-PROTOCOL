using UnityEngine;

public class HidingSpot : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform hidePoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private string enterPrompt = "Hide";
    [SerializeField] private string exitPrompt = "Exit hiding";
    [SerializeField] private float yawLimitDegrees = 45f;

    private PlayerHidingController _occupant;
    private ulong _stableId;

    public Transform HidePoint => hidePoint != null ? hidePoint : transform;
    public Transform ExitPoint => exitPoint;
    public ulong StableId
    {
        get
        {
            if (_stableId == 0UL)
            {
                _stableId = ComputeStableId();
            }

            return _stableId;
        }
    }

    public float YawLimitDegrees => yawLimitDegrees;
    public bool IsOccupied => _occupant != null;
    public string InteractionPrompt => IsOccupied ? exitPrompt : enterPrompt;

    public bool CanInteract(GameObject interactor)
    {
        PlayerHidingController hidingController = GetHidingController(interactor);
        return hidingController != null && (_occupant == null || _occupant == hidingController);
    }

    public void Interact(GameObject interactor)
    {
        PlayerHidingController hidingController = GetHidingController(interactor);
        if (hidingController == null)
        {
            return;
        }

        if (_occupant == hidingController)
        {
            hidingController.ExitHiding();
            return;
        }

        hidingController.EnterHiding(this);
    }

    public bool TryOccupy(PlayerHidingController hidingController)
    {
        if (_occupant != null && _occupant != hidingController)
        {
            return false;
        }

        _occupant = hidingController;
        return true;
    }

    public void Release(PlayerHidingController hidingController)
    {
        if (_occupant == hidingController)
        {
            _occupant = null;
        }
    }

    public bool TryInspectOccupant(out PlayerHidingController occupant)
    {
        occupant = _occupant;
        return occupant != null;
    }

    private ulong ComputeStableId()
    {
        const ulong offsetBasis = 14695981039346656037UL;

        ulong hash = offsetBasis;

        string sceneIdentity = !string.IsNullOrEmpty(gameObject.scene.path)
            ? gameObject.scene.path
            : gameObject.scene.name;

        hash = HashString(hash, sceneIdentity);
        hash = HashTransformHierarchy(hash, transform);

        var hidingSpots = GetComponents<HidingSpot>();
        int componentIndex = 0;

        for (int i = 0; i < hidingSpots.Length; i++)
        {
            if (hidingSpots[i] == this)
            {
                componentIndex = i;
                break;
            }
        }

        hash = HashInt(hash, componentIndex);

        return hash == 0UL ? 1UL : hash;
    }

    private static ulong HashTransformHierarchy(
        ulong hash,
        Transform current)
    {
        if (current == null)
        {
            return hash;
        }

        if (current.parent != null)
        {
            hash = HashTransformHierarchy(hash, current.parent);
        }

        hash = HashInt(hash, current.GetSiblingIndex());
        hash = HashString(hash, current.name);

        return hash;
    }

    private static ulong HashString(
        ulong hash,
        string value)
    {
        const ulong prime = 1099511628211UL;

        if (string.IsNullOrEmpty(value))
        {
            hash ^= 0UL;
            return hash * prime;
        }

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            hash ^= (byte)(character & 0xFF);
            hash *= prime;

            hash ^= (byte)(character >> 8);
            hash *= prime;
        }

        return hash;
    }

    private static ulong HashInt(
        ulong hash,
        int value)
    {
        const ulong prime = 1099511628211UL;

        unchecked
        {
            uint bits = (uint)value;

            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(bits >> shift);
                hash *= prime;
            }
        }

        return hash;
    }

    private static PlayerHidingController GetHidingController(GameObject interactor)
    {
        return interactor != null ? interactor.GetComponentInParent<PlayerHidingController>() : null;
    }
}
