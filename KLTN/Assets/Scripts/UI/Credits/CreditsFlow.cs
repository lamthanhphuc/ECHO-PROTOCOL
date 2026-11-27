using System;
using System.Threading;
using System.Threading.Tasks;
using EchoProtocol.Core;
using EchoProtocol.UI.Result;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.UI.Credits
{
    /// <summary>Loads local UI additively. Fusion continues owning its Result/Lobby scenes.</summary>
    public static class CreditsFlow
    {
        private const double SceneTimeoutSeconds = 30;
        private static Task _playback;
        private static AsyncOperation _discardedLoad;
        public static bool IsResultPlayback { get; private set; }
        internal static bool IsManagedPlayback => _playback != null;
        internal static bool IsDiscardingLoad => _discardedLoad != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (_discardedLoad != null) _discardedLoad.completed -= DiscardLoadedScene;
            _discardedLoad = null;
            _playback = null;
            IsResultPlayback = false;
        }

        public static Task PlayAndReturnToLobbyAsync() => PlayAsync(returnToLobby: true);

        public static Task PlayPreviewAsync() => PlayAsync(returnToLobby: false);

        private static async Task PlayAsync(bool returnToLobby)
        {
            while (_playback != null)
            {
                bool sameFlow = IsResultPlayback == returnToLobby;
                await _playback;
                if (sameFlow || !returnToLobby || !Application.isPlaying) return;
                await Task.Yield();
            }
            IsResultPlayback = returnToLobby;
            var playback = PlayCoreAsync(returnToLobby);
            _playback = playback;
            try { await playback; }
            finally
            {
                if (ReferenceEquals(_playback, playback)) { _playback = null; IsResultPlayback = false; }
            }
        }

        private static async Task PlayCoreAsync(bool returnToLobby)
        {
            // Assign the shared task before starting any load or fallback navigation.
            await Task.Yield();
            CreditsController controller = null;
            Scene creditsScene = default;
            try
            {
                try
                {
                    if (IsDiscardingLoad)
                        throw new InvalidOperationException("A timed-out AfterCredit load is still being discarded.");
                    if (!Application.CanStreamedLevelBeLoaded(GameConstants.SceneCredits))
                        throw new InvalidOperationException("AfterCredit is not in the build scene list.");
                    var load = SceneManager.LoadSceneAsync(GameConstants.SceneCredits, LoadSceneMode.Additive);
                    if (load == null) throw new InvalidOperationException("Unity could not start loading AfterCredit.");
                    double loadDeadline = Time.realtimeSinceStartupAsDouble + SceneTimeoutSeconds;
                    while (!load.isDone)
                    {
                        if (!Application.isPlaying) return;
                        if (Time.realtimeSinceStartupAsDouble >= loadDeadline)
                        {
                            // Unity scene loads cannot be canceled. Suppress and unload this scene if it arrives later.
                            _discardedLoad = load;
                            load.completed += DiscardLoadedScene;
                            throw new TimeoutException("AfterCredit scene load timed out.");
                        }
                        await Task.Yield();
                    }
                    creditsScene = SceneManager.GetSceneByName(GameConstants.SceneCredits);
                    controller = CreditsController.Active;
                    if (controller == null)
                        throw new InvalidOperationException("AfterCredit did not start a CreditsController.");
                    using var timeout = new CancellationTokenSource();
                    var deadline = Task.Delay(TimeSpan.FromMinutes(4), timeout.Token);
                    try
                    {
                        if (await Task.WhenAny(controller.Completion, deadline) == deadline)
                            throw new TimeoutException("AfterCredit playback timed out.");
                        await controller.Completion;
                    }
                    finally { timeout.Cancel(); }
                }
                catch (Exception error) when (Application.isPlaying)
                {
                    Debug.LogWarning($"[Credits] {error.Message}");
                    if (controller != null) controller.gameObject.SetActive(false);
                }
                if (returnToLobby && Application.isPlaying)
                    await CinematicResultSceneController.NavigateSessionToLobbyAsync(false);
            }
            finally
            {
                if (Application.isPlaying && controller != null) UnityEngine.Object.Destroy(controller.gameObject);
                if (Application.isPlaying && creditsScene.IsValid() && creditsScene.isLoaded && SceneManager.GetActiveScene() != creditsScene)
                {
                    var unload = SceneManager.UnloadSceneAsync(creditsScene);
                    double unloadDeadline = Time.realtimeSinceStartupAsDouble + SceneTimeoutSeconds;
                    while (Application.isPlaying && unload != null && !unload.isDone
                        && Time.realtimeSinceStartupAsDouble < unloadDeadline) await Task.Yield();
                }
            }
        }

        private static void DiscardLoadedScene(AsyncOperation load)
        {
            if (_discardedLoad != load) return;
            _discardedLoad = null;
            if (!Application.isPlaying) return;
            var scene = SceneManager.GetSceneByName(GameConstants.SceneCredits);
            if (scene.IsValid() && scene.isLoaded && SceneManager.GetActiveScene() != scene)
                SceneManager.UnloadSceneAsync(scene);
        }
    }
}
