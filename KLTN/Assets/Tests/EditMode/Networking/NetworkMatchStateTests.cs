using System.IO;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class NetworkMatchStateTests
    {
        private const string MatchSourcePath =
            "Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs";
        private const string ObjectiveSourcePath =
            "Assets/_Project/Scripts/Networking/Interaction/NetworkSectorBox.cs";

        [Test]
        public void MATCH_NET_FsmOnlyAdvancesFromExpectedRunningPhase()
        {
            var source = File.ReadAllText(MatchSourcePath);

            StringAssert.Contains("status == NetworkMatchStatus.Running", source);
            StringAssert.Contains("current == expected", source);
            StringAssert.Contains("next != current", source);
            StringAssert.Contains("next != NetworkMatchPhase.MatchEnded", source);
            StringAssert.Contains("TryAdvancePhase(NetworkMatchPhase.Puzzle, NetworkMatchPhase.SecurityHold", source);
            StringAssert.Contains("NetworkMatchPhase.SecurityHold,\n                    NetworkMatchPhase.FinalHunt", source.Replace("\r\n", "\n"));
        }

        [Test]
        public void MATCH_NET_EndAndObjectiveMutationFreezeAfterTerminalCommit()
        {
            var source = File.ReadAllText(MatchSourcePath);

            StringAssert.Contains("result != NetworkMatchResult.None", source);
            StringAssert.Contains("Status = NetworkMatchStatus.Ended", source);
            StringAssert.Contains("CurrentPhase = NetworkMatchPhase.MatchEnded", source);
            StringAssert.Contains("if (!Object.HasStateAuthority || !NetworkMatchStateRules.CanEnd", source);
            StringAssert.Contains("status == NetworkMatchStatus.Running && current == required", source);
        }

        [Test]
        public void MATCH_NET_CoreProgressHasOneAuthoritativeSource()
        {
            var matchSource = File.ReadAllText(MatchSourcePath);
            var objectiveSource = File.ReadAllText(ObjectiveSourcePath);

            StringAssert.DoesNotContain("[Networked] public int ObjectiveProgress", matchSource);
            StringAssert.Contains("public NetworkId ObjectiveSourceId", matchSource);
            StringAssert.Contains("current = source.PlacedCoreCount", matchSource);
            StringAssert.Contains("public int PlacedCoreCount", objectiveSource);
        }

        [Test]
        public void MATCH_NET_TimersAndTerminalResultAreReplicatedSemanticState()
        {
            var source = File.ReadAllText(MatchSourcePath);

            StringAssert.Contains("public NetworkMatchPhase CurrentPhase", source);
            StringAssert.Contains("public NetworkMatchStatus Status", source);
            StringAssert.Contains("public NetworkMatchResult Result", source);
            StringAssert.Contains("public NetworkMatchEndReason EndReason", source);
            StringAssert.Contains("private TickTimer EscapeTimer", source);
            StringAssert.Contains("private TickTimer MatchTimer", source);
            StringAssert.Contains("TickTimer.CreateFromSeconds(Runner, _escapeDurationSeconds)", source);
            StringAssert.Contains("EscapeTimer.Expired(Runner)", source);
            StringAssert.DoesNotContain("Time.deltaTime", source);
        }

        [Test]
        public void MATCH_NET_AllEliminatedRequiresAtLeastOneTrackedGameplayPlayer()
        {
            var source = File.ReadAllText(MatchSourcePath);

            StringAssert.Contains("trackedCount > 0 && activeCount == 0 && survivorCount == 0", source);
            StringAssert.Contains("lobbyState.IsGameplayPlayer", source);
        }
    }
}
