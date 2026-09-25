using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class PlayerInputStateDriver : MonoBehaviour
{
    private static PlayerInputStateDriver _instance;

    internal static void EnsureExists()
    {
        if (!Application.isPlaying || _instance != null) return;
        var owner = new GameObject("PlayerInputState");
        DontDestroyOnLoad(owner);
        _instance = owner.AddComponent<PlayerInputStateDriver>();
    }

    private void OnEnable() => SceneManager.sceneUnloaded += OnSceneUnloaded;
    private void Update() => PlayerInteractionControlLock.ReleaseInvalidLocks();
    private void OnSceneUnloaded(Scene scene) => PlayerInteractionControlLock.ReleaseAll();
    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        PlayerInteractionControlLock.ReleaseAll();
        if (_instance == this) _instance = null;
    }
}
