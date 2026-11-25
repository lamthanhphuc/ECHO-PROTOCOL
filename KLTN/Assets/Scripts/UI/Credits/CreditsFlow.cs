using System;
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
        private static Task _playback;
        public static bool IsResultPlayback { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _playback = null; IsResultPlayback = false; }

        public static async Task PlayAndReturnToLobbyAsync()
        {
            if (_playback != null) { await _playback; return; }
            IsResultPlayback = true;
            var playback = PlayCoreAsync();
            _playback = playback;
            try { await playback; }
            finally
            {
                if (ReferenceEquals(_playback, playback)) { _playback = null; IsResultPlayback = false; }
            }
        }

        private static async Task PlayCoreAsync()
        {
            CreditsController controller = null;
            Scene creditsScene = default;
            try
            {
                if (!Application.CanStreamedLevelBeLoaded(GameConstants.SceneCredits))
                {
                    Debug.LogWarning("[Credits] AfterCredit is not in the scene list; using the existing Lobby return.");
                    await CinematicResultSceneController.NavigateSessionToLobbyAsync(false);
                    return;
                }
                var load = SceneManager.LoadSceneAsync(GameConstants.SceneCredits, LoadSceneMode.Additive);
                while (load != null && !load.isDone)
                {
                    if (!Application.isPlaying) throw new OperationCanceledException();
                    await Task.Yield();
                }
                creditsScene = SceneManager.GetSceneByName(GameConstants.SceneCredits);
                controller = CreditsController.Active;
                if (controller == null || !controller.HasReferences)
                    throw new InvalidOperationException("AfterCredit is missing a configured CreditsController.");
                await AwaitPlaybackThenNavigateAsync(controller.Completion,
                    () => CinematicResultSceneController.NavigateSessionToLobbyAsync(false));
            }
            finally
            {
                if (Application.isPlaying && controller != null) UnityEngine.Object.Destroy(controller.gameObject);
                if (Application.isPlaying && creditsScene.IsValid() && creditsScene.isLoaded && SceneManager.GetActiveScene() != creditsScene)
                {
                    var unload = SceneManager.UnloadSceneAsync(creditsScene);
                    while (unload != null && !unload.isDone) await Task.Yield();
                }
            }
        }

        internal static async Task AwaitPlaybackThenNavigateAsync(Task completion, Func<Task> navigate)
        {
            await completion;
            await navigate();
        }
    }
}
