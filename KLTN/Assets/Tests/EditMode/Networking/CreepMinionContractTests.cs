using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Networking.Tests
{
    public sealed class CreepMinionContractTests
    {
        private const string MinionSource = "Assets/Scripts/AI/Minions/CreepMinionRuntime.cs";
        private const string SpawnerSource = "Assets/_Project/Scripts/Networking/Player/PlayerSpawner.cs";

        [Test]
        public void CreepAlert_HasRejectedAlertRetry()
        {
            string source = File.ReadAllText(MinionSource);
            StringAssert.Contains("_stalkerAlertDeliveredForTarget", source);
            StringAssert.Contains("alertRetrySeconds", source);
            StringAssert.Contains("TrySendStalkerAlert", source);
        }

        [Test]
        public void CreepSabotageMovement_RequiresCompletePaths()
        {
            string source = File.ReadAllText(MinionSource);
            string toolRelocation = ExtractMethod(source,
                "private bool TryFindSabotageDropPosition",
                "private bool TryStealCore");
            string coreFlee = ExtractMethod(source,
                "private bool TryFindFleeDestination",
                "public void ReleaseStolenCoreAuthoritative");

            StringAssert.Contains("NavMesh.CalculatePath", toolRelocation);
            StringAssert.Contains("NavMeshPathStatus.PathComplete", toolRelocation);
            StringAssert.Contains("NavMesh.CalculatePath", coreFlee);
            StringAssert.Contains("NavMeshPathStatus.PathComplete", coreFlee);
        }

        [Test]
        public void Zone2Minions_UseZone2StalkerAsAnchor()
        {
            string source = File.ReadAllText(SpawnerSource);
            StringAssert.Contains("TryGetCreepMinionSpawnAnchor(zone, out var stalker)", source);
            StringAssert.Contains("? _zone2MonsterInstance", source);
            StringAssert.Contains(": _monsterInstance;", source);
            StringAssert.Contains("NavMesh.SamplePosition(", source);
        }

        [Test]
        public void CreepPrefab_SerializesAlertRetrySeconds()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/PF_CreepMinionNetwork.prefab");
            Assert.That(prefab, Is.Not.Null);

            var minion = FindComponentByTypeName(
                prefab,
                "EchoProtocol.AI.Minions.CreepMinionRuntime");
            Assert.That(minion, Is.Not.Null);

            var retry = new SerializedObject(minion).FindProperty("alertRetrySeconds");
            Assert.That(retry, Is.Not.Null);
            Assert.That(retry.floatValue, Is.EqualTo(0.15f).Within(0.001f));
        }

        [Test]
        public void UnifiedMinion_ContainsAllMechanics()
        {
            string source = File.ReadAllText(MinionSource);

            StringAssert.Contains("CreepMinionAttackKind.ShootSlow", source);
            StringAssert.Contains("CreepMinionAttackKind.StealTool", source);
            StringAssert.Contains("CreepMinionAttackKind.StealCore", source);
            StringAssert.Contains("TryApplySlowAuthoritative", source);
            StringAssert.Contains("TryTransferTeamToolToMonster", source);
            StringAssert.Contains("TryStealCore", source);
            StringAssert.Contains("BeginFleeTo", source);
            StringAssert.Contains("TrySendStalkerAlert", source);
            StringAssert.Contains("RuntimeNoiseType.NOISE_MAKER", source);
            StringAssert.Contains("IsStabilizerBuffed", source);
            StringAssert.Contains("BeginFlashlightDeath", source);
        }

        [Test]
        public void BothZones_UseSameUnifiedMinionPrefab()
        {
            string source = File.ReadAllText(SpawnerSource);

            StringAssert.Contains("ResolveCreepMinionPrefab()", source);
            StringAssert.Contains("ConfigureBeforeSpawn(zone)", source);
            StringAssert.Contains("GetComponent<CreepMinionRuntime>()", source);
        }

        private static Component FindComponentByTypeName(GameObject root, string fullTypeName)
        {
            var components = root.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null && component.GetType().FullName == fullTypeName) return component;
            }

            return null;
        }

        private static string ExtractMethod(string source, string signature, string nextSignature)
        {
            int start = source.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing method: {signature}");
            int end = source.IndexOf(nextSignature, start, System.StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), $"Missing method boundary: {nextSignature}");
            return source.Substring(start, end - start);
        }
    }
}
