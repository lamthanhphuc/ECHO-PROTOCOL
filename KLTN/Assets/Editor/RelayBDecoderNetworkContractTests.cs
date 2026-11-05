using System.IO;
using EchoProtocol.RelayB;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.RelayB.Tests
{
    public sealed class RelayBDecoderNetworkContractTests
    {
        private static string Read(string path) => File.ReadAllText(Path.Combine(Application.dataPath, path));
        [Test] public void TransmissionUsesValidatedOperatorAndExpectedRoundAndAttempt()
        {
            string source = Read("_Project/Scripts/Networking/Match/NetworkMatchState.cs");
            int start = source.IndexOf("private bool TryTransmitRelayBAuthoritative");
            string method = source.Substring(start, source.IndexOf("private", start + 12) - start);
            StringAssert.Contains("RelayBDecoder.IsValidCode(packed)", method);
            StringAssert.Contains("TryValidateRelayCommand(requester, slot", method);
            StringAssert.Contains("state.Round != round || state.Attempts != attempt", method);
            StringAssert.Contains("ClaimRelayOperator(requester, slot)", method);
            StringAssert.Contains("controller.TransmitCode(packed)", method);
        }
        [Test] public void ReplicationContainsPublicHistoryButNoSecretOrDecoderSeed()
        {
            foreach (var field in typeof(RelayBDecodeTelemetry).GetFields())
                Assert.IsFalse(field.Name.ToLowerInvariant().Contains("secret") || field.Name.ToLowerInvariant().Contains("seed"));
            string source = Read("Scripts/RelayB/RelayBController.cs");
            StringAssert.Contains("_simulation.Decoder.Initialize(NewAttemptSeed())", source);
        }
        [Test] public void ProxiesApplyDecoderStateWithoutTickingAuthoritySimulation()
        {
            string director = Read("Scripts/MatchFlow/Zone2MissionDirector.cs");
            StringAssert.Contains("!matchState.Object.HasStateAuthority && matchState.RelayB1Decoder.Round > 0", director);
            StringAssert.Contains("!matchState.Object.HasStateAuthority && matchState.RelayB2Decoder.Round > 0", director);
            string controller = Read("Scripts/RelayB/RelayBController.cs");
            StringAssert.Contains("&& !matchState.Object.HasStateAuthority) return;", controller);
        }
        [Test] public void RetiredDspCommandsAreRejectedInsteadOfUnlockingDecoder()
        {
            string source = Read("_Project/Scripts/Networking/Match/NetworkMatchState.cs");
            int start = source.IndexOf("private bool TryTestRelayBAuthoritative");
            string method = source.Substring(start, source.IndexOf("private bool TryRelayBActionAuthoritative", start) - start);
            StringAssert.Contains("return false;", method); StringAssert.DoesNotContain("AnalyzeOutput", method);
        }
    }
}
