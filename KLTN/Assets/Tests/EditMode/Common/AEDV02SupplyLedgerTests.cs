using System;
using EchoProtocol.AI.Common.AED;
using NUnit.Framework;

public sealed class AEDV02SupplyLedgerTests
{
    [Test]
    public void AEDV02_UnavailableOptionalSlotNeverCancelsRequiredZoneOrInventsMaterializedSupply()
    {
        var ledger = new AEDV02SupplyLedger(Guid.NewGuid(), 2);
        Assert.That(ledger.TryRecordZoneSpawn(1, 5, -1), Is.False);
        Assert.That(ledger.TryRecordZoneSpawn(1, 5, 2), Is.False);
        Assert.That(ledger.TryRecordZoneSpawn(1, 5, 0), Is.True);
        Assert.That(ledger.OptionalMaterialized, Is.Zero);
        Assert.That(ledger.TryRecordZoneSpawn(2, 5, 1), Is.True);
        Assert.That(ledger.OptionalMaterialized, Is.EqualTo(1));
        Assert.That(ledger.TrySetOptionalBudget(1), Is.False, "A missing optional slot in an already spawned zone cannot become a new squeeze transaction.");
    }
    [Test]
    public void AEDV02_OptionalReductionTouchesOnlyUnspawnedReservationsAndKeepsFiveRequired()
    {
        var ledger = new AEDV02SupplyLedger(Guid.NewGuid(), 2);
        Assert.That(ledger.TryRecordZoneSpawn(1, 4, 1), Is.False);
        Assert.That(ledger.TryRecordZoneSpawn(1, 5, 1), Is.True);
        Assert.That(ledger.TryRecordZoneSpawn(1, 5, 1), Is.False);
        Assert.That(ledger.TrySetOptionalBudget(0), Is.False);
        Assert.That(ledger.OptionalBudget, Is.EqualTo(2));
        Assert.That(ledger.TrySetOptionalBudget(1), Is.True);
        Assert.That(ledger.OptionalForZone(2), Is.Zero);
        Assert.That(ledger.TryRecordZoneSpawn(2, 5, 0), Is.True);
        Assert.That(ledger.TryRecordZoneSpawn(3, 5, 0), Is.True);
        Assert.That(ledger.OptionalMaterialized, Is.EqualTo(1));
        Assert.That(ledger.HasSpawned(1), Is.True);
    }
    [Test]
    public void AEDV02_ReturnToInitialBudgetRestoresOnlyFutureSupplyAndNewMatchResets()
    {
        var ledger = new AEDV02SupplyLedger(Guid.NewGuid(), 2);
        Assert.That(ledger.TryRecordZoneSpawn(1, 5, 1), Is.True);
        Assert.That(ledger.TrySetOptionalBudget(1), Is.True);
        Assert.That(ledger.TrySetOptionalBudget(2), Is.True);
        Assert.That(ledger.TryRecordZoneSpawn(2, 5, 1), Is.True);
        Assert.That(ledger.OptionalMaterialized, Is.EqualTo(2));
        var next = new AEDV02SupplyLedger(Guid.NewGuid(), 0);
        Assert.That(next.TrySetOptionalBudget(1), Is.False);
        Assert.That(next.HasSpawned(1), Is.False);
        Assert.That(next.OptionalMaterialized, Is.Zero);
        Assert.That(next.TryRecordZoneSpawn(1, 5, 0), Is.True);
    }
}
