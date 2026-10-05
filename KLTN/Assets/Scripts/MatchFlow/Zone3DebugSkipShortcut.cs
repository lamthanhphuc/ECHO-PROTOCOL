using EchoProtocol.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class Zone3DebugSkipShortcut : MonoBehaviour
{
    [SerializeField] private Key zone2SkipKey = Key.F8;
    [SerializeField] private Key skipKey = Key.F9;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<Zone3DebugSkipShortcut>() != null) return;

        var shortcut = new GameObject("Zone3 Debug Skip Shortcut");
        DontDestroyOnLoad(shortcut);
        shortcut.AddComponent<Zone3DebugSkipShortcut>();
    }
#endif

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[zone2SkipKey].wasPressedThisFrame)
        {
            var match = NetworkMatchState.Instance;
            if (match != null && match.Object != null)
                match.RequestDebugSkipToZone2();
            else
                FindAnyObjectByType<MatchFlowController>()?.DebugSkipToZone2();
            return;
        }

        if (!keyboard[skipKey].wasPressedThisFrame) return;

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
#endif
    }
}
