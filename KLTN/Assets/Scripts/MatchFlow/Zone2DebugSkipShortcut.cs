using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class Zone2DebugSkipShortcut : MonoBehaviour
{
    [SerializeField] private Key skipKey = Key.F8;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<Zone2DebugSkipShortcut>() != null) return;

        var shortcut = new GameObject("Zone2 Debug Skip Shortcut");
        DontDestroyOnLoad(shortcut);
        shortcut.AddComponent<Zone2DebugSkipShortcut>();
    }
#endif

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard[skipKey].wasPressedThisFrame) return;

        var networkMatch = NetworkMatchState.Instance;
        if (networkMatch != null && networkMatch.Object != null)
        {
            networkMatch.RequestDebugSkipToZone2();
            Debug.Log("[Zone2DebugSkip] Requested Zone 2 skip.");
            return;
        }

        var offlineFlow = FindAnyObjectByType<MatchFlowController>();
        if (offlineFlow == null)
        {
            Debug.LogWarning("[Zone2DebugSkip] Match flow not found.");
            return;
        }

        offlineFlow.DebugSkipToZone2();
        Debug.Log("[Zone2DebugSkip] Skipped offline match flow to Zone 2.");
#endif
    }
}
