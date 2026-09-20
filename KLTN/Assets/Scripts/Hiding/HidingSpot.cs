using UnityEngine;

public class HidingSpot : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform hidePoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private Transform inspectPoint;
    [SerializeField] private string enterPrompt = "Hide";
    [SerializeField] private string exitPrompt = "Exit hiding";
    [SerializeField] private float yawLimitDegrees = 45f;

    private PlayerHidingController _occupant;
    private ulong _stableId;
    private float _lockoutUntilTime;

    public Transform HidePoint => hidePoint != null ? hidePoint : transform;
    public Transform ExitPoint => exitPoint;
    public Transform InspectPoint => inspectPoint;
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
    public bool IsTemporarilyLockedOut() =>
        Time.time < _lockoutUntilTime;

    public float RemainingLockoutSeconds =>
        Mathf.Max(0f, _lockoutUntilTime - Time.time);

    public string InteractionPrompt =>
        IsTemporarilyLockedOut() && !IsOccupied
            ? string.Empty
            : IsOccupied
                ? exitPrompt
                : enterPrompt;

    public bool CanInteract(GameObject interactor)
    {
        PlayerHidingController hidingController =
            GetHidingController(interactor);

        if (hidingController == null)
        {
            return false;
        }

        // An existing occupant must always be allowed to exit.
        if (_occupant == hidingController)
        {
            return true;
        }

        if (IsTemporarilyLockedOut())
        {
            return false;
        }

        return _occupant == null;
    }

    public void Interact(GameObject interactor)
    {
        PlayerHidingController hidingController =
            GetHidingController(interactor);

        if (hidingController == null)
        {
            return;
        }

        if (_occupant == hidingController)
        {
            hidingController.ExitHiding();
            return;
        }

        if (IsTemporarilyLockedOut())
        {
            return;
        }

        hidingController.EnterHiding(this);
    }

    public bool TryOccupy(PlayerHidingController hidingController)
    {
        if (IsTemporarilyLockedOut())
        {
            return false;
        }

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

    public void BeginTemporaryLockout(float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return;
        }

        float requestedUntil =
            Time.time + durationSeconds;

        if (requestedUntil > _lockoutUntilTime)
        {
            _lockoutUntilTime = requestedUntil;
        }

        EchoProtocol.Diagnostics.RuntimeLog.Log(
            EchoProtocol.Diagnostics.RuntimeLogCategory.StalkerHideFlow,
            $"[STK_HIDE_FLOW][LOCKOUT_START] " +
            $"stableId={StableId} " +
            $"duration={durationSeconds:F2} " +
            $"remaining={RemainingLockoutSeconds:F2}",
            this);
    }

    public static bool BeginTemporaryLockoutByStableId(
        ulong stableId,
        float durationSeconds)
    {
        if (stableId == 0UL || durationSeconds <= 0f)
        {
            return false;
        }

        var spots =
            Object.FindObjectsByType<HidingSpot>(
                FindObjectsInactive.Exclude);

        bool found = false;

        for (int i = 0; i < spots.Length; i++)
        {
            HidingSpot spot = spots[i];

            if (spot == null || spot.StableId != stableId)
            {
                continue;
            }

            spot.BeginTemporaryLockout(durationSeconds);
            found = true;
        }

        return found;
    }

    public static bool IsTemporarilyLockedOut(
        ulong stableId)
    {
        if (stableId == 0UL)
        {
            return false;
        }

        var spots =
            Object.FindObjectsByType<HidingSpot>(
                FindObjectsInactive.Exclude);

        for (int i = 0; i < spots.Length; i++)
        {
            HidingSpot spot = spots[i];

            if (spot != null
                && spot.StableId == stableId
                && spot.IsTemporarilyLockedOut())
            {
                return true;
            }
        }

        return false;
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
