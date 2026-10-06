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
        var keyboard = Keyboard.current;
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

        if (!ctrl
            || !shift
            || !keyboard.gKey.wasPressedThisFrame)
        {
            return;
        }

        var lifeStates =
            FindObjectsByType<NetworkPlayerLifeState>(
                FindObjectsInactive.Exclude);

        foreach (var lifeState in lifeStates)
        {
            if (lifeState == null
                || lifeState.Object == null
                || !lifeState.Object.IsValid
                || !lifeState.Object.HasInputAuthority)
            {
                continue;
            }

            lifeState.RequestDebugGodModeToggle();
            return;
        }

        Debug.LogWarning(
            "[DebugGodMode] Local player life state not found.");
    }
}
