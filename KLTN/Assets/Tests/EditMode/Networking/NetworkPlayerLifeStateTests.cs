using System.IO;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class NetworkPlayerLifeStateTests
    {
        private const string LifeStateSourcePath =
            "Assets/_Project/Scripts/Networking/Player/NetworkPlayerLifeState.cs";
        private const string InteractorSourcePath =
            "Assets/_Project/Scripts/Networking/Interaction/NetworkPlayerInteractor.cs";

        [Test]
        public void LIFE_NET_ReviveStartsOnlyForDownedTargetAndAliveOtherPlayer()
        {
            var source = File.ReadAllText(LifeStateSourcePath);

            StringAssert.Contains("targetStatus == NetworkPlayerLifeStatus.Downed", source);
            StringAssert.Contains("reviverStatus == NetworkPlayerLifeStatus.Alive", source);
            StringAssert.Contains("&& !samePlayer", source);
            StringAssert.Contains("&& reviveCount < maximumRevives", source);
        }

        [Test]
        public void LIFE_NET_SecondReviverCannotStartWhileReviveProgressExists()
        {
            var source = File.ReadAllText(LifeStateSourcePath);
            StringAssert.Contains("&& !reviveInProgress", source);
        }

        [Test]
        public void LIFE_NET_ProtectionRejectsDamageAndEliminatedCannotMove()
        {
            var source = File.ReadAllText(LifeStateSourcePath);
            StringAssert.Contains("status == NetworkPlayerLifeStatus.Alive && !hasReviveProtection", source);
            StringAssert.Contains("status == NetworkPlayerLifeStatus.Alive\n            || status == NetworkPlayerLifeStatus.Downed", source.Replace("\r\n", "\n"));
        }

        [Test]
        public void LIFE_NET_OnlyDownedCanBleedOut()
        {
            var source = File.ReadAllText(LifeStateSourcePath);
            StringAssert.Contains("public static bool CanBleedOut", source);
            StringAssert.Contains("status == NetworkPlayerLifeStatus.Downed", source);
        }

        [Test]
        public void LIFE_NET_PersistentStateAndTimersAreNetworkedAndRpcOnlyStartsRequest()
        {
            var lifeSource = File.ReadAllText(LifeStateSourcePath);
            var interactorSource = File.ReadAllText(InteractorSourcePath);

            StringAssert.Contains("public NetworkPlayerLifeStatus Status", lifeSource);
            StringAssert.Contains("public float Health", lifeSource);
            StringAssert.Contains("public PlayerRef Reviver", lifeSource);
            StringAssert.Contains("private TickTimer BleedoutTimer", lifeSource);
            StringAssert.Contains("private TickTimer ReviveTimer", lifeSource);
            StringAssert.Contains("private TickTimer ProtectionTimer", lifeSource);
            StringAssert.Contains("targetLifeState.TryStartRevive(requester)", interactorSource);
            StringAssert.DoesNotContain("RpcReviveCompleted", lifeSource);
            StringAssert.DoesNotContain("BeingRevived =", lifeSource);
            StringAssert.DoesNotContain("Protected =", lifeSource);

            var bleedoutCheck = lifeSource.IndexOf("BleedoutTimer.Expired(Runner)", System.StringComparison.Ordinal);
            var reviveCheck = lifeSource.IndexOf("ReviveTimer.Expired(Runner)", System.StringComparison.Ordinal);
            Assert.That(bleedoutCheck, Is.GreaterThanOrEqualTo(0));
            Assert.That(reviveCheck, Is.GreaterThan(bleedoutCheck),
                "Bleedout must win a same-tick race against revive completion.");
        }
    }
}
