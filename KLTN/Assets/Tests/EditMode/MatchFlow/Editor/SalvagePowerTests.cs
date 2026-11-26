using NUnit.Framework;
using EchoProtocol.Salvage;
using UnityEngine;

public sealed class SalvagePowerTests
{
    [TestCase(0)] [TestCase(1)] [TestCase(3)] [TestCase(10)] [TestCase(20)] [TestCase(50)] [TestCase(100)]
    public void HealthAndStaminaUseFlatBonusesWithoutGameplayRankCap(int purchases)
    {
        Assert.That(SalvageRunState.FlatPowerBonus(SalvagePower.Health,purchases),Is.EqualTo(20f*purchases));
        Assert.That(SalvageRunState.FlatPowerBonus(SalvagePower.Stamina,purchases),Is.EqualTo(10f*purchases));
        Assert.That(SalvageRunState.CanUpgradePower(1,purchases),Is.True);
        Assert.That(SalvageRunState.CanUpgradePower(2,purchases),Is.True);
        Assert.That(SalvageRunState.PowerPrice(1,purchases),Is.Positive);
        Assert.That(SalvageRunState.PowerPrice(2,purchases),Is.Positive);
    }
    [Test] public void DeepRunPricesRemainPositiveAndCannotWrap()
    {
        Assert.That(SalvageRunState.PowerPrice(1,0),Is.EqualTo(75));
        Assert.That(SalvageRunState.PowerPrice(1,1),Is.EqualTo(105));
        Assert.That(SalvageRunState.PowerPrice(2,0),Is.EqualTo(50));
        Assert.That(SalvageRunState.PowerPrice(2,1),Is.EqualTo(68));
        foreach(int type in new[]{1,2})
        {
            Assert.That(SalvageRunState.PowerPrice(type,1000000),Is.EqualTo(int.MaxValue));
            Assert.That(SalvageRunState.PowerPrice(type,int.MaxValue-1),Is.EqualTo(int.MaxValue));
            Assert.That(SalvageRunState.PowerPrice(type,int.MaxValue),Is.EqualTo(-1));
            Assert.That(SalvageRunState.PowerPrice(type,-1),Is.EqualTo(-1));
        }
    }
    [Test] public void PricesEscalateAndStopAtThreeRanks()
    {
        Assert.That(SalvageRunState.PowerPrice(0,0), Is.EqualTo(75));
        Assert.That(SalvageRunState.PowerPrice(0,1), Is.EqualTo(150));
        Assert.That(SalvageRunState.PowerPrice(0,2), Is.EqualTo(300));
        Assert.That(SalvageRunState.PowerPrice(0,3), Is.EqualTo(-1));
        Assert.That(SalvageRunState.PowerPrice(0,-1), Is.EqualTo(-1));
    }
    [Test] public void CommonFundsCannotOverspendOrBuyDuringDay()
    {
        var ledger = new SalvageLedger(2);
        Assert.That(ledger.Funds, Is.Zero);
        Assert.That(ledger.TrySpendPower(75), Is.False);
        ledger.Depart(2);
        foreach(int value in new[]{240,240,220})
        {
            Assert.That(ledger.TryCreate(0,value,out int i),Is.True);
            int serial=ledger.Cargo[i].Serial;Assert.That(ledger.AcceptDelivery(i,serial),Is.True);
            Assert.That(ledger.TrySell(i,serial),Is.True);
        }
        ledger.Return(new bool[4]);
        Assert.That(ledger.Funds,Is.EqualTo(100));
        Assert.That(ledger.TrySpendPower(75), Is.True);
        Assert.That(ledger.Funds, Is.EqualTo(25));
        Assert.That(ledger.TrySpendPower(75), Is.False);
        Assert.That(ledger.TrySpendPower(-1), Is.False);
        Assert.That(ledger.TrySpendPower(0), Is.False);
        ledger.Depart(2);
        Assert.That(ledger.TrySpendPower(1), Is.False);
        Assert.That(ledger.Funds, Is.EqualTo(25));
    }
    [Test] public void PlayersWithoutAnActiveSalvageRunKeepTheirBaseStats()
    {
        var player = new GameObject("Unbound player");
        try { foreach (SalvagePower power in System.Enum.GetValues(typeof(SalvagePower)))
            { Assert.That(SalvageRunState.PowerMultiplier(player,power), Is.EqualTo(1)); Assert.That(SalvageRunState.PowerBonus(player,power), Is.Zero); } }
        finally { Object.DestroyImmediate(player); }
    }
}
