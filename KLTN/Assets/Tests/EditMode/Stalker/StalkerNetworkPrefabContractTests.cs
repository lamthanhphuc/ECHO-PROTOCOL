using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerNetworkPrefabContractTests
    {
        private const string PrefabPath =
            "Assets/Prefabs/StalkerNetwork.prefab";

        [Test]
        public void StalkerNetwork_HasSolidBodyCollider()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath);

            Assert.That(prefab, Is.Not.Null);

            CapsuleCollider body =
                prefab.GetComponent<CapsuleCollider>();

            Assert.That(
                body,
                Is.Not.Null,
                "StalkerNetwork must have a physical body collider.");

            Assert.That(body.isTrigger, Is.False);
            Assert.That(body.radius, Is.GreaterThanOrEqualTo(0.5f));
            Assert.That(body.height, Is.GreaterThanOrEqualTo(1.8f));

            NavMeshAgent agent =
                prefab.GetComponent<NavMeshAgent>();

            Assert.That(agent, Is.Not.Null);

            Assert.That(
                body.radius,
                Is.LessThanOrEqualTo(agent.radius),
                "Physical collider should not exceed the NavMeshAgent clearance radius.");
        }

        [Test]
        public void LizardPrefab_UsesCorrectAnimatorAndAnimations()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/StalkerNetwork_Lizard.prefab");

            Assert.That(prefab, Is.Not.Null);

            var presenter = System.Array.Find(
                prefab.GetComponents<MonoBehaviour>(),
                component => component != null
                    && component.GetType().FullName ==
                    "EchoProtocol.AI.Stalker.Presentation.StalkerAnimatorPresenter");
            Assert.That(presenter, Is.Not.Null);

            var animator = new SerializedObject(presenter)
                .FindProperty("animator").objectReferenceValue as Animator;

            Assert.That(animator, Is.Not.Null);

            var controller =
                animator.runtimeAnimatorController as AnimatorController;

            Assert.That(controller, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(controller),
                Is.EqualTo("Assets/Animations/Stalker/AC_Stalker_Lizard.controller"));

            foreach (var entry in new[]
            {
                ("Idle1", "idle"),
                ("Walk1", "walk"),
                ("Walk2", "run"),
                ("Bite", "attack1"),
                ("Punch", "attack2")
            })
            {
                var state = System.Array.Find(
                    controller.layers[0].stateMachine.states,
                    child => child.state.name == entry.Item1);

                Assert.That(state.state, Is.Not.Null, entry.Item1);
                var expectedPath = entry.Item1 == "Bite"
                    ? "Assets/Animations/Stalker/LizardRetargeted/Lizard_Creep_Bite.anim"
                    : $"Assets/Hatogame_new/Lizard/Animation/Lizard@{entry.Item2}.FBX";

                Assert.That(
                    AssetDatabase.GetAssetPath(state.state.motion),
                    Is.EqualTo(expectedPath),
                    entry.Item1);
            }
        }

        [Test]
        public void StalkerModelSelection_DefaultsToOriginal()
        {
            var spawnerType = System.Type.GetType(
                "EchoProtocol.Networking.PlayerSpawner, Assembly-CSharp");

            Assert.That(spawnerType, Is.Not.Null);

            var method = spawnerType.GetMethod(
                "GetSelectedStalkerModel",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            Assert.That(
                method.Invoke(null, new object[] { null }),
                Is.EqualTo(0));
        }
    }
}
