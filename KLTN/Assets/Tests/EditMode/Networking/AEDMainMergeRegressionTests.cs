using System;
using System.IO;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class AEDMainMergeRegressionTests
    {
        private const string Minion = "Assets/Scripts/AI/Minions/CreepMinionRuntime.cs";
        private const string Scanner = "Assets/Scripts/Tools/Scanner/NetworkFieldScanner.cs";
        private const string Movement = "Assets/_Project/Scripts/Networking/Player/NetworkPlayerMovement.cs";
        private const string V02Coordinator = "Assets/_Project/Scripts/Networking/Authority/MatchAuthorityRuntimeV02Coordinator.cs";
        private const string Lobby = "Assets/_Project/Scripts/Networking/Session/LobbyManager.cs";
        private const string Match = "Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs";

        [Test]
        public void MinionLifecyclePublishesSpawnDespawnAndAuthoritativeEvidence()
        {
            var minion = File.ReadAllText(Minion);
            StringAssert.Contains("RecordPhase8MinionSpawn(", minion);
            StringAssert.Contains("RecordPhase8MinionDespawn(", minion);
            StringAssert.Contains("RecordMinionDespawn(", minion);
            StringAssert.Contains("RecordMinionSnapshot(Object.Id", minion);
            StringAssert.Contains("AEDMinionFactKindV1.AlertAccepted", minion);
            StringAssert.Contains("AEDMinionFactKindV1.CoreForcedDrop", minion);
            StringAssert.Contains("AEDMinionFactKindV1.CoreStolen", minion);
            StringAssert.Contains("AEDMinionFactKindV1.ToolRelocated", minion);
        }

        [Test]
        public void ScannerGameplayCandidatesAlsoProduceVerifiedAEDEvidence()
        {
            var scanner = File.ReadAllText(Scanner);
            StringAssert.Contains("CollectCoreCandidates(requestedMode)", scanner);
            StringAssert.Contains("CaptureV02Scan(sequence, requestedMode", scanner);
            StringAssert.Contains("V02LastScan.Sequence != sequence", scanner);
            StringAssert.Contains("MatchAuthorityRuntime.Instance.MatchId != V02LastScan.MatchId", scanner);
        }

        [Test]
        public void ResonanceInputAndP4HideProtectionShareMovementState()
        {
            var movement = File.ReadAllText(Movement);
            StringAssert.Contains("ResonanceHeld", movement);
            StringAssert.Contains("AEDV02HideProtectionTimer", movement);
            StringAssert.Contains("TryActivateAEDV02HideProtection()", movement);
            StringAssert.Contains("if (BlocksAEDV02HideForceExit()) return false", movement);
        }

        [Test]
        public void LobbyBindsRosterBeforePreMatchAndCleansRejectedStart()
        {
            var lobby = File.ReadAllText(Lobby);
            int binding = lobby.IndexOf("TryValidateCommittedRoster(runner, authority, out rosterError)", StringComparison.Ordinal);
            int preMatch = lobby.IndexOf("ResolvePreMatchAsync(", StringComparison.Ordinal);
            Assert.That(binding, Is.GreaterThanOrEqualTo(0));
            Assert.That(preMatch, Is.GreaterThan(binding));
            StringAssert.Contains("TryStageBackendPreMatch(matchId, decision)", lobby);
            StringAssert.Contains("StageV02PreMatchAsync(_preMatchCancellation.Token)", lobby);
            StringAssert.Contains("ClearStagedV02PreMatch()", lobby);
            StringAssert.Contains("CancelMatchStartGate()", lobby);
        }

        [Test]
        public void FixedNormalKeepsMapOneMinionBaseline()
        {
            var difficultyType = Type.GetType("EchoProtocol.Gameplay.MatchDifficulty, Assembly-CSharp", true);
            var profilesType = Type.GetType("EchoProtocol.Gameplay.MatchDifficultyProfiles, Assembly-CSharp", true);
            var normal = profilesType.GetMethod("Get").Invoke(null,
                new[] { Enum.Parse(difficultyType, "Normal") });
            Assert.That(normal.GetType().GetProperty("Zone1MinionCap").GetValue(normal), Is.EqualTo(3));
            Assert.That(normal.GetType().GetProperty("Zone2MinionCap").GetValue(normal), Is.EqualTo(5));
            Assert.That(normal.GetType().GetProperty("Zone3MinionCap").GetValue(normal), Is.EqualTo(1));
            StringAssert.Contains("CanApplyAEDv2Gameplay(", File.ReadAllText(Match));
        }

        [Test]
        public void ApprovalHostConsumerReceiptChainRemainsConnected()
        {
            var lobby = File.ReadAllText(Lobby);
            var coordinator = File.ReadAllText(V02Coordinator);
            var match = File.ReadAllText(Match);
            StringAssert.Contains("TryStageBackendPreMatch(matchId, decision)", lobby);
            StringAssert.Contains("StageV02PreMatchAsync", coordinator);
            StringAssert.Contains("CommitStagedV02PreMatch(this)", match);
            StringAssert.Contains("_aedv2Boundary.TryReconcileReceipt", match);
            StringAssert.Contains("ConfirmPlanAppliedAsync(", match);
        }
    }
}
