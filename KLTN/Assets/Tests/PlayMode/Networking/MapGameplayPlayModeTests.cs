#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EchoProtocol.Networking.Tests
{
    public sealed class MapGameplayPlayModeTests
    {
        private const string PatrolFixture = "EchoProtocol.AI.Stalker.EditorTools.Tests.Map2StalkerPatrolSmokeTests";
        private object purgeFixture;

        [UnityTest]
        public IEnumerator InvalidRoomRequestsAreRejectedWithoutStartingSession()
        {
            var root = new GameObject("Invalid room request test");
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                var bootstrapType = assembly?.GetType("EchoProtocol.Networking.NetworkBootstrap");
                Assert.That(bootstrapType, Is.Not.Null);
                var bootstrap = root.AddComponent(bootstrapType);
                var createRoom = bootstrapType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .First(method => method.Name == "CreateRoomAsync" && method.GetParameters().Length == 5);
                AssertRejected(assembly, bootstrapType, bootstrap, createRoom, 1, null);
                AssertRejected(assembly, bootstrapType, bootstrap, createRoom, 5, null);
                AssertRejected(assembly, bootstrapType, bootstrap, createRoom, 2, 2);
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
            yield return null;
        }

        private static void AssertRejected(System.Reflection.Assembly assembly, Type bootstrapType, Component bootstrap, MethodInfo createRoom,
            int maxPlayers, int? gameMode)
        {
            var gameplay = assembly.GetType("EchoProtocol.Gameplay.MatchDifficulty");
            var config = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name == "EchoProtocol.AI.Common" ? a : null).First(a => a != null);
            var difficulty = Enum.ToObject(gameplay, 0);
            var resolution = Enum.ToObject(config.GetType("EchoProtocol.AI.Common.AED.ScenarioResolutionMode"), 0);
            var task = (System.Threading.Tasks.Task<bool>)createRoom.Invoke(bootstrap,
                new object[] { "invalid-room", maxPlayers, difficulty, gameMode, resolution });
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.Result, Is.False);
            Assert.That(bootstrapType.GetProperty("State").GetValue(bootstrap).ToString(), Is.EqualTo("Disconnected"));
            Assert.That(bootstrapType.GetProperty("Runner").GetValue(bootstrap), Is.Null);
        }

        [UnityTest]
        public IEnumerator BaitCaptureFuelAndBurnCycleConsumesExactlyOneAlien() =>
            Run(Create("AlienHuntSceneTests"), nameof(BaitCaptureFuelAndBurnCycleConsumesExactlyOneAlien));

        [UnityTest]
        public IEnumerator DirectorRageUvSupplyAndCatchRunOnStateAuthority()
        {
            purgeFixture = Create("PurgeDirectorTests");
            return Run(purgeFixture, nameof(DirectorRageUvSupplyAndCatchRunOnStateAuthority));
        }

        [UnityTest]
        public IEnumerator Map2_FlyingBeetlePatrolMovesOnSceneNavMesh() =>
            Run(Create(PatrolFixture), nameof(Map2_FlyingBeetlePatrolMovesOnSceneNavMesh));

        [UnityTest]
        public IEnumerator Map2_LizardPrefabPatrolMovesOnSceneNavMesh() =>
            Run(Create(PatrolFixture), nameof(Map2_LizardPrefabPatrolMovesOnSceneNavMesh));

        [UnityTest]
        public IEnumerator Map2_HuntLizardPatrolMovesOnItsOwnSceneNavMesh() =>
            Run(Create(PatrolFixture), nameof(Map2_HuntLizardPatrolMovesOnItsOwnSceneNavMesh));

        [UnityTest]
        public IEnumerator Map2_LizardPatrolMovesFromReportedStuckLocation() =>
            Run(Create(PatrolFixture), nameof(Map2_LizardPatrolMovesFromReportedStuckLocation));

        [UnityTest]
        public IEnumerator Map2_LizardFallbackReturnsFromReportedIsolatedStart() =>
            Run(Create(PatrolFixture), nameof(Map2_LizardFallbackReturnsFromReportedIsolatedStart));

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (purgeFixture != null)
            {
                yield return Run(purgeFixture, "Cleanup");
                purgeFixture = null;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Scene_A.unity" && scene.path != "Assets/Scenes/Scene_B.unity")
                yield break;

            SceneManager.SetActiveScene(SceneManager.CreateScene("MapGameplayTestCleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        // The implementations use AssetDatabase and types in the predefined Assembly-CSharp.
        // An asmdef cannot reference that assembly directly, so dispatch to the typed editor code.
        private static object Create(string typeName) =>
            Activator.CreateInstance(Type.GetType(typeName + ", Assembly-CSharp-Editor", throwOnError: true));

        private static IEnumerator Run(object fixture, string methodName)
        {
            var method = fixture.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, methodName);
            return (IEnumerator)method.Invoke(fixture, null);
        }
    }
}
#endif
