using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using EchoProtocol.Salvage;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

public sealed class SalvageEnemyTests
{
    [TestCase(1,2,8,1,2)] [TestCase(3,4,8,1,3)] [TestCase(5,2,8,2,4)]
    [TestCase(8,4,8,3,6)] [TestCase(100,2,10,2,4)] [TestCase(100,4,6,2,6)]
    public void PopulationHasLevelCrewAndRoomCaps(int level,int crew,int rooms,int main,int minions)
    {
        Assert.That(SalvageRunState.MainEnemyLimit(level,crew,rooms),Is.EqualTo(main));
        Assert.That(SalvageRunState.MinionLimit(level,crew),Is.EqualTo(minions));
        Assert.That(main+minions,Is.LessThanOrEqualTo(9));
    }
    [Test] public void SixNetworkPrefabsHaveCompatibleAgentsAndExplicitRoles()
    {
        var all=Resources.LoadAll<NetworkObject>("Salvage/Enemies");Assert.That(all.Length,Is.EqualTo(6));
        int main=0;
        foreach(var prefab in all)
        {
            var policy=prefab.GetComponent<SalvageEnemy>();Assert.That(policy,Is.Not.Null);
            if(policy.Main)main++;
            var agent=prefab.GetComponent<NavMeshAgent>();Assert.That(agent.agentTypeID,Is.Zero);
            Assert.That(agent.radius,Is.EqualTo(.4f).Within(.001f));
        }
        Assert.That(main,Is.EqualTo(3));
    }
}
