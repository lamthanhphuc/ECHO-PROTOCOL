using System.IO;
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
            Assert.That(EnergyCoreAuthorityRules.CanPickup(
                NetworkItemState.Available, PlayerRef.None, true, false), Is.True);
            Assert.That(EnergyCoreAuthorityRules.CanPickup(
                NetworkItemState.Dropped, PlayerRef.None, true, false), Is.True);
        }

        [Test]
        public void M2_CORE_Pickup_RejectsMissingPlayerExistingHolderOrSecondCore()
        {
            var holder = PlayerRef.FromIndex(0);

            Assert.That(EnergyCoreAuthorityRules.CanPickup(
                NetworkItemState.Available, PlayerRef.None, false, false), Is.False);
            Assert.That(EnergyCoreAuthorityRules.CanPickup(
                NetworkItemState.Available, holder, true, false), Is.False);
            Assert.That(EnergyCoreAuthorityRules.CanPickup(
                NetworkItemState.Available, PlayerRef.None, true, true), Is.False);
            Assert.That(EnergyCoreAuthorityRules.CanPickup(
                NetworkItemState.Placed, PlayerRef.None, true, false), Is.False);
        }

        [Test]
        public void M2_CORE_DropAndPlace_AcceptOnlyCurrentHolder()
        {
            var holder = PlayerRef.FromIndex(0);
            var otherPlayer = PlayerRef.FromIndex(1);

            Assert.That(EnergyCoreAuthorityRules.CanDrop(NetworkItemState.Carried, holder, holder), Is.True);
            Assert.That(EnergyCoreAuthorityRules.CanDrop(NetworkItemState.Carried, holder, otherPlayer), Is.False);
            Assert.That(EnergyCoreAuthorityRules.CanDrop(NetworkItemState.Dropped, holder, holder), Is.False);
            Assert.That(EnergyCoreAuthorityRules.CanPlace(NetworkItemState.Carried, holder, holder), Is.True);
            Assert.That(EnergyCoreAuthorityRules.CanPlace(NetworkItemState.Placed, holder, holder), Is.False);
        }

        [Test]
        public void M2_CORE_ObjectiveCount_StopsAtRequiredCount()
        {
            Assert.That(EnergyCoreObjectiveRules.CanRegisterPlacement(0, 3), Is.True);
            Assert.That(EnergyCoreObjectiveRules.CanRegisterPlacement(2, 3), Is.True);
            Assert.That(EnergyCoreObjectiveRules.CanRegisterPlacement(3, 3), Is.False);
            Assert.That(EnergyCoreObjectiveRules.CanRegisterPlacement(4, 3), Is.False);
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
    }
}
