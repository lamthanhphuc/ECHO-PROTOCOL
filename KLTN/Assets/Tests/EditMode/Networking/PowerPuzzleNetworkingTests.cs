using System.IO;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class PowerPuzzleNetworkingTests
    {
        private const string PuzzleSourcePath =
            "Assets/_Project/Scripts/Networking/Interaction/NetworkPowerPuzzle.cs";
        private const string StationSourcePath =
            "Assets/_Project/Scripts/Networking/Interaction/NetworkPowerPuzzleStation.cs";
        private const string SectorSourcePath =
            "Assets/_Project/Scripts/Networking/Interaction/NetworkSectorBox.cs";

        [Test]
        public void M2_PUZZLE_CorrectInput_AdvancesOnlyExpectedStep()
        {
            Assert.That(
                PowerPuzzleAuthorityRules.EvaluateInput(
                    NetworkPowerPuzzleState.InProgress, 0, 2, 0),
                Is.EqualTo(PowerPuzzleInputResult.AcceptedCorrect));
            Assert.That(
                PowerPuzzleAuthorityRules.EvaluateInput(
                    NetworkPowerPuzzleState.InProgress, 1, 2, 0),
                Is.EqualTo(PowerPuzzleInputResult.AcceptedIncorrect));
        }

        [Test]
        public void M2_PUZZLE_RejectsInputDuringFailureResetAndAfterCompletion()
        {
            Assert.That(
                PowerPuzzleAuthorityRules.EvaluateInput(
                    NetworkPowerPuzzleState.Failed, 0, 2, 0),
                Is.EqualTo(PowerPuzzleInputResult.RejectedInvalidState));
            Assert.That(
                PowerPuzzleAuthorityRules.EvaluateInput(
                    NetworkPowerPuzzleState.Resetting, 0, 2, 0),
                Is.EqualTo(PowerPuzzleInputResult.RejectedInvalidState));
            Assert.That(
                PowerPuzzleAuthorityRules.EvaluateInput(
                    NetworkPowerPuzzleState.Completed, 0, 2, 0),
                Is.EqualTo(PowerPuzzleInputResult.AlreadyCompleted));
        }

        [Test]
        public void M2_PUZZLE_RejectsUnknownStationInput()
        {
            Assert.That(
                PowerPuzzleAuthorityRules.EvaluateInput(
                    NetworkPowerPuzzleState.InProgress, -1, 2, 0),
                Is.EqualTo(PowerPuzzleInputResult.RejectedInvalidInput));
            Assert.That(
                PowerPuzzleAuthorityRules.EvaluateInput(
                    NetworkPowerPuzzleState.InProgress, 2, 2, 0),
                Is.EqualTo(PowerPuzzleInputResult.RejectedInvalidInput));
            Assert.That(
                PowerPuzzleAuthorityRules.EvaluateInput(
                    NetworkPowerPuzzleState.InProgress, 0, 2, 3),
                Is.EqualTo(PowerPuzzleInputResult.RejectedInvalidInput));
        }

        [Test]
        public void M2_PUZZLE_NearSimultaneousInputs_AreEvaluatedAgainstLatestAuthorityState()
        {
            // First serialized host command advances expected input from A(0) to B(1).
            var first = PowerPuzzleAuthorityRules.EvaluateInput(
                NetworkPowerPuzzleState.InProgress, 0, 2, 0);
            var second = PowerPuzzleAuthorityRules.EvaluateInput(
                NetworkPowerPuzzleState.InProgress, 1, 2, 1);

            Assert.That(first, Is.EqualTo(PowerPuzzleInputResult.AcceptedCorrect));
            Assert.That(second, Is.EqualTo(PowerPuzzleInputResult.AcceptedCorrect));
        }

        [Test]
        public void M2_PUZZLE_PersistentProgressIsNetworkedAndSectorHasNoInteractShortcut()
        {
            var puzzleSource = File.ReadAllText(PuzzleSourcePath);
            var stationSource = File.ReadAllText(StationSourcePath);
            var sectorSource = File.ReadAllText(SectorSourcePath);

            StringAssert.Contains("public NetworkPowerPuzzleState State", puzzleSource);
            StringAssert.Contains("public int CurrentSequenceIndex", puzzleSource);
            StringAssert.Contains("public int FailureCount", puzzleSource);
            StringAssert.Contains("public NetworkBool LastInputWasCorrect", puzzleSource);
            StringAssert.Contains("public PlayerRef LastInteractor", puzzleSource);
            StringAssert.Contains("puzzle.TryApplyInput(context.Player, InputId)", stationSource);
            StringAssert.DoesNotContain("case NetworkMatchPhase.PowerPuzzle:", sectorSource);
            StringAssert.DoesNotContain("RPC_CompletePuzzle", puzzleSource);
        }
    }
}
