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
            Assert.That(MatchDifficultyProfiles.Get(MatchDifficulty.Easy).ObjectiveInvestigationEnabled,
                Is.False);
            Assert.That(MatchDifficultyProfiles.Get(MatchDifficulty.Hard).TeamToolsPerZone,
                Is.EqualTo(5));
            Assert.That(MatchDifficultyProfiles.Get(MatchDifficulty.Hard).ObjectiveInvestigationEnabled,
                Is.True);

            var stalker = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/StalkerNetwork.prefab").GetComponent<StalkerController>();
            var serialized = new SerializedObject(stalker);
            var easy = MatchDifficultyProfiles.Get(MatchDifficulty.Easy);
            var normal = MatchDifficultyProfiles.Get(MatchDifficulty.Normal);
            var hard = MatchDifficultyProfiles.Get(MatchDifficulty.Hard);
            Assert.That(easy.DetectionDecayDurationSeconds, Is.EqualTo(0.35f));
            Assert.That(normal.DetectionDecayDurationSeconds, Is.EqualTo(0.65f));
            Assert.That(hard.DetectionDecayDurationSeconds, Is.EqualTo(1f));
            Assert.That(easy.MaximumRevivesPerZone, Is.EqualTo(4));
            Assert.That(normal.MaximumRevivesPerZone, Is.EqualTo(3));
            Assert.That(hard.MaximumRevivesPerZone, Is.EqualTo(2));
            Assert.That(hard.PatrolSpeed,
                Is.EqualTo(serialized.FindProperty("patrolSpeed").floatValue));
            Assert.That(hard.ChaseSpeed,
                Is.EqualTo(serialized.FindProperty("chaseSpeed").floatValue));
            Assert.That(hard.DetectionDurationSeconds,
                Is.EqualTo(serialized.FindProperty("detectionDurationSeconds").floatValue));
            Assert.That(normal.ChaseSpeed, Is.LessThan(hard.ChaseSpeed));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
