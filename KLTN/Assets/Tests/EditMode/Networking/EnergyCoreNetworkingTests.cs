using System;
using System.IO;
using System.Reflection;
using Fusion;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class EnergyCoreNetworkingTests
    {
        private const string CoreSourcePath =
            "Assets/_Project/Scripts/Networking/Interaction/NetworkPickupItem.cs";
        private const string SectorBoxSourcePath =
            "Assets/_Project/Scripts/Networking/Interaction/NetworkSectorBox.cs";

        [Test]
        public void M2_CORE_Pickup_AcceptsAvailableOrDroppedUnownedCore()
        {
            Assert.That(CanPickup("Available", PlayerRef.None, true, false), Is.True);
            Assert.That(CanPickup("Dropped", PlayerRef.None, true, false), Is.True);
        }

        [Test]
        public void M2_CORE_Pickup_RejectsMissingPlayerExistingHolderOrSecondCore()
        {
            var holder = PlayerRef.FromIndex(0);

            Assert.That(CanPickup("Available", PlayerRef.None, false, false), Is.False);
            Assert.That(CanPickup("Available", holder, true, false), Is.False);
            Assert.That(CanPickup("Available", PlayerRef.None, true, true), Is.False);
            Assert.That(CanPickup("Placed", PlayerRef.None, true, false), Is.False);
        }

        [Test]
        public void M2_CORE_DropAndPlace_AcceptOnlyCurrentHolder()
        {
            var holder = PlayerRef.FromIndex(0);
            var otherPlayer = PlayerRef.FromIndex(1);

            Assert.That(CanDrop("Carried", holder, holder), Is.True);
            Assert.That(CanDrop("Carried", holder, otherPlayer), Is.False);
            Assert.That(CanDrop("Dropped", holder, holder), Is.False);
            Assert.That(CanPlace("Carried", holder, holder), Is.True);
            Assert.That(CanPlace("Placed", holder, holder), Is.False);
        }

        [Test]
        public void M2_CORE_ObjectiveCount_StopsAtRequiredCount()
        {
            Assert.That(CanRegisterPlacement(0, 3), Is.True);
            Assert.That(CanRegisterPlacement(2, 3), Is.True);
            Assert.That(CanRegisterPlacement(3, 3), Is.False);
            Assert.That(CanRegisterPlacement(4, 3), Is.False);
        }

        [Test]
        public void M2_CORE_PersistentResults_AreNetworkedSemanticState()
        {
            var coreSource = File.ReadAllText(CoreSourcePath);
            var sectorSource = File.ReadAllText(SectorBoxSourcePath);

            StringAssert.Contains("[Networked, OnChangedRender(nameof(ApplyReplicatedState))]", coreSource);
            StringAssert.Contains("public PlayerRef Holder", coreSource);
            StringAssert.Contains("public Vector3 WorldPosition", coreSource);
            StringAssert.Contains("public NetworkId PlacedSectorId", coreSource);
            StringAssert.Contains("public int PlacementSlot", coreSource);
            StringAssert.Contains("public int PlacedCoreCount", sectorSource);
            StringAssert.DoesNotContain("RPC_OpenCore", coreSource);
        }

        private static bool CanPickup(string stateName, PlayerRef holder, bool playerExists, bool playerAlreadyCarriesCore)
        {
            var rulesType = ResolveProductionType("EchoProtocol.Networking.EnergyCoreAuthorityRules");
            var stateEnumType = ResolveProductionType("EchoProtocol.Networking.NetworkItemState");
            var stateValue = Enum.Parse(stateEnumType, stateName);
            var method = rulesType.GetMethod("CanPickup", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing EnergyCoreAuthorityRules.CanPickup method.");
            return (bool)method.Invoke(null, new object[] { stateValue, holder, playerExists, playerAlreadyCarriesCore });
        }

        private static bool CanDrop(string stateName, PlayerRef holder, PlayerRef requester)
        {
            var rulesType = ResolveProductionType("EchoProtocol.Networking.EnergyCoreAuthorityRules");
            var stateEnumType = ResolveProductionType("EchoProtocol.Networking.NetworkItemState");
            var stateValue = Enum.Parse(stateEnumType, stateName);
            var method = rulesType.GetMethod("CanDrop", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing EnergyCoreAuthorityRules.CanDrop method.");
            return (bool)method.Invoke(null, new object[] { stateValue, holder, requester });
        }

        private static bool CanPlace(string stateName, PlayerRef holder, PlayerRef requester)
        {
            var rulesType = ResolveProductionType("EchoProtocol.Networking.EnergyCoreAuthorityRules");
            var stateEnumType = ResolveProductionType("EchoProtocol.Networking.NetworkItemState");
            var stateValue = Enum.Parse(stateEnumType, stateName);
            var method = rulesType.GetMethod("CanPlace", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing EnergyCoreAuthorityRules.CanPlace method.");
            return (bool)method.Invoke(null, new object[] { stateValue, holder, requester });
        }

        private static bool CanRegisterPlacement(int currentCount, int requiredCount)
        {
            var rulesType = ResolveProductionType("EchoProtocol.Networking.EnergyCoreObjectiveRules");
            var method = rulesType.GetMethod("CanRegisterPlacement", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing EnergyCoreObjectiveRules.CanRegisterPlacement method.");
            return (bool)method.Invoke(null, new object[] { currentCount, requiredCount });
        }

        private static Type ResolveProductionType(string fullTypeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var resolved = assembly.GetType(fullTypeName, false);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            var asmCSharp = Assembly.Load("Assembly-CSharp");
            var type = asmCSharp.GetType(fullTypeName, false);
            if (type != null)
            {
                return type;
            }

            Assert.Fail($"Could not resolve production type '{fullTypeName}'.");
            return null;
        }
    }
}