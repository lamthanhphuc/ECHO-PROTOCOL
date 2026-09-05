using System;
using System.IO;
using System.Reflection;
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
                EvaluateInput("InProgress", 0, 2, 0),
                Is.EqualTo(Result("AcceptedCorrect")));
            Assert.That(
                EvaluateInput("InProgress", 1, 2, 0),
                Is.EqualTo(Result("AcceptedIncorrect")));
        }

        [Test]
        public void M2_PUZZLE_RejectsInputDuringFailureResetAndAfterCompletion()
        {
            Assert.That(
                EvaluateInput("Failed", 0, 2, 0),
                Is.EqualTo(Result("RejectedInvalidState")));
            Assert.That(
                EvaluateInput("Resetting", 0, 2, 0),
                Is.EqualTo(Result("RejectedInvalidState")));
            Assert.That(
                EvaluateInput("Completed", 0, 2, 0),
                Is.EqualTo(Result("AlreadyCompleted")));
        }

        [Test]
        public void M2_PUZZLE_RejectsUnknownStationInput()
        {
            Assert.That(
                EvaluateInput("InProgress", -1, 2, 0),
                Is.EqualTo(Result("RejectedInvalidInput")));
            Assert.That(
                EvaluateInput("InProgress", 2, 2, 0),
                Is.EqualTo(Result("RejectedInvalidInput")));
            Assert.That(
                EvaluateInput("InProgress", 0, 2, 3),
                Is.EqualTo(Result("RejectedInvalidInput")));
        }

        [Test]
        public void M2_PUZZLE_NearSimultaneousInputs_AreEvaluatedAgainstLatestAuthorityState()
        {
            // First serialized host command advances expected input from A(0) to B(1).
            var first = EvaluateInput("InProgress", 0, 2, 0);
            var second = EvaluateInput("InProgress", 1, 2, 1);

            Assert.That(first, Is.EqualTo(Result("AcceptedCorrect")));
            Assert.That(second, Is.EqualTo(Result("AcceptedCorrect")));
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

        private static object EvaluateInput(string stateName, int inputId, int stationCount, int expectedInputId)
        {
            var rulesType = ResolveProductionType("EchoProtocol.Networking.PowerPuzzleAuthorityRules");
            var stateEnumType = ResolveProductionType("EchoProtocol.Networking.NetworkPowerPuzzleState");
            var stateValue = Enum.Parse(stateEnumType, stateName);
            var method = rulesType.GetMethod("EvaluateInput", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing PowerPuzzleAuthorityRules.EvaluateInput method.");
            return method.Invoke(null, new object[] { stateValue, inputId, stationCount, expectedInputId });
        }

        private static object Result(string resultName)
        {
            var resultEnumType = ResolveProductionType("EchoProtocol.Networking.PowerPuzzleInputResult");
            return Enum.Parse(resultEnumType, resultName);
        }

        private static Type ResolveProductionType(string fullTypeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullTypeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Could not find production type '{fullTypeName}' in the loaded Unity AppDomain.");
            return null;
        }
    }
}
