using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Networking.Tests
{
    public sealed class NetworkMatchStateTests
    {
        [Test]
        public void Zone1Completion_DoesNotTeleportPlayersToZone2()
        {
            string source = File.ReadAllText(
                    "Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs")
                .Replace("\r\n", "\n");

            StringAssert.Contains(
                "if (next != NetworkMatchPhase.Zone2Objective)",
                source);

            StringAssert.Contains(
                "TeleportGameplayPlayersAuthoritative(next);",
                source);

            StringAssert.Contains(
                "TeleportGameplayPlayersAuthoritative(\n" +
                "                NetworkMatchPhase.Zone2Objective);",
                source);
        }

        private const string MatchSourcePath =
            "Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs";
        private const string ObjectiveSourcePath =
            "Assets/_Project/Scripts/Networking/Interaction/NetworkSectorBox.cs";
        private const string HudObjectiveTrackerPath =
            "Assets/Scripts/UI/HUD/HUDObjectiveTracker.cs";
        private const string ZoneMissionHudPath =
            "Assets/Scripts/UI/HUD/HUDZoneMissions.cs";
        private const string MatchPrefabPath =
            "Assets/Resources/Network/NetworkMatchState.prefab";

        [Test]
        public void CoreStabilizer_SilencesObjectiveNoiseInsideField()
        {
            string source = File.ReadAllText(
                "Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs");

            StringAssert.Contains("IsObjectiveNoiseSilenced", source);
            StringAssert.Contains("IsCoreStabilizerCovering", source);
            StringAssert.Contains("RuntimeNoiseType.MACHINE_REPAIR", source);
            StringAssert.Contains("RuntimeNoiseType.TERMINAL_DOWNLOAD", source);
            StringAssert.Contains("RuntimeNoiseType.VEHICLE_PUSH", source);
            StringAssert.Contains("RuntimeNoiseType.CHARGE_TRANSFER", source);
        }

        [Test]
        public void NetworkMatchPrefab_UsesCanonicalTiming()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Network/NetworkMatchState.prefab");
            Assert.That(prefab, Is.Not.Null);

            var state = FindComponentByTypeName(
                prefab,
                "EchoProtocol.Networking.NetworkMatchState");
            Assert.That(state, Is.Not.Null);

            var serializedState = new SerializedObject(state);
            var matchDuration = serializedState.FindProperty("_matchDurationSeconds");
            var escapeDuration = serializedState.FindProperty("_escapeDurationSeconds");
            Assert.That(matchDuration, Is.Not.Null);
            Assert.That(escapeDuration, Is.Not.Null);
            Assert.That(matchDuration.floatValue, Is.EqualTo(2700f).Within(0.001f));
            Assert.That(escapeDuration.floatValue, Is.EqualTo(45f).Within(0.001f));
        }

        [Test]
        public void NetworkMatchSource_DeclaresCanonicalTiming()
        {
            string source = File.ReadAllText(MatchSourcePath);
            StringAssert.Contains("public const float DefaultMatchDurationSeconds = 2700f;", source);
            StringAssert.Contains("public const float DefaultEscapeDurationSeconds = 45f;", source);
        }

        [Test]
        public void MATCH_NET_FsmOnlyAdvancesFromExpectedRunningPhase()
        {
            var source = LoadNetworkMatchStateSource();

            StringAssert.Contains("status == NetworkMatchStatus.Running", source);
            StringAssert.Contains("current == expected", source);
            StringAssert.Contains("next != current", source);
            StringAssert.Contains("TryAdvancePhase(\n                    NetworkMatchPhase.CoreObjective,\n                    NetworkMatchPhase.Zone2Objective", source.Replace("\r\n", "\n"));
        }

        [Test]
        public void MATCH_NET_EndAndObjectiveMutationFreezeAfterTerminalCommit()
        {
            var source = LoadNetworkMatchStateSource();

            StringAssert.Contains("result != NetworkMatchResult.None", source);
            StringAssert.Contains("Status = NetworkMatchStatus.Ended", source);
            StringAssert.Contains("CurrentPhase = NetworkMatchPhase.MatchEnded", source);
            StringAssert.Contains("if (!Object.HasStateAuthority || !NetworkMatchStateRules.CanEnd", source);
            StringAssert.Contains("status == NetworkMatchStatus.Running && current == required", source);
        }

        [Test]
        public void MATCH_NET_CoreProgressHasOneAuthoritativeSource()
        {
            var matchSource = LoadNetworkMatchStateSource();
            var objectiveSource = File.ReadAllText(ObjectiveSourcePath);

            StringAssert.DoesNotContain("[Networked] public int ObjectiveProgress", matchSource);
            StringAssert.Contains("public NetworkId ObjectiveSourceId", matchSource);
            StringAssert.Contains("current = source.PlacedCoreCount", matchSource);
            StringAssert.Contains("public int PlacedCoreCount", objectiveSource);
        }

        [Test]
        public void MATCH_NET_AuthoritativeMatchTimerIsFortyFiveMinutes()
        {
            var source = LoadNetworkMatchStateSource();
            var prefab = File.ReadAllText(MatchPrefabPath);

            StringAssert.Contains("_matchDurationSeconds = 2700f", source);
            StringAssert.Contains("_matchDurationSeconds: 2700", prefab);
        }

        [Test]
        public void MATCH_NET_TimersAndTerminalResultAreReplicatedSemanticState()
        {
            var source = LoadNetworkMatchStateSource();

            StringAssert.Contains("public NetworkMatchPhase CurrentPhase", source);
            StringAssert.Contains("public NetworkMatchStatus Status", source);
            StringAssert.Contains("public NetworkMatchResult Result", source);
            StringAssert.Contains("public NetworkMatchEndReason EndReason", source);
            StringAssert.Contains("private TickTimer EscapeTimer", source);
            StringAssert.Contains("private TickTimer MatchTimer", source);
            StringAssert.Contains("public float CurrentScenarioEscapeDoorTimerSeconds", source);
            StringAssert.Contains("TickTimer.CreateFromSeconds(Runner, duration)", source);
            StringAssert.Contains("EscapeTimer.Expired(Runner)", source);
            StringAssert.DoesNotContain("Time.deltaTime", source);
        }

        [Test]
        public void MATCH_NET_Zone3PowerTransferStartsSingleEscapeDeadline()
        {
            var source = LoadNetworkMatchStateSource().Replace("\r\n", "\n");

            StringAssert.Contains(
                "TryAdvancePhase(NetworkMatchPhase.Zone3PushFrigate,\n" +
                "                    NetworkMatchPhase.FinalHunt, \"ZONE3_CHARGE_ACTIVATED\")",
                source);

            StringAssert.Contains(
                "StartEscapeDeadlineIfNeededAuthoritative(\"ZONE3_POWER_TRANSFER\")",
                source);

            StringAssert.Contains(
                "if (!Object.HasStateAuthority || EscapeTimer.IsRunning) return;",
                source);
        }

        [Test]
        public void MATCH_NET_EscapeDeadlineRunsDuringFinalHuntAndEscape()
        {
            var source = LoadNetworkMatchStateSource().Replace("\r\n", "\n");

            StringAssert.Contains(
                "public bool IsEscapeTimerRunning =>\n" +
                "            (CurrentPhase == NetworkMatchPhase.FinalHunt\n" +
                "             || CurrentPhase == NetworkMatchPhase.Escape)\n" +
                "            && EscapeTimer.IsRunning;",
                source);

            StringAssert.Contains(
                "if (IsEscapeTimerRunning && EscapeTimer.Expired(Runner))",
                source);
        }

        [Test]
        public void MATCH_NET_Zone3DoorexitTransitionsToEscapeWithoutResettingDeadline()
        {
            var source = LoadNetworkMatchStateSource().Replace("\r\n", "\n");

            StringAssert.Contains(
                "if (CurrentPhase != NetworkMatchPhase.FinalHunt\n" +
                "                    && CurrentPhase != NetworkMatchPhase.Escape)",
                source);

            StringAssert.Contains(
                "fromDoorexit\n" +
                "                    && CurrentPhase == NetworkMatchPhase.FinalHunt\n" +
                "                    && !TryAdvancePhase(\n" +
                "                        NetworkMatchPhase.FinalHunt,\n" +
                "                        NetworkMatchPhase.Escape,\n" +
                "                        \"FINAL_HUNT\")",
                source);

            StringAssert.Contains(
                "StartEscapeDeadlineIfNeededAuthoritative(\"LEGACY_ESCAPE_DOOR\")",
                source);
        }

        [Test]
        public void MATCH_NET_ZoneBoundaryUsesZone3EntryAfterZone2()
        {
            var source = LoadNetworkMatchStateSource().Replace("\r\n", "\n");

            StringAssert.Contains(
                "Status, CurrentPhase, NetworkMatchPhase.Zone2Objective, NetworkMatchPhase.Zone3FindFrigate",
                source);

            StringAssert.Contains(
                "TryAdvancePhase(NetworkMatchPhase.Zone2Objective, NetworkMatchPhase.Zone3FindFrigate, \"ZONE2_OBJECTIVE\")",
                source);

            StringAssert.DoesNotContain(
                "TryAdvancePhase(NetworkMatchPhase.Zone2Objective, NetworkMatchPhase.FinalHunt",
                source);
        }

        [Test]
        public void MATCH_NET_HudUsesEmergencyPowerTransferWording()
        {
            var source = File.ReadAllText(HudObjectiveTrackerPath);

            StringAssert.Contains("INITIATE POWER TRANSFER", source);
            StringAssert.Contains("Emergency power remaining", source);
            StringAssert.DoesNotContain("CHARGE SPACEFRIGATE", source);
            StringAssert.DoesNotContain("Scifi Charge", source);
        }

        [Test]
        public void ZoneMissionHud_UsesAuthoritativeNetworkProgress()
        {
            string source = File.ReadAllText(ZoneMissionHudPath);

            StringAssert.Contains("NetworkMatchState.Instance", source);
            StringAssert.Contains("TryGetObjectiveProgress", source);
            StringAssert.Contains("CompletedRelayCount", source);
            StringAssert.Contains("SecurityHoldProgress01", source);
        }

        [Test]
        public void MATCH_NET_ScenarioNumericStatePreservesDoublePrecision()
        {
            var source = LoadNetworkMatchStateSource();

            StringAssert.Contains(
                "[Networked] public double ScenarioDetectionFillRate",
                source);

            StringAssert.Contains(
                "[Networked] public double ScenarioDetectionDecayRate",
                source);

            StringAssert.Contains(
                "[Networked] public double ScenarioChaseSpeed",
                source);

            StringAssert.Contains(
                "[Networked] public double ScenarioSearchDuration",
                source);

            StringAssert.Contains(
                "[Networked] public double ScenarioEscapeDoorTimerSeconds",
                source);

            StringAssert.DoesNotContain(
                "(float)config.MonsterParameters.DetectionFillRate",
                source);
        }

        [Test]
        public void MATCH_NET_AllEliminatedRequiresAtLeastOneTrackedGameplayPlayer()
        {
            var source = LoadNetworkMatchStateSource();

            StringAssert.Contains("trackedCount > 0 && activeCount == 0 && survivorCount == 0", source);
            StringAssert.Contains("lobbyState.IsGameplayPlayer", source);
        }

        [Test]
        public void MATCH_NET_Zone2RelayRepairExpiresIfSecurityHoldIsNotCompleted()
        {
            var source = LoadNetworkMatchStateSource();

            StringAssert.Contains("_securityHoldRelayRetryWindowSeconds = 300f", source);
            StringAssert.Contains("private TickTimer RelayRepairWindowTimer", source);
            StringAssert.Contains("ShouldResetRelayRepairForSecurityHoldTimeout()", source);
            StringAssert.Contains("ResetRelayRepairForRetryAuthoritative()", source);
            StringAssert.Contains("Zone2Stage = Zone2MissionStage.RepairRelays", source);
        }

        [Test]
        public void MATCH_NET_ZoneBoundaryResetsReviveBudget()
        {
            var source = LoadNetworkMatchStateSource();

            StringAssert.Contains("ResetPlayerReviveBudgetsAuthoritative", source);
            StringAssert.Contains("previous == NetworkMatchPhase.CoreObjective", source);
            StringAssert.Contains("next == NetworkMatchPhase.Zone2Objective", source);
            StringAssert.Contains("previous == NetworkMatchPhase.Zone2Objective", source);
            StringAssert.Contains("next == NetworkMatchPhase.FinalHunt", source);
            StringAssert.Contains("ResetZoneReviveBudgetAuthoritative", source);
        }

        [Test]
        public void MATCH_NET_SecurityHoldAccumulatesAcrossUpToFourValidParticipants()
        {
            var source = LoadNetworkMatchStateSource();

            StringAssert.Contains("SecurityHoldOperator4", source);
            StringAssert.Contains("SecurityHoldParticipantCount >= 4", source);
            StringAssert.Contains("SecurityHoldAccumulatedSeconds + Runner.DeltaTime * SecurityHoldWorkRate(participants)", source);
            StringAssert.Contains("1 => 1f", source);
            StringAssert.Contains("2 => 1.2f", source);
            StringAssert.Contains("3 => 1.5f", source);
            StringAssert.Contains("4 => 2f", source);
            StringAssert.Contains("TryValidateZone2Requester(player, director.SecurityTerminal", source);
            StringAssert.Contains("RemoveSecurityHoldParticipant(player)", source);
            StringAssert.Contains("RpcRefreshSecurityHold", source);
            StringAssert.Contains("EmitSecurityTerminalInteractionNoiseAuthoritative", source);
            StringAssert.Contains("RuntimeNoiseType.INTERACTION", source);
            StringAssert.Contains("_securityHoldLeases[index].ExpiredOrNotRunning(Runner)", source);
            StringAssert.Contains("if (player.IsNone) _securityHoldLeases[index] = TickTimer.None", source);
            StringAssert.DoesNotContain("SecurityHoldTimer", source);
        }

        [Test]
        public void MATCH_NET_FirstSecurityTerminalInteractionEmitsNoise()
        {
            var source = LoadNetworkMatchStateSource();

            StringAssert.Contains(
                "EmitSecurityTerminalInteractionNoiseAuthoritative(\n" +
                "                actor,\n" +
                "                director.SecurityTerminal);",
                source.Replace("\r\n", "\n"));

            StringAssert.Contains(
                "SecurityTerminalDiscovered = true",
                source);

            StringAssert.Contains(
                "Zone2Stage = Zone2MissionStage.RepairRelays",
                source);
        }

        [Test]
        public void MATCH_NET_EscapeTimeoutHonorsEscapedPlayers()
        {
            var source = LoadNetworkMatchStateSource();
            int start = source.IndexOf("if (IsEscapeTimerRunning && EscapeTimer.Expired(Runner))");
            int end = source.IndexOf("public override void Render()", start);
            var timeout = source.Substring(start, end - start);
            StringAssert.Contains("CountFinalPlayers(out var escapedCount, out _, out _)", timeout);
            StringAssert.Contains("bool hasEscapedPlayer = escapedCount > 0", timeout);
            StringAssert.Contains("hasEscapedPlayer ? NetworkMatchResult.Win : NetworkMatchResult.Lose", timeout);
            StringAssert.Contains("hasEscapedPlayer ? NetworkMatchEndReason.PlayerEscaped : NetworkMatchEndReason.EscapeTimeout", timeout);
        }

        [Test]
        public void MATCH_NET_PowerTransferAllowsAtLeastThreeMinutesRegardlessOfScenario()
        {
            var source = LoadNetworkMatchStateSource();
            StringAssert.Contains("MinimumPowerTransferEscapeDurationSeconds = 180f", source);
            StringAssert.Contains("reason == \"ZONE3_POWER_TRANSFER\"", source);
            StringAssert.Contains("Mathf.Max(MinimumPowerTransferEscapeDurationSeconds, CurrentScenarioEscapeDoorTimerSeconds)", source);
            StringAssert.Contains("TickTimer.CreateFromSeconds(Runner, duration)", source);
        }

        private static string LoadNetworkMatchStateSource()
        {
            return File.ReadAllText(MatchSourcePath);
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
    }
}
