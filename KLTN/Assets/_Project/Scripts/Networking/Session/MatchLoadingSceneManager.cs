using System;
using System.IO;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Networking
{
    /// <summary>Observes Fusion's normal scene operations without taking ownership of loading.</summary>
    public sealed class MatchLoadingSceneManager : NetworkSceneManagerDefault
    {
        public override NetworkSceneAsyncOp LoadScene(SceneRef sceneRef, NetworkLoadSceneParameters parameters)
        {
            var runner = Runner;
            var sceneName = ResolveSceneName(sceneRef);
            bool gameplay = LobbyMapCatalog.IsGameplayScene(sceneName);
            if (gameplay)
                Observe(() => MatchLoadingCoordinator.Instance?.SceneLoadStarted(runner, sceneName));

            NetworkSceneAsyncOp operation;
            try
            {
                operation = base.LoadScene(sceneRef, parameters);
            }
            catch (Exception exception)
            {
                if (gameplay) ReportFailure(runner, sceneName, exception);
                throw;
            }

            if (gameplay)
            {
                // Completion can run immediately for an already completed/error operation.
                // Capture the runner: Shutdown() may clear the manager's Runner before completion.
                operation.AddOnCompleted(completed =>
                {
                    if (completed.Error != null)
                        ReportFailure(runner, sceneName, completed.Error);
                    else
                        Observe(() => MatchLoadingCoordinator.Instance?.SceneLoadCompleted(runner, sceneName));
                });
            }

            return operation;
        }

        protected override void OnLoadSceneProgress(SceneRef sceneRef, float progress)
        {
            base.OnLoadSceneProgress(sceneRef, progress);
            var runner = Runner;
            var sceneName = ResolveSceneName(sceneRef);
            if (LobbyMapCatalog.IsGameplayScene(sceneName))
                Observe(() => MatchLoadingCoordinator.Instance?.SceneLoadProgress(runner, sceneName, progress));
        }

        private static void ReportFailure(NetworkRunner runner, string sceneName, Exception exception)
        {
            // The observed operation has ended, so clearing the callback flag is safe.
            // Runner teardown remains owned by NetworkBootstrap and its scene-idle fence.
            Observe(() => NetworkBootstrap.Instance?.ReportSceneLoadFailure(
                runner, $"Scene '{sceneName}' failed to load: {exception.Message}"));
            Observe(() => MatchLoadingCoordinator.Instance?.SceneLoadFailed(runner, sceneName, exception));
        }

        private static void Observe(Action notification)
        {
            // Presentation errors must not abort Fusion's scene operation or its callbacks.
            try { notification(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static string ResolveSceneName(SceneRef sceneRef)
        {
            if (sceneRef.IsIndex)
                return Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(sceneRef.AsIndex));

            for (int index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(index);
                if (sceneRef.IsPath(path)) return Path.GetFileNameWithoutExtension(path);
            }

            return string.Empty;
        }
    }
}
