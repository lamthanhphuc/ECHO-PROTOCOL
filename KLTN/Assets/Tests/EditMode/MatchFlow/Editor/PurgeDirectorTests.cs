using System.Reflection;
using EchoProtocol.Networking;
using EchoProtocol.Gameplay;
using EchoProtocol.AI.Stalker;
using EchoProtocol.AI.Stalker.Networking;
using EchoProtocol.AI.Stalker.Spatial;
using EchoProtocol.Tools.Scanner;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Assert=NUnit.Framework.Assert;

public sealed class PurgeDirectorTests
{
    [Test]
    public void PurgeDetectionTimesMatchRageTiers()
    {
        var methodFill = typeof(StalkerController).GetMethod("GetDetectionFillRate", BindingFlags.NonPublic | BindingFlags.Instance);
        var methodDecay = typeof(StalkerController).GetMethod("GetDetectionDecayRate", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(methodFill, Is.Not.Null);
        Assert.That(methodDecay, Is.Not.Null);
    }

}

namespace EchoProtocol.Networking.Tests
{
    public sealed class PurgeFishcatBalanceTests
    {
        [TestCase(1, MatchDifficulty.Easy, 1f)]
        [TestCase(1, MatchDifficulty.Normal, 1f)]
        [TestCase(1, MatchDifficulty.Hard, 1f)]
        [TestCase(2, MatchDifficulty.Easy, 1f)]
        [TestCase(2, MatchDifficulty.Normal, 1f)]
        [TestCase(4, MatchDifficulty.Hard, 1f)]
        public void FishcatUvThresholdIsOneSecond(int partySize, MatchDifficulty difficulty, float expected)
        {
            Assert.That(PurgeFishcatBalance.UvDeathSeconds(partySize, difficulty), Is.EqualTo(expected));
        }

        [Test]
        public void RefillAllowsOneSpawnThenWaitsAndFailedPlacementCanRetry()
        {
            var schedule = new PurgeFishcatRefillSchedule();
            Assert.That(schedule.IsDue(0f, 0, 3, 35f), Is.True);
            schedule.Spawned(0f);
            Assert.That(schedule.IsDue(0f, 1, 3, 35f), Is.False);
            Assert.That(schedule.IsDue(34.9f, 1, 3, 35f), Is.False);
            Assert.That(schedule.IsDue(35f, 1, 3, 35f), Is.True);
            Assert.That(schedule.IsDue(36f, 1, 3, 35f), Is.True, "Failed placement must not restart a full cooldown.");
            schedule.Spawned(36f);
            Assert.That(schedule.IsDue(36f, 2, 3, 35f), Is.False);
        }

        [Test]
        public void KillStartsCooldownAtLossAndTierGrowthDoesNotFillImmediately()
        {
            var schedule = new PurgeFishcatRefillSchedule();
            schedule.IsDue(0f, 0, 1, 35f);
            schedule.Spawned(0f);
            Assert.That(schedule.IsDue(100f, 1, 1, 35f), Is.False);
            Assert.That(schedule.IsDue(101f, 0, 1, 35f), Is.False);
            Assert.That(schedule.IsDue(135.9f, 0, 1, 35f), Is.False);
            Assert.That(schedule.IsDue(136f, 0, 1, 35f), Is.True);
            schedule.Spawned(136f);
            Assert.That(schedule.IsDue(136f, 1, 8, 16f), Is.False);
            Assert.That(schedule.IsDue(152f, 1, 8, 16f), Is.True);
            schedule.Spawned(152f);
            Assert.That(schedule.IsDue(152f, 2, 8, 16f), Is.False);
            schedule.Reset();
            Assert.That(schedule.IsDue(0f, 0, 1, 35f), Is.True);
        }

        [TestCase(0, 1, 1)] [TestCase(0, 2, 2)] [TestCase(0, 3, 2)] [TestCase(0, 4, 3)]
        [TestCase(3, 1, 2)] [TestCase(3, 2, 3)] [TestCase(3, 3, 4)] [TestCase(3, 4, 5)]
        [TestCase(6, 1, 3)] [TestCase(6, 2, 4)] [TestCase(6, 3, 5)] [TestCase(6, 4, 6)]
        [TestCase(8, 1, 3)] [TestCase(8, 2, 5)] [TestCase(8, 3, 6)] [TestCase(8, 4, 8)]
        public void NormalPopulationMatchesApprovedPartyAndProgressTable(int burned, int players, int expected)
        {
            Assert.That(PurgeFishcatBalance.Cap(burned, players, MatchDifficulty.Normal), Is.EqualTo(expected));
        }

        [TestCase(0, 35f)] [TestCase(2, 35f)] [TestCase(3, 28f)] [TestCase(5, 28f)]
        [TestCase(6, 22f)] [TestCase(7, 22f)] [TestCase(8, 16f)]
        public void RefillCooldownChangesOnlyAtApprovedProgressBoundaries(int burned, float expected)
        {
            Assert.That(PurgeFishcatBalance.SpawnCooldown(burned), Is.EqualTo(expected));
        }

        [Test]
        public void PopulationRemainsBoundedAndDifficultyNeverReversesPressure()
        {
            Assert.That(PurgeFishcatBalance.Cap(0, 0, MatchDifficulty.Normal), Is.EqualTo(1));
            Assert.That(PurgeFishcatBalance.Cap(8, 99, MatchDifficulty.Normal), Is.EqualTo(8));
            for (int players = 1; players <= 4; players++)
            {
                int previous = 0;
                for (int burned = 0; burned <= 8; burned++)
                {
                    int normal = PurgeFishcatBalance.Cap(burned, players, MatchDifficulty.Normal);
                    Assert.That(normal, Is.GreaterThanOrEqualTo(previous));
                    Assert.That(PurgeFishcatBalance.Cap(burned, players, MatchDifficulty.Easy), Is.InRange(1, normal));
                    Assert.That(PurgeFishcatBalance.Cap(burned, players, MatchDifficulty.Hard), Is.GreaterThanOrEqualTo(normal));
                    previous = normal;
                }
            }
        }
    }

}
