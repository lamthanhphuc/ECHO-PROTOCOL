using System;
using NUnit.Framework;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerPatrolDirectorPacingTests
    {
        [Test]
        public void STK_DIRECTOR_PACING_002_SeeksAfterThreeMinutesAndResetsOnDetection()
        {
            var directorType = ResolveProductionType(
                "EchoProtocol.AI.Stalker.Spatial.Strategic.StalkerPatrolDirector");
            var settings = Activator.CreateInstance(ResolveProductionType(
                "EchoProtocol.AI.Stalker.Spatial.Strategic.StalkerSmartPatrolSettings"));
            var director = Activator.CreateInstance(directorType, settings);
            var stateType = ResolveProductionType("EchoProtocol.AI.Stalker.StalkerState");
            var update = directorType.GetMethod("Update");
            var shouldSeek = directorType.GetProperty("ShouldSeekPlayers");
            Assert.That(update, Is.Not.Null);
            Assert.That(shouldSeek, Is.Not.Null);

            var patrol = Enum.Parse(stateType, "PATROL");
            var detect = Enum.Parse(stateType, "DETECT");
            update.Invoke(director, new[] { patrol, null, null, (object)10d });
            update.Invoke(director, new[] { patrol, null, null, (object)189d });
            Assert.That(shouldSeek.GetValue(director), Is.False);
            update.Invoke(director, new[] { patrol, null, null, (object)190d });
            Assert.That(shouldSeek.GetValue(director), Is.True);
            update.Invoke(director, new[] { detect, null, null, (object)191d });
            Assert.That(shouldSeek.GetValue(director), Is.False);
            update.Invoke(director, new[] { patrol, null, null, (object)371d });
            Assert.That(shouldSeek.GetValue(director), Is.True);
        }

        private const string DirectorTypeName =
            "EchoProtocol.AI.Stalker.Spatial.Strategic.StalkerPatrolDirector";

        private const string SettingsTypeName =
            "EchoProtocol.AI.Stalker.Spatial.Strategic.StalkerSmartPatrolSettings";

        private const string StateTypeName =
            "EchoProtocol.AI.Stalker.StalkerState";

        [Test]
        public void STK_DIRECTOR_PACING_001_ActiveCooldown_CannotBeBypassedByAttack()
        {
            var directorType =
                ResolveProductionType(
                    DirectorTypeName);

            var settingsType =
                ResolveProductionType(
                    SettingsTypeName);

            var stateType =
                ResolveProductionType(
                    StateTypeName);

            var settings =
                Activator.CreateInstance(
                    settingsType);

            var director =
                Activator.CreateInstance(
                    directorType,
                    settings);

            var recordMethod =
                directorType.GetMethod(
                    "RecordMajorEncounter");

            var updateMethod =
                directorType.GetMethod(
                    "Update");

            var canStartMethod =
                directorType.GetMethod(
                    "CanStartMajorEncounter");

            Assert.That(recordMethod, Is.Not.Null);
            Assert.That(updateMethod, Is.Not.Null);
            Assert.That(canStartMethod, Is.Not.Null);

            recordMethod.Invoke(
                director,
                new object[]
                {
                    0d,
                    20f
                });

            var attackState =
                Enum.Parse(
                    stateType,
                    "ATTACK");

            //
            // Attack changes the displayed pacing Mode to Pressure,
            // but the real 20-second cooldown is still active.
            //
            updateMethod.Invoke(
                director,
                new[]
                {
                    attackState,
                    null,
                    null,
                    (object)10d
                });

            var canStartDuringCooldown =
                (bool)canStartMethod.Invoke(
                    director,
                    new object[]
                    {
                        1f
                    });

            Assert.That(
                canStartDuringCooldown,
                Is.False);

            //
            // Once the real cooldown deadline has passed,
            // the encounter becomes eligible again.
            //
            updateMethod.Invoke(
                director,
                new[]
                {
                    attackState,
                    null,
                    null,
                    (object)21d
                });

            var canStartAfterCooldown =
                (bool)canStartMethod.Invoke(
                    director,
                    new object[]
                    {
                        1f
                    });

            Assert.That(
                canStartAfterCooldown,
                Is.True);
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
                $"Production type not found: {fullName}");

            return null;
        }
    }
}
