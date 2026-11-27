using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EchoProtocol.Cinematics;
using EchoProtocol.Networking;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;
using Assert = NUnit.Framework.Assert;

namespace EchoProtocol.Tests.EditMode.Controls
{
    public sealed class OpeningCutsceneLifecycleTests
    {
        [Test]
        public void MissingPresentationReferencesAreUnavailable_AndCleanupDoesNotThrow()
        {
            var root = new GameObject("Incomplete opening UI"); root.SetActive(false);
            try
            {
                var presentation = root.AddComponent<OpeningCutscenePresentation>();
                Assert.That(presentation.IsReady, Is.False);
                Assert.DoesNotThrow(() => presentation.Hide());
                Assert.DoesNotThrow(() => presentation.Show("", 1, 0, 1, false, 0));
                Assert.DoesNotThrow(() => presentation.FadeToGameplay(0));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MissingTimeline_FailsOpenBeforePlayback()
        {
            var definition = ScriptableObject.CreateInstance<OpeningCutsceneDefinition>();
            try { Assert.That(definition.IsPlayable, Is.False); Assert.That(definition.Duration, Is.Zero); }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void CleanupRestoresCapturedCameraListenerAndHudStates_AndIsIdempotent()
        {
            var root = new GameObject("Opening cleanup test"); root.SetActive(false);
            try
            {
                var controller = root.AddComponent<OpeningCutsceneController>();
                var camera = root.AddComponent<Camera>(); camera.enabled = false;
                var listener = root.AddComponent<AudioListener>(); listener.enabled = false;
                var canvas = new GameObject("HUD", typeof(Canvas)).GetComponent<Canvas>(); canvas.transform.SetParent(root.transform);
                canvas.enabled = true;
                var body = root.AddComponent<PlayerFirstPersonVisibility>(); body.SetCinematicView(true);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                ((Dictionary<Camera, bool>)typeof(OpeningCutsceneController).GetField("_cameras", flags).GetValue(controller)).Add(camera, true);
                ((Dictionary<AudioListener, bool>)typeof(OpeningCutsceneController).GetField("_listeners", flags).GetValue(controller)).Add(listener, true);
                ((Dictionary<Canvas, bool>)typeof(OpeningCutsceneController).GetField("_hud", flags).GetValue(controller)).Add(canvas, false);
                ((Dictionary<PlayerFirstPersonVisibility, bool>)typeof(OpeningCutsceneController).GetField("_bodyViews", flags).GetValue(controller)).Add(body, false);
                controller.Cleanup();
                Assert.That(camera.enabled, Is.True); Assert.That(listener.enabled, Is.True); Assert.That(canvas.enabled, Is.False);
                Assert.That(body.IsCinematicView, Is.False);
                controller.Cleanup();
                Assert.That(camera.enabled, Is.True); Assert.That(listener.enabled, Is.True); Assert.That(canvas.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SciFiTimelineHasFiveContinuousShotsAndThirtySeconds()
        {
            var definition = AssetDatabase.LoadAssetAtPath<OpeningCutsceneDefinition>("Assets/_Project/Cinematics/SciFiOpeningDefinition.asset");
            Assert.That(definition, Is.Not.Null, "Run Build SciFi Opening before asset validation.");
            Assert.That(definition.IsPlayable, Is.True);
            Assert.That(definition.Duration, Is.EqualTo(30).Within(0.01f));
            var track = definition.timeline.GetOutputTracks().OfType<AnimationTrack>().Single();
            var clips = track.GetClips().ToArray();
            Assert.That(clips.Length, Is.EqualTo(5));
            double time = 0;
            foreach (var clip in clips)
            {
                Assert.That(clip.start, Is.EqualTo(time).Within(0.001));
                Assert.That(((AnimationPlayableAsset)clip.asset).clip, Is.Not.Null);
                time += clip.duration;
            }
            Assert.That(time, Is.EqualTo(definition.Duration).Within(0.01));
        }

        [TestCase("SciFi", 30f)]
        [TestCase("Scene_B", 28f)]
        public void TimelineEvaluatesCameraAtEachAuthoredShot(string sceneName, float duration)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + sceneName + ".unity");
            try
            {
                var controller = scene.GetRootGameObjects().Select(root => root.GetComponent<OpeningCutsceneController>()).First(c => c != null);
                Assert.That(controller.Definition.Duration, Is.EqualTo(duration).Within(0.01));
                double start = 0;
                foreach (var shot in controller.Definition.shots)
                {
                    controller.Director.time = start + shot.duration * 0.5;
                    controller.Director.Evaluate();
                    var expected = Vector3.Lerp(shot.startPosition, shot.endPosition, 0.5f);
                    Assert.That(Vector3.Distance(controller.CinematicCamera.transform.position, expected), Is.LessThan(0.03f), shot.name);
                    start += shot.duration;
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void EachGameplaySceneUsesItsOwnOpeningDefinitionWhenConfigured()
        {
            foreach (string name in new[] { "SciFi", "Scene_A", "Scene_B" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
                try
                {
                    var controllers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<OpeningCutsceneController>(true)).ToArray();
                    Assert.That(controllers.Length, Is.EqualTo(name == "Scene_A" ? 0 : 1));
                    if (controllers.Length == 0) continue;
                    var controller = controllers[0];
                    Assert.That(controller.Definition.sceneName, Is.EqualTo(name));
                    Assert.That(controller.Definition.hidePlayers, Is.EqualTo(name == "Scene_B"));
                    Assert.That(controller.IsPlayable, Is.True);
                    Assert.That(controller.Director.playOnAwake, Is.False);
                    Assert.That(controller.CinematicCamera.enabled, Is.False);
                    Assert.That(controller.GetComponentInChildren<AudioListener>().enabled, Is.False);
                    Assert.That(controller.GetComponentsInChildren<NetworkObject>(true).Length, Is.Zero);
                    Assert.That(controller.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(controller.GetComponentsInChildren<OpeningCutsceneShotAnchor>(true).All(a => a.source != null && a.end != null), Is.True);
                    foreach (var binding in controller.Definition.timeline.outputs)
                        Assert.That(controller.Director.GetGenericBinding(binding.sourceObject), Is.Not.Null);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        [Test]
        public void FusionWeavesSkipAndFallbackRpcInvokers()
        {
            var invokers = typeof(NetworkMatchState).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => System.Attribute.IsDefined(method, typeof(NetworkRpcWeavedInvokerAttribute))).ToArray();
            Assert.That(invokers.Any(method => method.Name.Contains("RpcOpeningSkip")), Is.True);
            Assert.That(invokers.Any(method => method.Name.Contains("RpcOpeningUnavailable")), Is.True);
        }
    }
}
