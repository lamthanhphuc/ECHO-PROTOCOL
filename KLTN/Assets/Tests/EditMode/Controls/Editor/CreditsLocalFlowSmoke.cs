using System;
using System.Collections;
using System.Reflection;
using EchoProtocol.Core;
using EchoProtocol.UI.Credits;
using EchoProtocol.UI.Result;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EchoProtocol.Tests.EditMode.Controls
{
    // Invoked by the PlayMode asmdef through reflection, as it cannot reference Assembly-CSharp.
    public sealed class CreditsLocalFlowSmoke
    {
        private Scene _result, _lobby;

        public IEnumerator ResultCreditsLobby()
        {
            int runnersBefore = Object.FindObjectsByType<Fusion.NetworkRunner>(FindObjectsInactive.Include).Length;
            yield return SceneManager.LoadSceneAsync(GameConstants.SceneResult, LoadSceneMode.Additive);
            _result = SceneManager.GetSceneByName(GameConstants.SceneResult);
            SceneManager.SetActiveScene(_result);
            yield return null;
            var result = Object.FindAnyObjectByType<CinematicResultSceneController>();
            Assert.That(result, Is.Not.Null);
            // Stub only the verified reward boundary; no authentication, reward submission or backend call.
            var snapshot = new ResultSnapshot(EchoProtocol.Networking.NetworkMatchResult.Win,
                EchoProtocol.Networking.NetworkMatchEndReason.PlayerEscaped, 120, 1, 1, 1, "", true,
                Array.Empty<ResultPlayerSnapshot>());
            typeof(ResultSession).GetProperty("Current").SetValue(null, snapshot);
            Set(result, "_snapshot", snapshot); Set(result, "_rewardConfirmed", true);
            Set(result, "autoPlayCredits", false); result.FinishIntro();
            Assert.That(result.CanShowCredits, Is.True);
            typeof(CinematicResultSceneController).GetMethod("ReturnToLobby", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(result, null);
            float deadline = Time.realtimeSinceStartup + 20;
            while (CreditsController.Active == null && Time.realtimeSinceStartup < deadline) yield return null;
            var credits = CreditsController.Active;
            Assert.That(credits, Is.Not.Null, "Result must load the real AfterCredit scene.");
            yield return null; yield return null;
            Assert.That(credits.HasReferences, Is.True);
            Assert.That(credits.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(_result), "Local credits must not take over Fusion's active scene.");
            var completion = credits.Completion;
            Set(credits, "_startDelay", 0f); Set(credits, "_scrollSpeed", 900f);
            Set(credits, "_closingHold", 0.1f); Set(credits, "_fadeDuration", 0.05f);
            var music = credits.GetComponentInChildren<AudioSource>();
            Assert.That(music.isPlaying, Is.True); Assert.That(music.spatialBlend, Is.Zero);
            float y = credits.Content.anchoredPosition.y;
            yield return null; yield return null;
            Assert.That(credits.Content.anchoredPosition.y, Is.GreaterThan(y));
            // Simulate the host's shared scene handoff with a local Lobby fixture. The viewer must survive.
            _lobby = SceneManager.CreateScene(GameConstants.SceneLobby);
            SceneManager.SetActiveScene(_lobby);
            yield return SceneManager.UnloadSceneAsync(_result);
            Assert.That(credits != null && !completion.IsCompleted, Is.True);
            Assert.That(ResultSession.Current, Is.Not.Null, "Credits completion must precede Lobby result cleanup.");
            deadline = Time.realtimeSinceStartup + 15;
            while (CreditsFlow.IsResultPlayback && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(completion.IsCompletedSuccessfully, Is.True);
            Assert.That(CreditsFlow.IsResultPlayback, Is.False);
            Assert.That(CreditsController.Active, Is.Null);
            Assert.That(ResultSession.Current, Is.Null);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(_lobby));
            Assert.That(SceneManager.GetSceneByName(GameConstants.SceneCredits).isLoaded, Is.False);
            Assert.That(Object.FindObjectsByType<Fusion.NetworkRunner>(FindObjectsInactive.Include).Length, Is.EqualTo(runnersBefore));
            Assert.That(PlayerInteractionControlLock.HasModal, Is.False);
        }

        public IEnumerator Cleanup()
        {
            if (CreditsController.Active != null) Object.Destroy(CreditsController.Active.gameObject);
            yield return null;
            ResultSession.Clear();
            var creditsScene = SceneManager.GetSceneByName(GameConstants.SceneCredits);
            if (creditsScene.IsValid() && creditsScene.isLoaded) yield return SceneManager.UnloadSceneAsync(creditsScene);
            if (_result.IsValid() && _result.isLoaded) yield return SceneManager.UnloadSceneAsync(_result);
            if (_lobby.IsValid() && _lobby.isLoaded) yield return SceneManager.UnloadSceneAsync(_lobby);
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
