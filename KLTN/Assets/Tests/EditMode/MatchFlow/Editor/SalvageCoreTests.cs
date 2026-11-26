using EchoProtocol.Salvage;
using NUnit.Framework;

public sealed class SalvageCoreTests
{
    [Test]
    public void HostCommandRpcCarriesHostPlayerIdentity()
    {
        var method = typeof(SalvageRunState).GetMethod("RPC_Command",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var rpc = (Fusion.RpcAttribute)System.Attribute.GetCustomAttribute(method, typeof(Fusion.RpcAttribute));
        Assert.That(rpc.HostMode, Is.EqualTo(Fusion.RpcHostMode.SourceIsHostPlayer),
            "Default host RPC source is None, which is valid but is not a real player.");
    }

    [TestCase(true, 2, false, true)]
    [TestCase(true, 2, true, false)]
    [TestCase(false, 2, false, false)]
    [TestCase(true, 0, false, false)]
    [TestCase(true, 1, false, false)]
    [TestCase(true, -1, false, false)]
    [TestCase(true, 3, false, false)]
    public void BackendStartBypassRequiresDevelopmentSalvageAndAllowsOptOut(
        bool development, int mode, bool requireBackend, bool expected)
    {
        Assert.That(SalvagePlaytest.AllowsBackendStartBypass(development, mode, requireBackend), Is.EqualTo(expected));
    }

    private static SalvageLedger Day(int crew = 2) { var run = new SalvageLedger(crew); Assert.That(run.Depart(crew), Is.True); return run; }
    private static int Item(SalvageLedger run, int type = 2, int value = 80)
    { Assert.That(run.TryCreate(type,value,out var index),Is.True); return index; }
    private static void Store(SalvageLedger run,int index,int owner = 0)
    { Assert.That(run.TryPickup(index,run.Cargo[index].Serial,owner,out _),Is.True); Assert.That(run.Store(owner),Is.EqualTo(1)); }

    [TestCase(2,600)] [TestCase(3,720)] [TestCase(4,840)]
    public void QuotaLocksCrewAtCycleStart(int crew,int quota)
    { var run=Day(crew); Assert.That(run.Quota,Is.EqualTo(quota)); Assert.That(run.CrewAtCycleStart,Is.EqualTo(crew)); }
    [Test]
    public void PickupIsExclusiveAndCannotReplayAfterSale()
    {
        var run=Day(); int i=Item(run); int serial=run.Cargo[i].Serial;
        Assert.That(run.TryPickup(i,serial,0,out _),Is.True);
        Assert.That(run.TryPickup(i,serial,1,out _),Is.False);
        run.Store(0); Assert.That(run.TrySell(i,serial),Is.True,"Delivered cargo can be sold at the terminal during a Day.");
        run.Return(new bool[4]); Assert.That(run.TrySell(i,serial),Is.False);
        int funds=run.Funds; Assert.That(run.TrySell(i,serial),Is.False); Assert.That(run.Funds,Is.EqualTo(funds));
        run.Depart(2); int reused=Item(run); Assert.That(reused,Is.EqualTo(i));
        Assert.That(run.Cargo[reused].Serial,Is.Not.EqualTo(serial));
        Assert.That(run.TryPickup(reused,serial,0,out _),Is.False);
    }
    [Test]
    public void FourSlotsAndHeavyContiguousOccupancy()
    {
        var run=Day(); int a=Item(run),b=Item(run),heavy=Item(run,0,240);
        run.TryPickup(a,run.Cargo[a].Serial,0,out _); run.TryPickup(b,run.Cargo[b].Serial,0,out _);
        Assert.That(run.TryPickup(heavy,run.Cargo[heavy].Serial,0,out int slot),Is.True); Assert.That(slot,Is.EqualTo(2));
        Assert.That(run.FindHeld(0,3),Is.EqualTo(heavy));
        int extra=Item(run); Assert.That(run.TryPickup(extra,run.Cargo[extra].Serial,0,out _),Is.False);
        Assert.That(run.Weight(0),Is.EqualTo(24)); Assert.That(run.HeavyHeld(0),Is.EqualTo(heavy));
    }
    [Test]
    public void FragileDamageOnlyAffectsWorldCargoAndReducesItsSaleValue()
    {
        var run=Day(); int i=Item(run,6,150); int serial=run.Cargo[i].Serial;
        run.TryPickup(i,serial,0,out _); Assert.That(run.Damage(i,serial,40),Is.False);
        run.TryDrop(i,serial,0); Assert.That(run.Damage(i,serial,40),Is.True);
        Assert.That(run.Cargo[i].Value,Is.EqualTo(90)); Store(run,i); run.Return(new bool[4]);
        run.TrySell(i,serial); Assert.That(run.Funds,Is.Zero); Assert.That(run.SoldThisCycle,Is.EqualTo(90));
    }
    [Test]
    public void WipeLosesWorldAndCarriedCargoButKeepsStoredCargo()
    {
        var run=Day(); int stored=Item(run),held=Item(run),world=Item(run); Store(run,stored);
        run.TryPickup(held,run.Cargo[held].Serial,0,out _); run.Return(new bool[4]);
        Assert.That(run.Cargo[stored].State,Is.EqualTo(SalvageCargoState.Stored));
        Assert.That(run.Cargo[held].State,Is.EqualTo(SalvageCargoState.Destroyed));
        Assert.That(run.Cargo[world].State,Is.EqualTo(SalvageCargoState.Destroyed));
        Assert.That(run.Phase,Is.EqualTo(SalvagePhase.DaySummary)); Assert.That(run.Depart(2),Is.True);
        Assert.That(run.Day,Is.EqualTo(2)); Assert.That(run.Cargo[stored].State,Is.EqualTo(SalvageCargoState.Stored));
    }
    [Test]
    public void SaleCanMeetQuotaAtBaseAfterDayThreeAndUnSoldGoodsCarryToNextCycle()
    {
        var run=Day(); int[] sold=new int[3]; for(int i=0;i<3;i++){sold[i]=Item(run,0,240);Store(run,sold[i]);}
        int carry=Item(run); Store(run,carry);
        run.Return(new bool[4]); run.Depart(2); run.Return(new bool[4]); run.Depart(2); run.Return(new bool[4]);
        foreach(var i in sold) Assert.That(run.TrySell(i,run.Cargo[i].Serial),Is.True);
        Assert.That(run.Depart(4),Is.False); Assert.That(run.Phase,Is.EqualTo(SalvagePhase.CycleSummary));
        Assert.That(run.Cycle,Is.EqualTo(2)); Assert.That(run.Quota,Is.EqualTo(1260));
        Assert.That(run.Cargo[carry].State,Is.EqualTo(SalvageCargoState.Stored)); Assert.That(run.SoldThisCycle,Is.Zero);
        Assert.That(run.Depart(4),Is.True); Assert.That(run.Day,Is.EqualTo(1));
    }
    [Test]
    public void FailedQuotaEndsRunInsteadOfRestartingDeadline()
    {
        var run=Day(); for(int i=0;i<3;i++){run.Return(new bool[4]);if(i<2)run.Depart(2);}
        Assert.That(run.Depart(2),Is.False); Assert.That(run.Phase,Is.EqualTo(SalvagePhase.Failed));
        Assert.That(run.Depart(2),Is.False); Assert.That(run.TryPurchase(2,true),Is.False);
    }
    [Test]
    public void SellAllChecksConfirmedRevisionAndTotalAndCannotReplay()
    {
        var run=Day();int a=Item(run),b=Item(run,8,100);Store(run,a);Store(run,b);run.Return(new bool[4]);
        int revision=run.Revision,total=run.StoredValue();
        Assert.That(run.TrySellAll(revision-1,total),Is.False);
        Assert.That(run.TrySellAll(revision,total+1),Is.False);
        Assert.That(run.Funds,Is.Zero);
        Assert.That(run.TrySellAll(revision,total),Is.True);
        Assert.That(run.Funds,Is.Zero);Assert.That(run.SoldThisCycle,Is.EqualTo(total));
        Assert.That(run.TrySellAll(revision,total),Is.False);Assert.That(run.StoredValue(),Is.Zero);
    }
    [Test]
    public void OutOfBoundsCargoIsLostOnceAndNeverMovedToBaseForFree()
    {
        var run=Day();int i=Item(run,0,240),serial=run.Cargo[i].Serial;
        run.TryPickup(i,serial,0,out _);Assert.That(run.LoseWorldCargo(i,serial),Is.False);
        run.TryDrop(i,serial,0);Assert.That(run.LoseWorldCargo(i,serial),Is.True);
        Assert.That(run.LoseWorldCargo(i,serial),Is.False);Assert.That(run.LastDayLost,Is.EqualTo(240));
        Assert.That(run.TryPickup(i,serial,0,out _),Is.False);Assert.That(run.Cargo[i].State,Is.EqualTo(SalvageCargoState.Destroyed));
    }
    [Test]
    public void EquipmentPurchaseChecksFundsPhaseAndOccupiedSlot()
    {
        var run=new SalvageLedger(2); Assert.That(run.TryPurchase(1,true),Is.False);
        Assert.That(run.TryPurchase(2,false),Is.False); Assert.That(run.Funds,Is.Zero);
        Assert.That(run.Depart(2),Is.True); for(int n=0;n<3;n++){int i=Item(run,0,240);Store(run,i);Assert.That(run.TrySell(i,run.Cargo[i].Serial),Is.True);}run.Return(new bool[4]);
        Assert.That(run.Funds,Is.EqualTo(120));Assert.That(run.TryPurchase(2,true),Is.True); Assert.That(run.Funds,Is.EqualTo(80));
        Assert.That(run.TryPurchase(6,true),Is.False); run.Depart(2); Assert.That(run.TryPurchase(3,true),Is.False);
    }
    [TestCase(0,1)] [TestCase(10,1)] [TestCase(25,.85f)] [TestCase(40,.75f)] [TestCase(100,.75f)]
    public void WeightPenaltyHasApprovedBounds(float weight,float multiplier)
        => Assert.That(SalvageLedger.CarrySpeed(weight),Is.EqualTo(multiplier).Within(.001f));
    [Test]
    public void SnapshotRoundTripPreservesMoneySerialsCargoAndDeadline()
    {
        var original=Day(3); int i=Item(original,8,100); Store(original,i); original.Return(new bool[4]);
        var restored=new SalvageLedger(2);
        restored.Restore(original.Phase,original.Cycle,original.Day,original.Funds,original.Quota,original.SoldThisCycle,
            original.TotalSold,original.NextSerial,original.CrewAtCycleStart,original.DayCompleted,original.Revision,original.LastDayRecovered,original.LastDayLost);
        System.Array.Copy(original.Cargo,restored.Cargo,SalvageLedger.Capacity);
        Assert.That(restored.TrySell(i,original.Cargo[i].Serial),Is.True); Assert.That(restored.Funds,Is.Zero); Assert.That(restored.SoldThisCycle,Is.EqualTo(100));
        Assert.That(restored.TrySell(i,original.Cargo[i].Serial),Is.False); Assert.That(restored.Depart(4),Is.True);
        Assert.That(restored.Day,Is.EqualTo(2)); Assert.That(restored.Quota,Is.EqualTo(720));
    }
}
