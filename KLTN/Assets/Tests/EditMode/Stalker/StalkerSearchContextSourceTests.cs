using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerSearchContextSourceTests
    {
        private const string ContextTypeName =
            "EchoProtocol.AI.Stalker.StalkerSearchContext";

        private const string EpisodeIdTypeName =
            "EchoProtocol.AI.Stalker.SearchEpisodeId";

        private const string SearchSourceTypeName =
            "EchoProtocol.AI.Stalker.StalkerSearchSource";

        private const string SimulationTimeTypeName =
            "EchoProtocol.AI.Common.AiSimulationTime";

        private const string RegionIdTypeName =
            "EchoProtocol.AI.Common.Spatial.RegionId";

        [Test]
        public void LegacyConstructor_DefaultsToVisualTargetLoss()
        {
            var origin =
                new Vector3(4f, 0f, 8f);

            var context =
                CreateLegacyContext(origin);

            Assert.That(
                Read(context, "Source").ToString(),
                Is.EqualTo("VisualTargetLoss"));

            Assert.That(
                Read<Vector3>(
                    context,
                    "SearchOriginPosition"),
                Is.EqualTo(origin));

            Assert.That(
                Read<Vector3>(
                    context,
                    "SearchOriginLKP"),
                Is.EqualTo(origin));
        }

        [Test]
        public void HeardNoiseConstructor_StoresNoiseSearchSource()
        {
            var origin =
                new Vector3(12f, 0f, -5f);

            var context =
                CreateContext(
                    "HeardNoise",
                    origin);

            Assert.That(
                Read(context, "Source").ToString(),
                Is.EqualTo("HeardNoise"));

            Assert.That(
                Read<Vector3>(
                    context,
                    "SearchOriginPosition"),
                Is.EqualTo(origin));

            Assert.That(
                Read<Vector3>(
                    context,
                    "SearchOriginLKP"),
                Is.EqualTo(origin));
        }

        [Test]
        public void SearchOriginLKP_RemainsBackwardCompatibleAlias()
        {
            var origin =
                new Vector3(-3f, 1f, 9f);

            var context =
                CreateContext(
                    "HeardNoise",
                    origin);

            var genericOrigin =
                Read<Vector3>(
                    context,
                    "SearchOriginPosition");

            var legacyOrigin =
                Read<Vector3>(
                    context,
                    "SearchOriginLKP");

            Assert.That(
                legacyOrigin,
                Is.EqualTo(genericOrigin));
        }

        private static object CreateLegacyContext(
            Vector3 origin)
        {
            var contextType =
                ResolveType(ContextTypeName);

            var episodeId =
                Activator.CreateInstance(
                    ResolveType(EpisodeIdTypeName),
                    new object[]
                    {
                        1L
                    });

            var simulationTime =
                Activator.CreateInstance(
                    ResolveType(SimulationTimeTypeName),
                    new object[]
                    {
                        10L,
                        1.0d
                    });

            var regionId =
                Activator.CreateInstance(
                    ResolveType(RegionIdTypeName),
                    new object[]
                    {
                        1
                    });

            var constructor =
                contextType.GetConstructor(
                    new[]
                    {
                        ResolveType(EpisodeIdTypeName),
                        typeof(Vector3),
                        typeof(Vector3),
                        ResolveType(SimulationTimeTypeName),
                        ResolveType(RegionIdTypeName)
                    });

            Assert.That(
                constructor,
                Is.Not.Null,
                "Legacy StalkerSearchContext constructor is missing.");

            return constructor.Invoke(
                new[]
                {
                    episodeId,
                    origin,
                    Vector3.forward,
                    simulationTime,
                    regionId
                });
        }

        private static object CreateContext(
            string sourceName,
            Vector3 origin)
        {
            var contextType =
                ResolveType(ContextTypeName);

            var sourceType =
                ResolveType(SearchSourceTypeName);

            var source =
                Enum.Parse(
                    sourceType,
                    sourceName);

            var episodeId =
                Activator.CreateInstance(
                    ResolveType(EpisodeIdTypeName),
                    new object[]
                    {
                        2L
                    });

            var simulationTime =
                Activator.CreateInstance(
                    ResolveType(SimulationTimeTypeName),
                    new object[]
                    {
                        20L,
                        2.0d
                    });

            var regionId =
                Activator.CreateInstance(
                    ResolveType(RegionIdTypeName),
                    new object[]
                    {
                        1
                    });

            var constructor =
                contextType.GetConstructor(
                    new[]
                    {
                        ResolveType(EpisodeIdTypeName),
                        sourceType,
                        typeof(Vector3),
                        typeof(Vector3),
                        ResolveType(SimulationTimeTypeName),
                        ResolveType(RegionIdTypeName)
                    });

            Assert.That(
                constructor,
                Is.Not.Null,
                "Source-aware StalkerSearchContext constructor is missing.");

            return constructor.Invoke(
                new[]
                {
                    episodeId,
                    source,
                    origin,
                    Vector3.forward,
                    simulationTime,
                    regionId
                });
        }

        private static object Read(
            object target,
            string propertyName)
        {
            var property =
                target.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Public |
                    BindingFlags.Instance);

            Assert.That(
                property,
                Is.Not.Null,
                $"Missing property '{propertyName}'.");

            return property.GetValue(target);
        }

        private static T Read<T>(
            object target,
            string propertyName)
        {
            return (T)Read(
                target,
                propertyName);
        }

        private static Type ResolveType(
            string fullTypeName)
        {
            foreach (var assembly
                     in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type =
                    assembly.GetType(
                        fullTypeName,
                        false);

                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail(
                $"Could not resolve type '{fullTypeName}'.");

            return null;
        }
    }
}
