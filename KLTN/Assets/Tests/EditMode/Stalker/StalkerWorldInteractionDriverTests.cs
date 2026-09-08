using System;
using System.IO;
using NUnit.Framework;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerWorldInteractionDriverTests
    {
        private const string DriverPath = "Assets/Scripts/AI/Stalker/StalkerWorldInteractionDriver.cs";
        private const string ControllerPath = "Assets/Scripts/AI/Stalker/StalkerController.cs";
        private const string StalkerStatePath = "Assets/Scripts/AI/Stalker/StalkerState.cs";

        [Test]
        public void STK_WorldInteraction_CanonicalFsmStatesRemainUnchanged()
        {
            var source = File.ReadAllText(StalkerStatePath);

            StringAssert.Contains("PATROL", source);
            StringAssert.Contains("DETECT", source);
            StringAssert.Contains("CHASE", source);
            StringAssert.Contains("ATTACK", source);
            StringAssert.Contains("RECOVER", source);
            StringAssert.Contains("SEARCH", source);
            StringAssert.DoesNotContain("OPEN_DOOR", source);
            StringAssert.DoesNotContain("BREAK_DOOR", source);
            StringAssert.DoesNotContain("BREAK_JAMMER", source);
        }

        [Test]
        public void STK_WorldInteraction_SubordinateKindsCoverDoorAndJammerActions()
        {
            var kindType = ResolveProductionType("EchoProtocol.AI.Stalker.StalkerWorldInteractionKind");

            Assert.That(EnumValue(kindType, "None"), Is.EqualTo(0));
            Assert.That(EnumValue(kindType, "OpeningDoor"), Is.EqualTo(1));
            Assert.That(EnumValue(kindType, "BreakingDoor"), Is.EqualTo(2));
            Assert.That(EnumValue(kindType, "BreakingJammer"), Is.EqualTo(3));
        }

        [Test]
        public void STK_WorldInteraction_UnlockedClosedDoorChoosesOpenNotBreak()
        {
            var source = File.ReadAllText(DriverPath);
            var begin = MethodBody(source, "public StalkerWorldInteractionStartResult TryBegin");

            StringAssert.Contains("door.CanMonsterOpen", begin);
            StringAssert.Contains("door.TryOpenForMonsterAuthoritative()", begin);
            Assert.That(
                begin.IndexOf("door.TryOpenForMonsterAuthoritative()", StringComparison.Ordinal),
                Is.LessThan(begin.IndexOf("StalkerWorldInteractionKind.BreakingDoor", StringComparison.Ordinal)));
        }

        [Test]
        public void STK_WorldInteraction_LockedIntactDoorChoosesBreakDoor()
        {
            var source = File.ReadAllText(DriverPath);
            var begin = MethodBody(source, "public StalkerWorldInteractionStartResult TryBegin");

            StringAssert.Contains("if (!door.IsBroken)", begin);
            StringAssert.Contains("StalkerWorldInteractionKind.BreakingDoor", begin);
            StringAssert.Contains("doorBreakDurationSeconds", begin);
        }

        [Test]
        public void STK_WorldInteraction_ActiveJammerHasPriorityOverDoor()
        {
            var source = File.ReadAllText(DriverPath);
            var begin = MethodBody(source, "public StalkerWorldInteractionStartResult TryBegin");

            Assert.That(
                begin.IndexOf("GetComponentInParent<NetworkDoorJammer>()", StringComparison.Ordinal),
                Is.LessThan(begin.IndexOf("GetComponentInParent<NetworkSlidingDoor>()", StringComparison.Ordinal)));
            StringAssert.Contains("StalkerWorldInteractionKind.BreakingJammer", begin);
            StringAssert.Contains("jammer.BreakDurationSeconds", begin);
        }

        [Test]
        public void STK_WorldInteraction_OrdinaryWallIsNotHandled()
        {
            var source = File.ReadAllText(DriverPath);
            var begin = MethodBody(source, "public StalkerWorldInteractionStartResult TryBegin");

            StringAssert.Contains("if (door == null)", begin);
            StringAssert.Contains("return StalkerWorldInteractionStartResult.NotHandled;", begin);
        }

        [Test]
        public void STK_WorldInteraction_DurationsGateCompletion()
        {
            var source = File.ReadAllText(DriverPath);
            var tick = MethodBody(source, "public bool Tick");

            StringAssert.Contains("_elapsedSeconds += Mathf.Max(0f, deltaSeconds);", tick);
            StringAssert.Contains("if (_elapsedSeconds < _durationSeconds)", tick);
            StringAssert.Contains("return true;", tick);
            StringAssert.Contains("_door.TryBreakAuthoritative()", tick);
            StringAssert.Contains("_jammer.CompleteBreakAuthoritative()", tick);
        }

        [Test]
        public void STK_WorldInteraction_CompletionMutationPathsTerminate()
        {
            var source = File.ReadAllText(DriverPath);
            var tick = MethodBody(source, "public bool Tick");

            StringAssert.Contains("if (_door.TryBreakAuthoritative())", tick);
            StringAssert.Contains("completed = true;", tick);
            StringAssert.Contains("Cancel();", tick);
            StringAssert.Contains("return false;", tick);
            StringAssert.Contains("if (_jammer.CompleteBreakAuthoritative())", tick);
        }

        [Test]
        public void STK_WorldInteraction_AlreadyOpenDoorAndDestroyedJammerCompleteImmediately()
        {
            var source = File.ReadAllText(DriverPath);
            var begin = MethodBody(source, "public StalkerWorldInteractionStartResult TryBegin");
            var tick = MethodBody(source, "public bool Tick");

            StringAssert.Contains("if (!door.BlocksTraversal)", begin);
            StringAssert.Contains("return StalkerWorldInteractionStartResult.Completed;", begin);
            StringAssert.Contains("if (!_jammer.IsActive)", tick);
            StringAssert.Contains("completed = true;", tick);
        }

        [Test]
        public void STK_WorldInteraction_AuthorityRequiredForMutation()
        {
            var source = File.ReadAllText(DriverPath);
            var begin = MethodBody(source, "public StalkerWorldInteractionStartResult TryBegin");
            var tick = MethodBody(source, "public bool Tick");
            var controller = File.ReadAllText(ControllerPath);

            StringAssert.Contains("!hasAuthority", begin);
            StringAssert.Contains("!hasAuthority", tick);
            StringAssert.Contains("CanRunWorldInteractionAuthority()", controller);
            StringAssert.Contains("networkObject == null || !networkObject.IsValid || networkObject.HasStateAuthority", controller);
        }

        [Test]
        public void STK_WorldInteraction_ControllerPreservesSameNavigationObjective()
        {
            var source = File.ReadAllText(ControllerPath);
            var begin = MethodBody(source, "private bool TryBeginWorldInteractionForCurrentNavigation");
            var resume = MethodBody(source, "private void ResumeCurrentNavigationObjectiveAfterWorldInteraction");

            StringAssert.Contains("_navigationObjectiveKey.IsValid", begin);
            StringAssert.Contains("_worldInteractionDriver.TryBegin", begin);
            StringAssert.Contains("_navigation?.Stop();", begin);
            StringAssert.DoesNotContain("ClearNavigationObjective()", begin);
            StringAssert.Contains("TryGetCurrentNavigationRecoveryDestination(out var destination)", resume);
            StringAssert.Contains("_navigation?.RequestDestination(destination, NavigationRequestIntent.NewGoal)", resume);
            StringAssert.DoesNotContain("SetCurrentPatrolDestination()", resume);
            StringAssert.DoesNotContain("Mark", resume);
        }

        [Test]
        public void STK_WorldInteraction_ObjectiveReplacementCancelsWithoutArrival()
        {
            var source = File.ReadAllText(ControllerPath);
            var tick = MethodBody(source, "private bool TickWorldInteraction");

            StringAssert.Contains("!_navigationObjectiveKey.Equals(_worldInteractionDriver.ObjectiveKey)", tick);
            StringAssert.Contains("currentState != _worldInteractionDriver.State", tick);
            StringAssert.Contains("CancelWorldInteraction(\"objective-or-state-changed\")", tick);
            StringAssert.DoesNotContain("MarkRoomSweepDestinationReached()", tick);
            StringAssert.DoesNotContain("MarkCanonicalDestinationReached()", tick);
        }

        [Test]
        public void STK_WorldInteraction_RoomSweepObjectiveIsNotClearedByTemporaryBlockerHandling()
        {
            var source = File.ReadAllText(ControllerPath);
            var begin = MethodBody(source, "private bool TryBeginWorldInteractionForCurrentNavigation");
            var resume = MethodBody(source, "private void ResumeCurrentNavigationObjectiveAfterWorldInteraction");

            StringAssert.Contains("StalkerNavigationObjectiveKind.RoomSweepTransit", source);
            StringAssert.DoesNotContain("ActivateRoomSweepPatrolFallback()", begin);
            StringAssert.DoesNotContain("ClearRoomSweepDestination()", begin);
            StringAssert.DoesNotContain("ClearNavigationObjective()", resume);
        }

        private static Type ResolveProductionType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }

            Assert.Fail($"Missing production type '{fullName}'.");
            return null;
        }

        private static int EnumValue(Type type, string name)
        {
            return (int)Enum.Parse(type, name);
        }

        private static string MethodBody(string source, string signaturePrefix)
        {
            var start = source.IndexOf(signaturePrefix, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing method '{signaturePrefix}'.");

            var braceStart = source.IndexOf('{', start);
            Assert.That(braceStart, Is.GreaterThanOrEqualTo(0), $"Missing method body for '{signaturePrefix}'.");

            var depth = 0;
            for (var i = braceStart; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(start, i - start + 1);
                    }
                }
            }

            Assert.Fail($"Unterminated method body for '{signaturePrefix}'.");
            return string.Empty;
        }
    }
}
