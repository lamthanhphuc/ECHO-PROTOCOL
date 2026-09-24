using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerJumpEntrySelectorTests
    {
        private const string SelectorTypeName =
            "EchoProtocol.AI.Stalker.Special.StalkerJumpEntrySelector";

        private readonly List<GameObject> _createdObjects =
            new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _createdObjects.Count - 1;
                 i >= 0;
                 i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        _createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void STK_SPECIAL_ENTRY_001_FrontPreferredDistance_IsEligible()
        {
            var player =
                CreatePlayer(
                    "Player",
                    Vector3.zero,
                    Vector3.forward);

            var players =
                new List<Transform>
                {
                    player
                };

            var eligible =
                InvokeTryScorePosition(
                    new Vector3(0f, 0f, 8.5f),
                    players,
                    7f,
                    10f,
                    0.5f,
                    8.5f,
                    8f,
                    1.2f,
                    out var score);

            Assert.That(
                eligible,
                Is.True);

            Assert.That(
                score,
                Is.GreaterThan(
                    float.NegativeInfinity));
        }

        [Test]
        public void STK_SPECIAL_ENTRY_ZoneFilter_SelectsAllowedCandidate()
        {
            var buildSettings = NavMesh.GetSettingsByID(0);
            Assert.That(buildSettings.agentTypeID, Is.Not.EqualTo(-1));
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(new Vector3(0f, -0.05f, 0f),
                        Quaternion.identity, Vector3.one),
                    size = new Vector3(24f, 0.1f, 24f),
                    area = 0
                }
            };
            var data = NavMeshBuilder.BuildNavMeshData(buildSettings, sources,
                new Bounds(Vector3.zero, new Vector3(26f, 4f, 26f)),
                Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            var instance = NavMesh.AddNavMeshData(data);
            try
            {
                var selector = ResolveProductionType(SelectorTypeName);
                var settingsType = ResolveProductionType(
                    "EchoProtocol.AI.Stalker.Special.StalkerSpecialEncounterSettings");
                var settings = Activator.CreateInstance(settingsType);
                settingsType.GetField("requireEscapeRoute",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(settings, false);
                var method = Array.Find(selector.GetMethods(), candidate =>
                    candidate.Name == "TrySelectWithFairness"
                    && candidate.GetParameters().Length == 10);
                Assert.That(method, Is.Not.Null);
                var player = CreatePlayer("Player", Vector3.zero, Vector3.forward);
                var arguments = new object[]
                {
                    null, new List<Transform> { player }, null,
                    Activator.CreateInstance(ResolveProductionType(
                        "EchoProtocol.AI.Common.PlayerId")),
                    Activator.CreateInstance(ResolveProductionType(
                        "EchoProtocol.AI.Common.AiSimulationTime"), 1L, 1d),
                    settings, null, NavMesh.AllAreas,
                    new Func<Vector3, bool>(position => position.x > 2f),
                    Vector3.zero
                };

                Assert.That((bool)method.Invoke(null, arguments), Is.True);
                Assert.That(((Vector3)arguments[9]).x, Is.GreaterThan(2f));
            }
            finally
            {
                instance.Remove();
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void STK_SPECIAL_ENTRY_002_BehindPlayer_IsRejected()
        {
            var player =
                CreatePlayer(
                    "Player",
                    Vector3.zero,
                    Vector3.forward);

            var players =
                new List<Transform>
                {
                    player
                };

            var eligible =
                InvokeTryScorePosition(
                    new Vector3(0f, 0f, -8.5f),
                    players,
                    7f,
                    10f,
                    0.5f,
                    8.5f,
                    8f,
                    1.2f,
                    out _);

            Assert.That(
                eligible,
                Is.False);
        }

        [Test]
        public void STK_SPECIAL_ENTRY_003_OutsideDistanceBand_IsRejected()
        {
            var player =
                CreatePlayer(
                    "Player",
                    Vector3.zero,
                    Vector3.forward);

            var players =
                new List<Transform>
                {
                    player
                };

            var tooClose =
                InvokeTryScorePosition(
                    new Vector3(0f, 0f, 6.5f),
                    players,
                    7f,
                    10f,
                    0.5f,
                    8.5f,
                    8f,
                    1.2f,
                    out _);

            var tooFar =
                InvokeTryScorePosition(
                    new Vector3(0f, 0f, 10.5f),
                    players,
                    7f,
                    10f,
                    0.5f,
                    8.5f,
                    8f,
                    1.2f,
                    out _);

            Assert.That(
                tooClose,
                Is.False);

            Assert.That(
                tooFar,
                Is.False);
        }

        [Test]
        public void STK_SPECIAL_ENTRY_004_OccludedCandidate_IsRejected()
        {
            var player =
                CreatePlayer(
                    "Player",
                    Vector3.zero,
                    Vector3.forward);

            var players =
                new List<Transform>
                {
                    player
                };

            var blocker =
                new GameObject(
                    "JumpIn_LOS_Blocker");

            _createdObjects.Add(
                blocker);

            blocker.transform.position =
                new Vector3(
                    0f,
                    1.2f,
                    4.25f);

            blocker.transform.localScale =
                new Vector3(
                    2f,
                    2f,
                    0.5f);

            blocker.AddComponent<BoxCollider>();

            Physics.SyncTransforms();

            var eligible =
                InvokeTryScorePosition(
                    new Vector3(0f, 0f, 8.5f),
                    players,
                    7f,
                    10f,
                    0.5f,
                    8.5f,
                    8f,
                    1.2f,
                    out _);

            Assert.That(
                eligible,
                Is.False);
        }

        [Test]
        public void STK_SPECIAL_ENTRY_005_PreferredDistance_ScoresHigherThanBoundary()
        {
            var player =
                CreatePlayer(
                    "Player",
                    Vector3.zero,
                    Vector3.forward);

            var players =
                new List<Transform>
                {
                    player
                };

            var preferredEligible =
                InvokeTryScorePosition(
                    new Vector3(0f, 0f, 8.5f),
                    players,
                    7f,
                    10f,
                    0.5f,
                    8.5f,
                    8f,
                    1.2f,
                    out var preferredScore);

            var boundaryEligible =
                InvokeTryScorePosition(
                    new Vector3(0f, 0f, 7f),
                    players,
                    7f,
                    10f,
                    0.5f,
                    8.5f,
                    8f,
                    1.2f,
                    out var boundaryScore);

            Assert.That(
                preferredEligible,
                Is.True);

            Assert.That(
                boundaryEligible,
                Is.True);

            Assert.That(
                preferredScore,
                Is.GreaterThan(
                    boundaryScore));
        }

        private Transform CreatePlayer(
            string name,
            Vector3 position,
            Vector3 forward)
        {
            var gameObject =
                new GameObject(
                    name);

            _createdObjects.Add(
                gameObject);

            gameObject.transform.position =
                position;

            var horizontalForward =
                forward;

            horizontalForward.y = 0f;

            if (horizontalForward.sqrMagnitude
                <= 0.0001f)
            {
                horizontalForward =
                    Vector3.forward;
            }

            gameObject.transform.rotation =
                Quaternion.LookRotation(
                    horizontalForward.normalized,
                    Vector3.up);

            return gameObject.transform;
        }

        private static bool InvokeTryScorePosition(
            Vector3 position,
            IReadOnlyList<Transform> alivePlayers,
            float minDistance,
            float maxDistance,
            float frontFacingDotThreshold,
            float preferredDistance,
            float groupRadius,
            float candidateLosHeight,
            out float score)
        {
            var selectorType =
                ResolveProductionType(
                    SelectorTypeName);

            var method =
                selectorType.GetMethod(
                    "TryScorePosition",
                    BindingFlags.NonPublic
                    | BindingFlags.Static);

            Assert.That(
                method,
                Is.Not.Null);

            var arguments =
                new object[]
                {
                    position,
                    alivePlayers,
                    minDistance,
                    maxDistance,
                    frontFacingDotThreshold,
                    preferredDistance,
                    groupRadius,
                    candidateLosHeight,
                    0f
                };

            var result =
                method.Invoke(
                    null,
                    arguments);

            Assert.That(
                result,
                Is.TypeOf<bool>());

            score =
                (float)arguments[8];

            return (bool)result;
        }

        private static Type ResolveProductionType(
            string fullName)
        {
            foreach (var assembly
                     in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type =
                    assembly.GetType(
                        fullName,
                        false);

                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail(
                $"Missing production type '{fullName}'.");

            return null;
        }
    }
}
