using System;
using System.IO;
using System.Reflection;
using Fusion;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class RpcRequesterResolverTests
    {
        private const string LobbyPlayerStateScriptPath = "Assets/_Project/Scripts/Networking/Player/LobbyPlayerState.cs";
        private const string NetworkPlayerInteractorScriptPath = "Assets/_Project/Scripts/Networking/Interaction/NetworkPlayerInteractor.cs";

        [Test]
        public void FND_NET_RPC_ValidRemoteSource_IsPreserved()
        {
            var source = PlayerRef.FromIndex(1);
            var owner = PlayerRef.FromIndex(2);

            var resolved = TryResolve(
                source,
                owner,
                hasStateAuthority: true,
                hasInputAuthority: false,
                out var requester);

            Assert.That(resolved, Is.True);
            Assert.That(requester, Is.EqualTo(source));
        }

        [Test]
        public void FND_NET_RPC_HostLocalInvalidSource_ResolvesToInputAuthorityOnlyWithLocalAuthority()
        {
            var owner = PlayerRef.FromIndex(0);

            var resolved = TryResolve(
                PlayerRef.None,
                owner,
                hasStateAuthority: true,
                hasInputAuthority: true,
                out var requester);

            Assert.That(resolved, Is.True);
            Assert.That(requester, Is.EqualTo(owner));
        }

        [Test]
        public void FND_NET_RPC_InvalidSourceWithoutHostLocalAuthority_StaysInvalid()
        {
            var owner = PlayerRef.FromIndex(0);

            Assert.That(TryResolve(PlayerRef.None, owner, hasStateAuthority: false, hasInputAuthority: true, out var noState), Is.False);
            Assert.That(noState, Is.EqualTo(PlayerRef.None));

            Assert.That(TryResolve(PlayerRef.None, owner, hasStateAuthority: true, hasInputAuthority: false, out var noInput), Is.False);
            Assert.That(noInput, Is.EqualTo(PlayerRef.None));
        }

        [Test]
        public void FND_NET_RPC_InvalidSourceWithLocalAuthorityButInvalidInputAuthority_StaysInvalid()
        {
            var resolved = TryResolve(
                PlayerRef.None,
                PlayerRef.None,
                hasStateAuthority: true,
                hasInputAuthority: true,
                out var requester);

            Assert.That(resolved, Is.False);
            Assert.That(requester, Is.EqualTo(PlayerRef.None));
        }

        [Test]
        public void FND_NET_RPC_ValidSourceDifferentFromOwner_IsNotReplacedByOwner()
        {
            var source = PlayerRef.FromIndex(1);
            var owner = PlayerRef.FromIndex(0);

            var resolved = TryResolve(
                source,
                owner,
                hasStateAuthority: true,
                hasInputAuthority: true,
                out var requester);

            Assert.That(resolved, Is.True);
            Assert.That(requester, Is.EqualTo(source));
            Assert.That(requester, Is.Not.EqualTo(owner));
        }

        [Test]
        public void FND_NET_RPC_LobbyReadyTeamTool_UseResolvedRequesterForValidationAndResponses()
        {
            var source = LoadSource(LobbyPlayerStateScriptPath);

            StringAssert.Contains("TryResolveOwnedRequester(info.Source, out var requester)", source);
            StringAssert.Contains("Runner.TryGetPlayerObject(requester, out var ownedObject)", source);
            StringAssert.Contains("if (!TryResolveRequester(info.Source, out var requester))", source);
            StringAssert.Contains("var error = ValidateOwnedRequest(requester);", source);
            StringAssert.Contains("SendSelectionResult(requester, LobbySelectionKind.Team", source);
            StringAssert.Contains("SendSelectionResult(requester, LobbySelectionKind.Tool", source);
        }

        [Test]
        public void FND_NET_RPC_LobbyUnresolvedRequester_ReturnsBeforeTargetedSelectionResult()
        {
            var source = LoadSource(LobbyPlayerStateScriptPath);

            AssertGuardPrecedes(source, "RpcRequestTeam", "SendSelectionResult(requester, LobbySelectionKind.Team");
            AssertGuardPrecedes(source, "RpcRequestTool", "SendSelectionResult(requester, LobbySelectionKind.Tool");
            StringAssert.DoesNotContain("SendSelectionResult(PlayerRef.None", source);
        }

        [Test]
        public void FND_NET_RPC_Interactor_UsesResolvedRequesterForValidationContextAndResultTarget()
        {
            var source = LoadSource(NetworkPlayerInteractorScriptPath);

            StringAssert.Contains("if (!TryResolveRequester(info.Source, out var requester))", source);
            StringAssert.Contains("var result = ValidateRequester(requester, sequence);", source);
            StringAssert.Contains("new InteractionContext(this, target, requester)", source);
            StringAssert.Contains("RpcInteractionResult(requester, targetId, sequence", source);
        }

        [Test]
        public void FND_NET_RPC_InteractorUnresolvedRequester_ReturnsBeforeSequenceConsumptionAndTargetedResult()
        {
            var source = LoadSource(NetworkPlayerInteractorScriptPath);

            AssertGuardPrecedes(source, "RpcRequestInteraction", "LastProcessedSequence = sequence");
            AssertGuardPrecedes(source, "RpcRequestInteraction", "RpcInteractionResult(requester, targetId, sequence");
            StringAssert.DoesNotContain("RpcInteractionResult(PlayerRef.None", source);
        }

        private static bool TryResolve(
            PlayerRef source,
            PlayerRef owner,
            bool hasStateAuthority,
            bool hasInputAuthority,
            out PlayerRef requester)
        {
            var resolverType = ResolveProductionType("EchoProtocol.Networking.RpcRequesterResolver");
            var method = resolverType.GetMethod(
                "TryResolveEffectiveRequester",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(PlayerRef), typeof(PlayerRef), typeof(bool), typeof(bool), typeof(PlayerRef).MakeByRefType() },
                null);
            Assert.That(method, Is.Not.Null, "Missing RpcRequesterResolver.TryResolveEffectiveRequester production method.");

            var args = new object[] { source, owner, hasStateAuthority, hasInputAuthority, default(PlayerRef) };
            var resolved = (bool)method.Invoke(null, args);
            requester = (PlayerRef)args[4];
            return resolved;
        }

        private static string LoadSource(string path)
        {
            Assert.That(File.Exists(path), Is.True);
            return File.ReadAllText(path);
        }

        private static void AssertGuardPrecedes(string source, string methodName, string laterStatement)
        {
            var methodMarker = $"private void {methodName}(";
            var methodIndex = source.IndexOf(methodMarker, System.StringComparison.Ordinal);
            Assert.That(
                methodIndex,
                Is.GreaterThanOrEqualTo(0),
                $"Missing method declaration '{methodMarker}'.");

            var guardIndex = source.IndexOf("if (!TryResolveRequester(info.Source, out var requester))", methodIndex, System.StringComparison.Ordinal);
            var returnIndex = source.IndexOf("return;", guardIndex, System.StringComparison.Ordinal);
            var laterIndex = source.IndexOf(laterStatement, methodIndex, System.StringComparison.Ordinal);

            Assert.That(guardIndex, Is.GreaterThanOrEqualTo(0), $"Missing requester guard in '{methodName}'.");
            Assert.That(returnIndex, Is.GreaterThan(guardIndex), $"Requester guard in '{methodName}' must return on failure.");
            Assert.That(laterIndex, Is.GreaterThan(returnIndex), $"'{laterStatement}' must occur after unresolved requester return in '{methodName}'.");
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
