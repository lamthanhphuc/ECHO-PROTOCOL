using System.IO;
using NUnit.Framework;

namespace EchoProtocol.Networking.Tests
{
    public sealed class CreepMinionContractTests
    {
        private const string MinionSource = "Assets/Scripts/AI/Minions/CreepMinionRuntime.cs";
        private const string SpawnerSource = "Assets/_Project/Scripts/Networking/Player/PlayerSpawner.cs";

        [Test]
        public void CreepAlert_HasRejectedAlertRetry()
        {
            string source = File.ReadAllText(MinionSource);
            StringAssert.Contains("_stalkerAlertDeliveredForTarget", source);
            StringAssert.Contains("alertRetrySeconds", source);
            StringAssert.Contains("TrySendStalkerAlert", source);
        }

        [Test]
        public void CreepToolRelocation_RequiresCompletePath()
        {
            string source = File.ReadAllText(MinionSource);
            StringAssert.Contains("NavMesh.CalculatePath", source);
            StringAssert.Contains("NavMeshPathStatus.PathComplete", source);
        }

        [Test]
        public void Zone2Minions_PreserveZone2Anchor()
        {
            string source = File.ReadAllText(SpawnerSource);
            StringAssert.Contains("_zone2AnchorPlayer", source);
            StringAssert.Contains("_zone2MonsterInstance", source);
            StringAssert.Contains("_zone2MinionAnchorFallbackRadius", source);
        }
    }
}
