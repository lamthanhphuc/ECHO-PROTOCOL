using EchoProtocol.Api.Services;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AEDPhase8ApprovalGateV1Tests
{
    private static AEDPhase8ApprovalEvidenceV1 Evidence(
        bool pressure = true,
        bool supply = true,
        bool minions = true) => new(
        CanonicalVerified: true,
        HistoricalSkillVerified: true,
        PressureVerified: pressure,
        SupplyLedgerVerified: supply,
        MinionPopulationVerified: minions,
        RosterCurrent: true,
        SnapshotCurrent: true,
        RevisionCurrent: true,
        SafeBoundary: true,
        AdaptiveNormal: true,
        HostAuthorized: true);

    [Fact, Trait("Category", "AEDv2")]
    public void FullyVerifiedResearch_NeverApprovesGameplay()
    {
        foreach (var kind in new[] { "TEAM_TOOL", "MINION", "STALKER" })
        {
            var result = AEDPhase8ApprovalGateV1.Evaluate(Evidence(), kind);
            Assert.True(result.ApprovedForResearch);
            Assert.False(result.GameplayApproved);
        }
    }

    [Fact, Trait("Category", "AEDv2")]
    public void MissingSources_RejectRelevantDirector()
    {
        Assert.False(AEDPhase8ApprovalGateV1.Evaluate(
            Evidence(pressure: false), "STALKER").ApprovedForResearch);
        Assert.Equal("P8_SUPPLY_UNVERIFIED", AEDPhase8ApprovalGateV1.Evaluate(
            Evidence(supply: false), "TEAM_TOOL").ReasonCode);
        Assert.Equal("P8_MINION_STATE_UNVERIFIED", AEDPhase8ApprovalGateV1.Evaluate(
            Evidence(minions: false), "MINION").ReasonCode);
    }

    [Fact, Trait("Category", "AEDv2")]
    public void UnsafeBoundary_RejectsApproval()
    {
        var result = AEDPhase8ApprovalGateV1.Evaluate(
            Evidence() with { SafeBoundary = false }, "STALKER");
        Assert.False(result.ApprovedForResearch);
        Assert.False(result.GameplayApproved);
        Assert.Equal("P8_UNSAFE_BOUNDARY", result.ReasonCode);
    }
}
