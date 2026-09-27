using System.Collections.Generic;
using Fusion;
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
    private static readonly Dictionary<NetworkRunner, Dictionary<ulong, PlayerRef>>
        NetworkReservations = new();

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

        if (IsOccupiedByAnotherNetworkPlayer(hidingController))
        {
            return false;
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

        if (IsOccupiedByAnotherNetworkPlayer(hidingController))
        {
            return;
        }

        hidingController.EnterHiding(this);
    }

    public bool TryOccupy(PlayerHidingController hidingController)
    {
        if (hidingController == null)
        {
            return false;
        }

        if (IsOccupiedByAnotherNetworkPlayer(hidingController))
        {
            return false;
        }

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

    public static bool TryResolveByStableId(
        ulong stableId,
        out HidingSpot spot)
    {
        spot = null;

        if (stableId == 0UL)
        {
            return false;
        }

        var spots =
            Object.FindObjectsByType<HidingSpot>(
                FindObjectsInactive.Exclude);

        for (int i = 0; i < spots.Length; i++)
        {
            HidingSpot candidate = spots[i];

            if (candidate == null ||
                candidate.StableId != stableId)
            {
                continue;
            }

            spot = candidate;
            return true;
        }

        return false;
    }

    public static bool TryReserveNetworkSpot(
        NetworkRunner runner,
        ulong stableId,
        PlayerRef player)
    {
        EchoProtocol.Diagnostics.RuntimeLog.Log(
            EchoProtocol.Diagnostics.RuntimeLogCategory.StalkerHideFlow,
            $"[HIDE_RESERVE][REQUEST] spot={stableId} player={player}");

        if (runner == null ||
            stableId == 0UL ||
            !player.IsRealPlayer)
        {
            return false;
        }

        if (!NetworkReservations.TryGetValue(
                runner,
                out var reservations))
        {
            reservations = new Dictionary<ulong, PlayerRef>();
            NetworkReservations.Add(runner, reservations);
        }

        if (reservations.TryGetValue(
                stableId,
                out PlayerRef currentOccupant))
        {
            if (currentOccupant == player)
            {
                return true;
            }

            if (IsPlayerActive(runner, currentOccupant))
            {
                EchoProtocol.Diagnostics.RuntimeLog.Log(
                    EchoProtocol.Diagnostics.RuntimeLogCategory.StalkerHideFlow,
                    $"[HIDE_RESERVE][REJECT] spot={stableId} owner={currentOccupant} requester={player}");
                return false;
            }

            reservations.Remove(stableId);
        }

        reservations[stableId] = player;
        EchoProtocol.Diagnostics.RuntimeLog.Log(
            EchoProtocol.Diagnostics.RuntimeLogCategory.StalkerHideFlow,
            $"[HIDE_RESERVE][CLAIM] spot={stableId} player={player}");
        return true;
    }

    public static void ReleaseNetworkSpot(
        NetworkRunner runner,
        ulong stableId,
        PlayerRef player)
    {
        if (runner == null ||
            stableId == 0UL ||
            !NetworkReservations.TryGetValue(
                runner,
                out var reservations))
        {
            return;
        }

        if (!reservations.TryGetValue(
                stableId,
                out PlayerRef occupant) ||
            occupant != player)
        {
            return;
        }

        reservations.Remove(stableId);

        if (reservations.Count == 0)
        {
            NetworkReservations.Remove(runner);
        }
    }

    public static void ReleaseAllNetworkSpots(
        NetworkRunner runner,
        PlayerRef player)
    {
        if (runner == null ||
            !player.IsRealPlayer ||
            !NetworkReservations.TryGetValue(
                runner,
                out var reservations))
        {
            return;
        }

        var spotsToRelease = new List<ulong>();

        foreach (var pair in reservations)
        {
            if (pair.Value == player)
            {
                spotsToRelease.Add(pair.Key);
            }
        }

        for (int i = 0; i < spotsToRelease.Count; i++)
        {
            reservations.Remove(spotsToRelease[i]);
        }

        if (reservations.Count == 0)
        {
            NetworkReservations.Remove(runner);
        }
    }

    private static bool IsPlayerActive(
        NetworkRunner runner,
        PlayerRef player)
    {
        if (runner == null ||
            !player.IsRealPlayer)
        {
            return false;
        }

        foreach (PlayerRef activePlayer in runner.ActivePlayers)
        {
            if (activePlayer == player)
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

        hash = HashString(hash, gameObject.scene.name);
        hash = HashTransformHierarchyStable(hash, transform);

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

    private static ulong HashTransformHierarchyStable(
        ulong hash,
        Transform current)
    {
        if (current == null)
        {
            return hash;
        }

        if (current.parent != null)
        {
            hash = HashTransformHierarchyStable(hash, current.parent);
        }

        hash = HashString(hash, current.name);

        Vector3 position = current.localPosition;

        hash = HashInt(
            hash,
            Mathf.RoundToInt(position.x * 100f));
        hash = HashInt(
            hash,
            Mathf.RoundToInt(position.y * 100f));
        hash = HashInt(
            hash,
            Mathf.RoundToInt(position.z * 100f));

        return hash;
    }

    private bool IsOccupiedByAnotherNetworkPlayer(
        PlayerHidingController requester)
    {
        var requesterMovement =
            requester != null
                ? requester.GetComponent<EchoProtocol.Networking.NetworkPlayerMovement>()
                : null;

        var players =
            Object.FindObjectsByType<EchoProtocol.Networking.NetworkPlayerMovement>(
                FindObjectsInactive.Exclude);

        for (int i = 0; i < players.Length; i++)
        {
            var movement = players[i];

            if (movement == null || movement == requesterMovement)
            {
                continue;
            }

            if (movement.Object == null || !movement.Object.IsValid)
            {
                continue;
            }

            if (movement.IsHidden && movement.CurrentHideSpotId == StableId)
            {
                return true;
            }
        }

        return false;
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
