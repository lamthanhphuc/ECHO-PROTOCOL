using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.UI.Tests
{
    public sealed class LobbyMapBackgroundTests
    {
        private const string ScenePath = "Assets/Scenes/Lobby.unity";
        private static System.Type BackgroundType =>
            System.Type.GetType("EchoProtocol.UI.LobbyMapBackground, Assembly-CSharp", true);

        [Test]
        public void CapturedSceneLightingDoesNotCacheTransientAmbientProbe()
        {
            TypeInfo stateType = BackgroundType
                .GetNestedType("EnvironmentState", BindingFlags.NonPublic)
                ?.GetTypeInfo();
            Assert.That(stateType, Is.Not.Null);

            MethodInfo capture = stateType.GetMethod("Capture", BindingFlags.Public | BindingFlags.Static);
            FieldInfo probe = stateType.GetField("AmbientProbe", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(capture, Is.Not.Null);
            Assert.That(probe, Is.Not.Null);

            object state = capture.Invoke(null, null);
            Assert.That(probe.GetValue(state), Is.Null,
                "Scene lighting snapshots must let Unity rebuild the ambient probe.");
        }

        [Test]
        public void RoomBoundaryRestoreReturnsToSciFiCameraAndEnvironment()
        {
            Scene previous = SceneManager.GetActiveScene();
            string previousPath = previous.path;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            try
            {
                var background = Object.FindAnyObjectByType(BackgroundType) as Component;
                Assert.That(background, Is.Not.Null);

                Invoke(background, "OnEnable");
                BackgroundType.GetMethod("ApplyMap").Invoke(background, new object[] { 1 });
                Invoke(background, "RestorePresentation");

                GameObject sciFi = Field<GameObject>(background, "sciFiEnvironment");
                GameObject flooded = Field<GameObject>(background, "floodedEnvironment");
                Camera floodedCamera = Field<Camera>(background, "backgroundCamera");
                Assert.That(sciFi.activeSelf, Is.True);
                Assert.That(flooded.activeSelf, Is.False);
                Assert.That(floodedCamera.enabled, Is.False);
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousPath) && previousPath != scene.path)
                    EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
            }
        }

        private static void Invoke(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(info, Is.Not.Null);
            info.Invoke(target, null);
        }

        private static T Field<T>(object target, string name) where T : Object
        {
            FieldInfo info = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(info, Is.Not.Null);
            return info.GetValue(target) as T;
        }
    }
}
