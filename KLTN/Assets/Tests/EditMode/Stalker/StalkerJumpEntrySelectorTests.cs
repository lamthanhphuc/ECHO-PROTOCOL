using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

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
