using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EchoProtocol.Core;
using EchoProtocol.UI.Credits;
using EchoProtocol.UI.Result;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EchoProtocol.Tests.EditMode.Controls
{
    public sealed class CreditsTests
    {
        private const string ScenePath = "Assets/Scenes/AfterCredit.unity";

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void CreditsRequireResultAndConfirmedReward(bool result, bool reward, bool expected) =>
            Assert.That(CreditsRollPlayback.CanPresentAfterResult(result, reward), Is.EqualTo(expected));

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void ResultControllerOnlyOffersCreditsForVerifiedReward(bool hasResult, bool confirmed, bool expected)
        {
            var root = new GameObject("Credits result gate"); root.SetActive(false);
            try
            {
                var controller = root.AddComponent<CinematicResultSceneController>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var snapshot = hasResult ? new ResultSnapshot(EchoProtocol.Networking.NetworkMatchResult.Win,
                    EchoProtocol.Networking.NetworkMatchEndReason.PlayerEscaped, 120, 1, 1, 1,
                    Guid.NewGuid().ToString("D"), true, Array.Empty<ResultPlayerSnapshot>()) : null;
                typeof(CinematicResultSceneController).GetField("_snapshot", flags).SetValue(controller, snapshot);
                typeof(CinematicResultSceneController).GetField("_rewardConfirmed", flags).SetValue(controller, confirmed);
                Assert.That(controller.CanShowCredits, Is.EqualTo(expected));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ScrollStartsAfterDelay_AndUsesDesignUnits()
        {
            var roll = new CreditsRollPlayback();
            Tick(roll, 1, delay: 1.5f);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Waiting));
            Assert.That(roll.Distance, Is.Zero);
            Tick(roll, 0.5f, delay: 1.5f);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Rolling));
            Tick(roll, 1, speed: 58);
            Assert.That(roll.Distance, Is.EqualTo(58).Within(0.001));
            Assert.That(roll.Opacity, Is.EqualTo(1));
        }

        [Test]
        public void SkipRequiresTwoContinuousSeconds_ThenFades()
        {
            var roll = new CreditsRollPlayback();
            Tick(roll, 1.99f, true);
            Assert.That(roll.Phase, Is.Not.EqualTo(CreditsRollPhase.Fading));
            Tick(roll, 0.02f, true);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Fading));
            Assert.That(CreditsRollPlayback.HoldProgress(roll.SkipHeldSeconds, 2), Is.EqualTo(1));
            Tick(roll, 1);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Completed));
        }

        [Test]
        public void ReleasingSpaceResetsProgress()
        {
            var roll = new CreditsRollPlayback();
            Tick(roll, 1.5f, true);
            Tick(roll, 0.1f);
            Assert.That(roll.SkipHeldSeconds, Is.Zero);
            Tick(roll, 1.9f, true);
            Assert.That(roll.Phase, Is.Not.EqualTo(CreditsRollPhase.Completed));
            Assert.That(roll.Phase, Is.Not.EqualTo(CreditsRollPhase.Fading));
        }

        [Test]
        public void NaturalEndHoldsThankYouBeforeFadeAndCompletion()
        {
            var roll = new CreditsRollPlayback();
            Tick(roll, 0);
            Tick(roll, 1, speed: 100, end: 100);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Closing));
            Tick(roll, 2, end: 100);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Closing));
            Tick(roll, 1, end: 100);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Fading));
            Tick(roll, 1);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Completed));
            Assert.That(roll.Opacity, Is.Zero);
        }

        [Test]
        public void ClientsHaveIndependentPlaybackAndSkip()
        {
            var first = new CreditsRollPlayback(); var second = new CreditsRollPlayback();
            Tick(first, 2, true); Tick(first, 1);
            Tick(second, 0); Tick(second, 1);
            Assert.That(first.Phase, Is.EqualTo(CreditsRollPhase.Completed));
            Assert.That(second.Phase, Is.EqualTo(CreditsRollPhase.Rolling));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void InvalidTimeDoesNotCorruptPlayback(float delta)
        {
            var roll = new CreditsRollPlayback(); Tick(roll, delta);
            Assert.That(roll.Distance, Is.Zero); Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Waiting));
        }

        [Test]
        public async Task LobbyNavigationWaitsForLocalPlaybackCompletion()
        {
            var completion = new TaskCompletionSource<bool>(); int navigations = 0;
            var flow = AwaitCreditsCompletion(completion.Task, () => { navigations++; return Task.CompletedTask; });
            Assert.That(navigations, Is.Zero); Assert.That(flow.IsCompleted, Is.False);
            completion.SetResult(true); await flow;
            Assert.That(navigations, Is.EqualTo(1));
        }

        [Test]
        public void InterruptedPlaybackDoesNotNavigatePrematurely()
        {
            var completion = new TaskCompletionSource<bool>(); int navigations = 0;
            completion.SetCanceled();
            var flow = AwaitCreditsCompletion(completion.Task, () => { navigations++; return Task.CompletedTask; });
            Assert.ThrowsAsync<TaskCanceledException>(async () => await flow);
            Assert.That(navigations, Is.Zero);
        }

        [Test]
        public void CreditNamesAndAdvisorMatchUserConfirmation()
        {
            var data = AssetDatabase.LoadAssetAtPath<CreditsData>("Assets/Resources/UI/Credits/EchoCreditsData.asset");
            Assert.That(data, Is.Not.Null); Assert.That(data.logo, Is.Not.Null);
            Assert.That(data.sections.SelectMany(section => section.names), Is.EqualTo(new[]
                { "Đặng Huy Hoàng", "Hứa Thành Đạt", "Lâm Thanh Phúc", "Nguyễn Nhật Lệ Thuyên", "Đào Quốc Tuấn" }));
            Assert.That(data.sections.All(section => !string.IsNullOrWhiteSpace(section.evidence)), Is.True);
            Assert.That(data.closingMessage, Is.EqualTo("THANK YOU FOR PLAYING"));
        }

        [Test]
        public void AfterCreditSceneHasRequiredReferencesAndNoNetworkObjects()
        {
            WithCredits(controller =>
            {
                Assert.That(controller.HasReferences, Is.True);
                Assert.That(controller.GetComponentsInChildren<Fusion.NetworkRunner>(true), Is.Empty);
                Assert.That(controller.GetComponentsInChildren<Fusion.NetworkObject>(true), Is.Empty);
                Assert.That(controller.Viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(controller.Content.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
                Assert.That(controller.Content.GetComponent<CanvasGroup>(), Is.Not.Null);
                var scaler = controller.GetComponent<CanvasScaler>();
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
                var serialized = new SerializedObject(controller);
                Assert.That(serialized.FindProperty("_backgroundMusic").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("_holdToSkipSeconds").floatValue, Is.EqualTo(2));
                var progress = (Image)serialized.FindProperty("_skipProgress").objectReferenceValue;
                Assert.That(progress.sprite, Is.Not.Null, "Filled Images need a sprite to draw partial progress.");
                Assert.That(progress.type, Is.EqualTo(Image.Type.Filled));
                Assert.That(progress.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
                Assert.That(progress.fillAmount, Is.Zero);
            });
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.path == ScenePath && scene.enabled), Is.True);
            Assert.That(GameConstants.SceneCredits, Is.EqualTo("AfterCredit"));
        }

        [Test]
        public void RebuildUsesNamesLargerThanRoleTitles_WithoutDuplicatingRows()
        {
            WithCredits(controller =>
            {
                controller.RebuildContent(); int count = controller.Content.childCount;
                controller.RebuildContent(); Assert.That(controller.Content.childCount, Is.EqualTo(count));
                var labels = controller.Content.GetComponentsInChildren<TMP_Text>(true);
                Assert.That(labels.Count(label => label.name == "MemberName"), Is.EqualTo(5));
                float roleSize = labels.First(label => label.name == "Role").fontSize;
                Assert.That(labels.Where(label => label.name == "MemberName").All(label => label.fontSize > roleSize), Is.True);
                Assert.That(controller.ThankYou, Is.Not.Null);
            });
        }

        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3840, 2160)]
        public void ReferenceLayoutFitsAllTargetResolutions(int width, int height)
        {
            WithCredits(controller =>
            {
                var canvas = controller.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var rect = controller.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(1920, 1080);
                rect.localScale = Vector3.one * (width / 1920f);
                controller.RebuildContent(); controller.PreviewSection(3);
                Assert.That(width / 1920f, Is.EqualTo(height / 1080f).Within(0.001));
                foreach (var label in controller.Content.GetComponentsInChildren<TMP_Text>())
                    Assert.That(label.rectTransform.rect.width, Is.LessThanOrEqualTo(controller.Viewport.rect.width + 1));
            });
        }

        [Test]
        public void CleanupRestoresLocalAudioMuteState()
        {
            var root = new GameObject("Credits cleanup"); root.SetActive(false);
            var sourceRoot = new GameObject("Underlying audio"); sourceRoot.SetActive(false);
            try
            {
                var controller = root.AddComponent<CreditsController>();
                var source = sourceRoot.AddComponent<AudioSource>(); source.mute = true;
                var field = typeof(CreditsController).GetField("_ducked", BindingFlags.Instance | BindingFlags.NonPublic);
                var map = (System.Collections.Generic.Dictionary<AudioSource, bool>)field.GetValue(controller);
                map[source] = false;
                typeof(CreditsController).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                Assert.That(source.mute, Is.False); Assert.That(map, Is.Empty);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(sourceRoot); }
        }

        private static void WithCredits(Action<CreditsController> check)
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try { check(scene.GetRootGameObjects().Select(root => root.GetComponent<CreditsController>()).Single(value => value != null)); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Task AwaitCreditsCompletion(Task completion, Func<Task> navigate)
        {
            var method = typeof(CreditsFlow).GetMethod("AwaitPlaybackThenNavigateAsync", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Task)method.Invoke(null, new object[] { completion, navigate });
        }

        private static void Tick(CreditsRollPlayback roll, float delta, bool hold = false,
            float speed = 58, float delay = 0, float end = 10000) => roll.Tick(delta, hold, speed, delay, 1, end, 3, 2);
    }
}
