using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public void OpeningFadesInThenRolls()
        {
            var roll = new CreditsRollPlayback();
            Tick(roll, 0.5f, opening: 1.5f);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Opening));
            Assert.That(roll.RollSeconds, Is.Zero);
            Assert.That(roll.Opacity, Is.EqualTo(0.5f));
            Tick(roll, 2, opening: 1.5f);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Rolling));
            Assert.That(roll.RollSeconds, Is.EqualTo(1).Within(0.001));
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
            Tick(roll, 2, opening: 1, duration: 1);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Closing));
            Tick(roll, 2, duration: 1);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Closing));
            Tick(roll, 1, duration: 1);
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
            Assert.That(roll.RollSeconds, Is.Zero);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Opening));
        }

        [TestCase(30)]
        [TestCase(144)]
        public void PlaybackConsumesFullTimelineAtDifferentFrameRates(int fps)
        {
            var roll = new CreditsRollPlayback();
            float elapsed = 0;
            while (elapsed < 20 && roll.Phase != CreditsRollPhase.Completed)
            {
                Tick(roll, 1f / fps, opening: 5, duration: 4);
                elapsed += 1f / fps;
            }
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Completed));
            Assert.That(elapsed, Is.EqualTo(13).Within(2f / fps));
            Tick(roll, 100, true);
            Assert.That(roll.Opacity, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(6)]
        [TestCase(11)]
        public void SkipFadesFromOpeningRollAndClosing(float elapsed)
        {
            var roll = new CreditsRollPlayback();
            roll.Tick(elapsed, false, 5, 1, 5, 6);
            roll.Tick(2, true, 5, 1, 5, 6);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Fading));
            roll.Tick(1, false, 5, 1, 5, 6);
            Assert.That(roll.Phase, Is.EqualTo(CreditsRollPhase.Completed));
        }

        [Test]
        public void CreditNamesAndAdvisorMatchUserConfirmation()
        {
            var data = AssetDatabase.LoadAssetAtPath<CreditsData>("Assets/Resources/UI/Credits/EchoCreditsData.asset");
            Assert.That(data, Is.Not.Null); Assert.That(data.logo, Is.Not.Null);
            Assert.That(data.sections.Select(section => section.title), Is.EqualTo(new[]
            {
                "Opening Title", "Development Team", "Production & Project Management", "Game Design",
                "Narrative & Worldbuilding", "Engineering & Programming", "Art & Environment",
                "Technical Art & Cinematics", "UI/UX Design", "Audio & Sound Design",
                "Quality Assurance & Playtesting", "Academic Supervision", "Powered By",
                "Third-Party Assets & Attributions", "Legal & Copyright", "Special Thanks & Closing Title"
            }));
            Assert.That(data.sections.Select(section => section.id).Distinct().Count(), Is.EqualTo(16));
            Assert.That(data.sections.SelectMany(section => section.blocks).Count(), Is.EqualTo(19));
            var team = new[] { "Lâm Thanh Phúc", "Đặng Huy Hoàng", "Hứa Thành Đạt", "Nguyễn Nhật Lệ Thuyên" };
            Assert.That(data.sections[1].blocks.Single().entries.Select(entry => entry.name), Is.EqualTo(team));
            Assert.That(data.sections[10].blocks[0].entries.Select(entry => entry.name), Is.EqualTo(team));
            Assert.That(data.sections[10].blocks[1].entries.Select(entry => entry.name), Is.EqualTo(new[]
            {
                "Hồ Gia Bảo", "Nguyễn Đức Anh", "Trần Đức Anh", "ThieuTaTrungDoan2", "Huỳnh", "MinhOB",
                "Hieusensei", "NghiEm", "MeowToxic", "Buddy", "BuBu", "Sulie Uyen", "Ji1"
            }));
            Assert.That(data.sections[11].blocks.Single().entries.Single().name, Is.EqualTo("Đào Quốc Tuấn"));
            Assert.That(data.sections[15].blocks[0].entries.Single().name, Is.EqualTo("Đào Quốc Tuấn"));
            Assert.That(data.sections[15].blocks[1].note, Is.EqualTo("Thank you for playing."));
        }

        [Test]
        public void CreditsRolesAttributionsAndLegalMatchApprovedScope()
        {
            var data = AssetDatabase.LoadAssetAtPath<CreditsData>("Assets/Resources/UI/Credits/EchoCreditsData.asset");
            var engineering = data.sections[5].blocks.Single().entries;
            Assert.That(engineering.Where(entry => entry.role.Contains("Deployment & Infrastructure")).Select(entry => entry.name),
                Is.EqualTo(new[] { "Lâm Thanh Phúc", "Nguyễn Nhật Lệ Thuyên" }));
            var audio = data.sections[9].blocks.Single().entries;
            Assert.That(audio.Where(entry => entry.contribution.Contains("UI Sound Effects")).Select(entry => entry.name),
                Is.EqualTo(new[] { "Hứa Thành Đạt" }));
            var platforms = data.sections[12].blocks.Single();
            Assert.That(platforms.holdSeconds, Is.EqualTo(5));
            Assert.That(platforms.entries.Select(entry => entry.name), Is.EqualTo(new[] { "Unity", "Photon Engine" }));
            var attributions = data.sections[13].blocks.SelectMany(block => block.entries).ToDictionary(entry => entry.name, entry => entry.role);
            Assert.That(attributions.Count, Is.EqualTo(11));
            Assert.That(attributions["Spacefrigate"], Is.EqualTo("Game-Ready Legacy"));
            Assert.That(attributions["Flying carnivorous beetle"], Is.EqualTo("Andryuha1981"));
            Assert.That(attributions["Grave Mercer"], Is.EqualTo("MESHCORP"));
            var legal = string.Join("\n", data.sections[14].blocks.Single().entries.Select(entry => entry.role));
            foreach (string line in new[] { "© 2026 ECHO PROTOCOL Development Team.", "FPT University", "FA26SE069", "GFA26SE117" })
                Assert.That(legal, Does.Contain(line));
        }

        [Test]
        public void AfterCreditSceneHasRequiredReferencesAndNoNetworkObjects()
        {
            WithCredits(controller =>
            {
                Assert.That(controller.HasReferences, Is.True);
                Assert.That(controller.GetComponentsInChildren<Fusion.NetworkRunner>(true), Is.Empty);
                Assert.That(controller.GetComponentsInChildren<Fusion.NetworkObject>(true), Is.Empty);
                var scene = controller.gameObject.scene;
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .Any(camera => camera.enabled), Is.False);
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true))
                    .Any(light => light.enabled), Is.False);
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
                Assert.That(count, Is.EqualTo(19));
                float roleSize = labels.First(label => label.name == "Role").fontSize;
                Assert.That(labels.Where(label => label.name == "MemberName").All(label => label.fontSize > roleSize), Is.True);
                Assert.That(controller.ThankYou, Is.Not.Null);
                Assert.That(controller.PlannedDuration, Is.EqualTo(155.2f).Within(0.001f));
                Assert.That(labels.All(label => !label.text.Contains("Voice Acting") && !label.text.Contains("Narration")), Is.True);
                var logos = controller.Content.GetComponentsInChildren<Image>();
                Assert.That(logos.Count(image => image.preserveAspect && image.sprite == controller.Data.logo), Is.EqualTo(2));
            });
        }

        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3840, 2160)]
        [TestCase(2560, 1080)]
        [TestCase(1280, 720)]
        [TestCase(960, 540)]
        [TestCase(1024, 768)]
        public void ReferenceLayoutFitsAllTargetResolutions(int width, int height)
        {
            WithCredits(controller =>
            {
                var canvas = controller.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var rect = controller.GetComponent<RectTransform>();
                float scale = Mathf.Sqrt(width / 1920f * height / 1080f);
                rect.sizeDelta = new Vector2(width, height) / scale;
                controller.ApplySafeArea(new Rect(0, 0, width, height), new Vector2(width, height));
                controller.RebuildContent();
                var columns = controller.Content.Find("Development Team/Columns");
                var left = columns.GetChild(0).GetComponentsInChildren<TMP_Text>()
                    .Where(label => label.name == "MemberName").Select(label => label.text);
                var right = columns.GetChild(1).GetComponentsInChildren<TMP_Text>()
                    .Where(label => label.name == "MemberName").Select(label => label.text);
                var names = left.Zip(right, (first, second) => new[] { first, second }).SelectMany(row => row);
                Assert.That(names, Is.EqualTo(new[] { "Lâm Thanh Phúc", "Đặng Huy Hoàng", "Hứa Thành Đạt", "Nguyễn Nhật Lệ Thuyên" }));
                foreach (var label in controller.Content.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate(true);
                    Assert.That(label.rectTransform.rect.width, Is.GreaterThan(0).And.LessThanOrEqualTo(controller.Viewport.rect.width + 1), label.text);
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1), label.text);
                    Assert.That(label.isTextOverflowing, Is.False, label.text);
                    foreach (char character in label.text.Where(c => c > 127))
                        Assert.That(label.font.HasCharacter(character, true, false), Is.True, $"{label.font.name} missing {character} in {label.text}");
                }
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

        private static void Tick(CreditsRollPlayback roll, float delta, bool hold = false,
            float opening = 0, float duration = 100) => roll.Tick(delta, hold, opening, 1, duration, 3, 2);
    }
}
