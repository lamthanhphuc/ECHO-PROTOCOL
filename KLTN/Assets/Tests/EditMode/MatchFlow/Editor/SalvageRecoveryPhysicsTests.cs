using NUnit.Framework;
using EchoProtocol.Salvage;
public sealed class SalvageRecoveryPhysicsTests
{
    private static int Add(SalvageLedger run,int definition)
    {Assert.That(run.TryCreate(definition,SalvageLedger.Types[definition].MinValue,out int i),Is.True);return i;}
    [Test]
    public void FullBagStillAllowsExclusiveExternalGrip()
    {
        var run=new SalvageLedger(2);run.Depart(2);
        for(int n=0;n<4;n++){int i=Add(run,12);Assert.That(run.TryPickup(i,run.Cargo[i].Serial,0,out _),Is.True);}
        int extra=Add(run,13),serial=run.Cargo[extra].Serial;
        Assert.That(run.TryPickup(extra,serial,0,out _),Is.False);
        Assert.That(run.TryGrab(extra,serial,0),Is.True);Assert.That(run.TryGrab(extra,serial,1),Is.False);
        Assert.That(run.PhysicsHeld(0),Is.EqualTo(extra));Assert.That(run.Cargo[extra].Slot,Is.EqualTo(-1));
        Assert.That(run.TryRelease(extra,serial,1),Is.False);Assert.That(run.TryRelease(extra,serial,0),Is.True);
        Assert.That(run.FindHeld(0,3),Is.GreaterThanOrEqualTo(0));
    }
    [Test]
    public void FurnitureNeverEntersBagButCanBeDeliveredAndSoldExactlyOnce()
    {
        var run=new SalvageLedger(2);run.Depart(2);int i=Add(run,16),serial=run.Cargo[i].Serial;
        Assert.That(run.TryPickup(i,serial,0,out _),Is.False);Assert.That(run.TryGrab(i,serial,0),Is.True);
        Assert.That(run.AcceptDelivery(i,serial),Is.False,"Held objects cannot be sold under another player's grip");
        run.TryRelease(i,serial,0);Assert.That(run.AcceptDelivery(i,serial),Is.True);
        int funds=run.Funds;Assert.That(run.TrySell(i,serial),Is.True);Assert.That(run.TrySell(i,serial),Is.False);
        Assert.That(run.Funds,Is.EqualTo(funds));Assert.That(run.SoldThisCycle,Is.EqualTo(SalvageLedger.Types[16].MinValue));
    }
    [Test]
    public void ReclaimingStockRemovesItFromSaleAndWipeKeepsDeliveredItems()
    {
        var run=new SalvageLedger(2);run.Depart(2);int delivered=Add(run,16),held=Add(run,17);
        run.AcceptDelivery(delivered,run.Cargo[delivered].Serial);run.AcceptDelivery(held,run.Cargo[held].Serial);
        Assert.That(run.TryGrab(held,run.Cargo[held].Serial,1),Is.True);Assert.That(run.TrySell(held,run.Cargo[held].Serial),Is.False);
        run.Return(new bool[4]);Assert.That(run.Cargo[delivered].State,Is.EqualTo(SalvageCargoState.Stored));
        Assert.That(run.Cargo[held].State,Is.EqualTo(SalvageCargoState.Destroyed));
    }
    [Test]
    public void NewCatalogHasBothPocketAndPhysicsOnlyProps()
    {
        Assert.That(SalvageLedger.CartDefinition,Is.EqualTo(34));
        Assert.That(SalvageLedger.Types[12].Pocketable,Is.True);Assert.That(SalvageLedger.Types[13].Pocketable,Is.True);
        Assert.That(SalvageLedger.Types[16].Pocketable,Is.False);Assert.That(SalvageLedger.Types[30].Pocketable,Is.False);
    }
    [Test]
    public void CollisionDamageScalesWithImpactAndFragilityAndIgnoresGentleContacts()
    {
        Assert.That(SalvageCollisionDamage.IntegrityLoss(1.8f,20,false,SalvageCargoMaterial.Metal),Is.Zero);
        int light=SalvageCollisionDamage.IntegrityLoss(2.5f,10,false,SalvageCargoMaterial.Metal);
        int hard=SalvageCollisionDamage.IntegrityLoss(5,10,false,SalvageCargoMaterial.Metal);
        Assert.That(hard,Is.GreaterThan(light));
        Assert.That(SalvageCollisionDamage.IntegrityLoss(5,10,true,SalvageCargoMaterial.Metal),Is.GreaterThan(hard));
        var run=new SalvageLedger(2);run.Depart(2);int i=Add(run,16),serial=run.Cargo[i].Serial;
        run.TryGrab(i,serial,0);int before=run.Cargo[i].Value;
        Assert.That(run.Damage(i,serial,light,true),Is.True);Assert.That(run.Cargo[i].Value,Is.LessThan(before));
        Assert.That(run.Damage(i,serial,100,true),Is.True);Assert.That(run.Cargo[i].State,Is.EqualTo(SalvageCargoState.Destroyed));
        Assert.That(run.Cargo[i].Value,Is.Zero);Assert.That(run.TrySell(i,serial),Is.False);
    }
}
