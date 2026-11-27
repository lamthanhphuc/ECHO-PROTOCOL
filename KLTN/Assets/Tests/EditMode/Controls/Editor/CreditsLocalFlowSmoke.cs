using System;
using System.Collections;
using System.IO;
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
        private float _previousTimeScale = 1;

        public IEnumerator ResultCreditsLobby()
        {
            int runnersBefore = Object.FindObjectsByType<Fusion.NetworkRunner>(FindObjectsInactive.Include).Length;
            yield return LoadResultWithReward(true);
            var result = Object.FindAnyObjectByType<CinematicResultSceneController>();
            Assert.That(result.CanShowCredits, Is.True);
            ReturnToLobby(result);
            float deadline = Time.realtimeSinceStartup + 20;
            while (CreditsController.Active == null && Time.realtimeSinceStartup < deadline) yield return null;
            var credits = CreditsController.Active;
            Assert.That(credits, Is.Not.Null, "Result must load the real AfterCredit scene.");
            yield return null; yield return null;
            Assert.That(credits.HasReferences, Is.True);
            Assert.That(credits.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(_result), "Local credits must not take over Fusion's active scene.");
            var completion = credits.Completion;
            Set(credits, "_playbackRate", 30f);
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            var music = credits.GetComponentInChildren<AudioSource>();
            Assert.That(music.isPlaying, Is.True); Assert.That(music.spatialBlend, Is.Zero);
            Assert.That(music.outputAudioMixerGroup?.name, Is.EqualTo("Music"));
            yield return new WaitForSecondsRealtime(0.25f);
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

        public IEnumerator UnconfirmedRewardReturnsToLobbyWithoutLoadingCredits()
        {
            yield return LoadResultWithReward(false);
            var result = Object.FindAnyObjectByType<CinematicResultSceneController>();
            Assert.That(result.CanShowCredits, Is.False);
            MoveToLobbyFixture();
            ReturnToLobby(result);
            yield return null;
            Assert.That(CreditsFlow.IsResultPlayback, Is.False);
            Assert.That(CreditsController.Active, Is.Null);
            Assert.That(ResultSession.Current, Is.Null);
        }

        public IEnumerator AutoPlayWithMissingCreditsContentFallsBackToLobby()
        {
            yield return LoadResultWithReward(true);
            var result = Object.FindAnyObjectByType<CinematicResultSceneController>();
            var data = Resources.Load<CreditsData>("UI/Credits/EchoCreditsData");
            Assert.That(data, Is.Not.Null);
            var sections = data.sections;
            MoveToLobbyFixture();
            try
            {
                data.sections = Array.Empty<CreditsData.Section>();
                Set(result, "creditsResultReadSeconds", 0f);
                Set(result, "autoPlayCredits", true);
                yield return null;
                float deadline = Time.realtimeSinceStartup + 20;
                while ((CreditsFlow.IsResultPlayback || ResultSession.Current != null) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                yield return null;
                Assert.That(ResultSession.Current, Is.Null, "Auto-play failure must still run the existing Lobby return.");
                Assert.That(CreditsFlow.IsResultPlayback, Is.False);
                Assert.That(SceneManager.GetSceneByName(GameConstants.SceneCredits).isLoaded, Is.False);
                Assert.That(CreditsController.Active, Is.Null);
            }
            finally { data.sections = sections; }
        }

        private IEnumerator LoadResultWithReward(bool confirmed)
        {
            yield return SceneManager.LoadSceneAsync(GameConstants.SceneResult, LoadSceneMode.Additive);
            _result = SceneManager.GetSceneByName(GameConstants.SceneResult);
            SceneManager.SetActiveScene(_result);
            yield return null;
            var result = Object.FindAnyObjectByType<CinematicResultSceneController>();
            Assert.That(result, Is.Not.Null);
            // Stub the verified reward boundary, with no authentication or reward submission.
            var snapshot = new ResultSnapshot(EchoProtocol.Networking.NetworkMatchResult.Win,
                EchoProtocol.Networking.NetworkMatchEndReason.PlayerEscaped, 120, 1, 1, 1, "", true,
                Array.Empty<ResultPlayerSnapshot>());
            typeof(ResultSession).GetProperty("Current").SetValue(null, snapshot);
            Set(result, "_snapshot", snapshot);
            Set(result, "_rewardConfirmed", confirmed);
            Set(result, "autoPlayCredits", false);
            result.FinishIntro();
        }

        private void MoveToLobbyFixture()
        {
            _lobby = SceneManager.CreateScene(GameConstants.SceneLobby);
            SceneManager.SetActiveScene(_lobby);
        }

        private static void ReturnToLobby(CinematicResultSceneController result) =>
            typeof(CinematicResultSceneController).GetMethod("ReturnToLobby", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(result, null);

        public IEnumerator DirectPreviewPlaysFullTimelineWithoutNetworkOrAuthentication()
        {
            var previousScene = SceneManager.GetActiveScene();
            var runnersBefore = Object.FindObjectsByType<Fusion.NetworkRunner>(FindObjectsInactive.Include).Length;
            yield return SceneManager.LoadSceneAsync(GameConstants.SceneCredits, LoadSceneMode.Additive);
            var credits = CreditsController.Active;
            Assert.That(credits, Is.Not.Null);
            var creditsScene = credits.gameObject.scene;
            Assert.That(creditsScene.name, Is.EqualTo(GameConstants.SceneCredits));
            Assert.That(CreditsFlow.IsResultPlayback, Is.False);
            yield return null;
            float started = Time.realtimeSinceStartup;
            float duration = credits.PlannedDuration;
            Assert.That(duration, Is.EqualTo(155.2f).Within(0.01f));
            var platform = (RectTransform)credits.Content.Find("Powered By");
            float settled = 0, previousY = float.PositiveInfinity;
            while (!credits.Completion.IsCompleted && Time.realtimeSinceStartup - started < duration + 10)
            {
                float position = credits.Viewport.InverseTransformPoint(platform.TransformPoint(platform.rect.center)).y;
                if (Mathf.Abs(position - credits.Viewport.rect.center.y) < 0.05f && Mathf.Abs(position - previousY) < 0.001f)
                    settled += Time.unscaledDeltaTime;
                previousY = position;
                yield return null;
            }
            float actual = Time.realtimeSinceStartup - started;
            Assert.That(credits.Completion.IsCompletedSuccessfully, Is.True);
            Assert.That(actual, Is.EqualTo(duration).Within(1f));
            Assert.That(settled, Is.EqualTo(5).Within(0.2f));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousScene));
            Assert.That(credits.GetComponentInChildren<AudioSource>().isPlaying, Is.False);
            Assert.That(Object.FindObjectsByType<Fusion.NetworkRunner>(FindObjectsInactive.Include).Length, Is.EqualTo(runnersBefore));
            File.WriteAllText("Temp/Credits.runtime-timing.txt",
                FormattableString.Invariant($"Runtime: {actual:F2}s, Powered By centered: {settled:F2}s."));
            yield return SceneManager.UnloadSceneAsync(creditsScene);
        }

        public IEnumerator Cleanup()
        {
            Time.timeScale = _previousTimeScale;
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
