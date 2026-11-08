#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
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
        public IEnumerator BaitCaptureFuelAndBurnCycleConsumesExactlyOneAlien() =>
            Run(Create("AlienHuntSceneTests"), nameof(BaitCaptureFuelAndBurnCycleConsumesExactlyOneAlien));

        [UnityTest]
        public IEnumerator DirectorRageUvSupplyAndCatchRunOnStateAuthority()
        {
            purgeFixture = Create("PurgeDirectorTests");
            return Run(purgeFixture, nameof(DirectorRageUvSupplyAndCatchRunOnStateAuthority));
        }

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
