using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class Zone3DebugSkipShortcut : MonoBehaviour
{
    [SerializeField] private Key skipKey = Key.F9;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<Zone3DebugSkipShortcut>() != null) return;

        var shortcut = new GameObject("Zone3 Debug Skip Shortcut");
        DontDestroyOnLoad(shortcut);
        shortcut.AddComponent<Zone3DebugSkipShortcut>();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard[skipKey].wasPressedThisFrame) return;

        var networkMatch = NetworkMatchState.Instance;
        if (networkMatch != null && networkMatch.Object != null)
        {
            networkMatch.RequestDebugSkipToZone3();
            Debug.Log("[Zone3DebugSkip] Requested Zone 3 skip.");
            return;
        }

        var offlineFlow = FindAnyObjectByType<MatchFlowController>();
        if (offlineFlow == null)
        {
            Debug.LogWarning("[Zone3DebugSkip] Match flow not found.");
            return;
        }

        offlineFlow.DebugSkipToZone3();
        Debug.Log("[Zone3DebugSkip] Skipped offline match flow to Zone 3.");
    }
}
