using NUnit.Framework;
using EchoProtocol.Salvage;

public sealed class SalvageEconomyTests
{
    private static int Stock(SalvageLedger run,int value)
    {
        Assert.That(run.TryCreate(0,value,out int i),Is.True);
        Assert.That(run.AcceptDelivery(i,run.Cargo[i].Serial),Is.True);return i;
    }
    private static void Sell(SalvageLedger run,int value)
    { int i=Stock(run,value);Assert.That(run.TrySell(i,run.Cargo[i].Serial),Is.True); }
    [Test] public void StartsWithoutMoneyOrPurchasingPower()
    {
        var run=new SalvageLedger(2);Assert.That(run.Funds,Is.Zero);
        Assert.That(run.TryPurchase(2,true),Is.False);Assert.That(run.TrySpendPower(75),Is.False);
    }
    [Test] public void ExactTargetPaysNoSurplusAndOnlyExcessCanBeSpent()
    {
        var run=new SalvageLedger(2);run.Depart(2);
        Sell(run,240);Sell(run,200);Sell(run,160);
        Assert.That(run.SoldThisCycle,Is.EqualTo(600));Assert.That(run.Funds,Is.Zero);
        Sell(run,180);Assert.That(run.Funds,Is.EqualTo(180));
        int remaining=Stock(run,160);
        run.Return(new bool[4]);Assert.That(run.TrySpendPower(75),Is.True);
        Assert.That(run.Funds,Is.EqualTo(105));
        Assert.That(run.TrySell(remaining,run.Cargo[remaining].Serial),Is.True);
        Assert.That(run.Funds,Is.EqualTo(265),"Spent surplus must never be credited again");
    }
    [Test] public void BulkSaleCrossingTargetMatchesIndividualSalesAndCannotReplay()
    {
        var bulk=new SalvageLedger(2);bulk.Depart(2);
        for(int n=0;n<3;n++)Stock(bulk,240);
        int revision=bulk.Revision;Assert.That(bulk.TrySellAll(revision,720),Is.True);
        Assert.That(bulk.Funds,Is.EqualTo(120));Assert.That(bulk.SoldThisCycle,Is.EqualTo(720));
        Assert.That(bulk.TrySellAll(revision,720),Is.False);Assert.That(bulk.Funds,Is.EqualTo(120));
        var individual=new SalvageLedger(2);individual.Depart(2);
        for(int n=0;n<3;n++)Sell(individual,240);
        Assert.That(individual.Funds,Is.EqualTo(bulk.Funds));
    }
    [Test] public void UnspentSurplusCarriesButNewLevelMustEarnItsOwnTarget()
    {
        var run=new SalvageLedger(2);run.Depart(2);for(int n=0;n<3;n++)Sell(run,240);
        for(int day=0;day<3;day++){run.Return(new bool[4]);if(day<2)run.Depart(2);}
        Assert.That(run.Depart(2),Is.False);Assert.That(run.Cycle,Is.EqualTo(2));
        Assert.That(run.SoldThisCycle,Is.Zero);Assert.That(run.Funds,Is.EqualTo(120));
        run.Depart(2);Sell(run,240);Assert.That(run.Funds,Is.EqualTo(120));
    }
}
