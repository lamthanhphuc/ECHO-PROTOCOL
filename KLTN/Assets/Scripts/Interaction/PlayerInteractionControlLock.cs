using System;
using System.Collections.Generic;
using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractionControlLock
{
    private static readonly List<PlayerInteractionControlLock> Locks = new List<PlayerInteractionControlLock>();
    private static CursorLockMode _previousCursorLock;
    private static bool _previousCursorVisible;
    private static int _consumedEscapeFrame = -1;
    private static int _releasedFrame = -1;

    private GameObject _player;
    private Action _onInterrupted;
    private PlayerInteraction _interaction;
    private NetworkPlayerInteractor _networkInteractor;
    private bool _promptWasSuppressed;
    private bool _networkPromptWasSuppressed;

    public GameObject Player => _player;
    public bool IsLocked => Locks.Contains(this);
    public static bool HasModal => Locks.Count > 0;
    public static bool EscapeConsumedThisFrame => _consumedEscapeFrame == Time.frameCount;
    public bool IsTopmost => IsLocked && Locks[Locks.Count - 1] == this;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Locks.Clear();
        _consumedEscapeFrame = -1;
        _releasedFrame = -1;
    }

    public static bool IsGameplayInputBlocked(GameObject player = null)
    {
        // Do not let a click used to close a panel also use a tool in the same frame.
        if (_releasedFrame == Time.frameCount) return true;
        foreach (var entry in Locks)
            if (player == null || SamePlayer(entry._player, player)) return true;
        return false;
    }

    public void Acquire(GameObject player, Action onInterrupted = null)
    {
        Release();
        if (player == null) return;
        var networkObject = player.GetComponentInParent<Fusion.NetworkObject>();
        if (networkObject != null && networkObject.IsValid && !networkObject.HasInputAuthority) return;

        _player = player;
        _onInterrupted = onInterrupted;
        _interaction = player.GetComponentInParent<PlayerInteraction>();
        _networkInteractor = player.GetComponentInParent<NetworkPlayerInteractor>();
        _promptWasSuppressed = _interaction != null && _interaction.IsInteractionPromptSuppressed;
        _networkPromptWasSuppressed = _networkInteractor != null && _networkInteractor.IsInteractionPromptSuppressed;
        foreach (var entry in Locks)
        {
            if (!SamePlayer(entry._player, player)) continue;
            _promptWasSuppressed = entry._promptWasSuppressed;
            _networkPromptWasSuppressed = entry._networkPromptWasSuppressed;
            break;
        }
        if (Locks.Count == 0)
        {
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
        }
        Locks.Add(this);
        _interaction?.SetInteractionPromptSuppressed(true);
        _networkInteractor?.SetInteractionPromptSuppressed(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PlayerInputStateDriver.EnsureExists();
    }

    public bool ConsumeEscape()
    {
        if (!IsTopmost || EscapeConsumedThisFrame || Keyboard.current == null
            || !Keyboard.current.escapeKey.wasPressedThisFrame) return false;
        _consumedEscapeFrame = Time.frameCount;
        return true;
    }

    public bool ShouldAutoRelease()
    {
        if (!IsLocked || _player == null || !_player.activeInHierarchy) return true;
        var life = _player.GetComponentInParent<NetworkPlayerLifeState>();
        if (life != null && life.Object != null && life.Object.IsValid && !life.CanInitiateAction) return true;
        var down = _player.GetComponentInParent<PlayerDownState>();
        return down != null && (down.IsDowned || down.IsEliminated);
    }

    public void Release()
    {
        if (!Locks.Remove(this)) return;
        _releasedFrame = Time.frameCount;
        bool anotherOwner = Locks.Exists(entry => SamePlayer(entry._player, _player));
        if (!anotherOwner)
        {
            if (_interaction != null) _interaction.SetInteractionPromptSuppressed(_promptWasSuppressed);
            if (_networkInteractor != null) _networkInteractor.SetInteractionPromptSuppressed(_networkPromptWasSuppressed);
        }
        if (Locks.Count == 0)
        {
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
        }
        _player = null;
        _interaction = null;
        _networkInteractor = null;
        _onInterrupted = null;
    }

    internal static void ReleaseInvalidLocks()
    {
        foreach (var entry in Locks.ToArray())
            if (entry.ShouldAutoRelease()) entry.Interrupt();
        if (Locks.Count > 0)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public static void ReleaseAll()
    {
        foreach (var entry in Locks.ToArray()) entry.Interrupt();
    }

    private void Interrupt()
    {
        var callback = _onInterrupted;
        Release();
        callback?.Invoke();
    }

    private static bool SamePlayer(GameObject left, GameObject right)
    {
        if (left == null || right == null) return left == right;
        return left == right || left.transform.IsChildOf(right.transform) || right.transform.IsChildOf(left.transform);
    }
}
