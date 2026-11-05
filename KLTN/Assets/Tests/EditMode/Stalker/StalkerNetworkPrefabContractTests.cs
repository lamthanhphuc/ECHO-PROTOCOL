using NUnit.Framework;
using UnityEditor;
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
    }
}
