using EchoProtocol.Gameplay;
using EchoProtocol.AI.Stalker;
using EchoProtocol.Networking.Authority;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MatchDifficultyTests
{
    [Test]
    public void HostPublishesSelectedDifficultyAndInvalidSelectionFallsBackToNormal()
    {
        var gameObject = new GameObject("MatchAuthority test");
        try
        {
            var authority = gameObject.AddComponent<MatchAuthorityRuntime>();
            authority.RequestDifficulty(MatchDifficulty.Hard);
            Assert.That((string)authority.BuildHostSessionProperties()[
                MatchAuthorityRuntime.MatchDifficultySessionProperty], Is.EqualTo("HARD"));

            authority.RequestDifficulty((MatchDifficulty)99);
            Assert.That(authority.Difficulty, Is.EqualTo(MatchDifficulty.Normal));
            Assert.That(MatchDifficultyProfiles.Get(MatchDifficulty.Easy).TeamToolsPerZone,
                Is.EqualTo(6));
            Assert.That(MatchDifficultyProfiles.Get(MatchDifficulty.Hard).TeamToolsPerZone,
                Is.EqualTo(5));

            var stalker = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/StalkerNetwork.prefab").GetComponent<StalkerController>();
            var serialized = new SerializedObject(stalker);
            var normal = MatchDifficultyProfiles.Get(MatchDifficulty.Normal);
            Assert.That(normal.PatrolSpeed,
                Is.EqualTo(serialized.FindProperty("patrolSpeed").floatValue));
            Assert.That(normal.ChaseSpeed,
                Is.EqualTo(serialized.FindProperty("chaseSpeed").floatValue));
            Assert.That(normal.DetectionDurationSeconds,
                Is.EqualTo(serialized.FindProperty("detectionDurationSeconds").floatValue));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
