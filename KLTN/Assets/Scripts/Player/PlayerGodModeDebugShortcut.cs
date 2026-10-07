using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerGodModeDebugShortcut : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<PlayerGodModeDebugShortcut>() != null)
        {
            return;
        }

        var shortcut =
            new GameObject("Player God Mode Debug Shortcut");

        DontDestroyOnLoad(shortcut);

        shortcut.AddComponent<
            PlayerGodModeDebugShortcut>();
    }

    private void Update()
    {
        var keyboard =
            Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        bool ctrl =
            keyboard.leftCtrlKey.isPressed
            || keyboard.rightCtrlKey.isPressed;

        bool shift =
            keyboard.leftShiftKey.isPressed
            || keyboard.rightShiftKey.isPressed;

        if (!ctrl || !shift)
        {
            return;
        }

        NetworkPlayerLifeState localLife =
            FindLocalLifeState();

        if (localLife == null)
        {
            return;
        }

        if (keyboard.gKey.wasPressedThisFrame)
        {
            localLife.RequestDebugGodModeToggle();
            return;
        }

        if (!localLife.DebugGodMode)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            localLife.RequestDebugTeamTool(
                LobbyPlayerState.FieldScannerToolId);
            return;
        }

        if (keyboard.digit2Key.wasPressedThisFrame)
        {
            localLife.RequestDebugTeamTool(
                LobbyPlayerState.NoiseMakerToolId);
            return;
        }

        if (keyboard.digit3Key.wasPressedThisFrame)
        {
            localLife.RequestDebugTeamTool(
                LobbyPlayerState.FirstAidKitToolId);
            return;
        }

        if (keyboard.digit4Key.wasPressedThisFrame)
        {
            localLife.RequestDebugTeamTool(
                LobbyPlayerState.DoorJammerToolId);
            return;
        }

        if (keyboard.digit5Key.wasPressedThisFrame)
        {
            localLife.RequestDebugTeamTool(
                LobbyPlayerState.CoreStabilizerToolId);
        }
    }

    private static NetworkPlayerLifeState FindLocalLifeState()
    {
        var lifeStates =
            FindObjectsByType<NetworkPlayerLifeState>(
                FindObjectsInactive.Exclude);

        foreach (var lifeState in lifeStates)
        {
            if (lifeState != null
                && lifeState.Object != null
                && lifeState.Object.IsValid
                && lifeState.Object.HasInputAuthority)
            {
                return lifeState;
            }
        }

        return null;
    }
}
