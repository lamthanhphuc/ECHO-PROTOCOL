using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AEDPreMatchCandidateV1Tests
{
    private static ScenarioConfigDefinition Baseline()
    {
        var config = Candidate(0, 9);
        config.ConfigSource = ScenarioConfigSource.Fixed;
        config.IsFixedFallback = true;
        config.ScenarioConfigId = "FIXED";
        return config;
    }

    private static ScenarioConfigDefinition Candidate(
        int budget, double speed) => new()
    {
        ScenarioConfigId = $"ADAPTIVE_{budget}_{speed}",
        ScenarioConfigVersion = "V1",
        SchemaVersion = "1.1",
        PolicyVersion = "AED_SCENARIO_POLICY_V1_1",
        ConfigSource = ScenarioConfigSource.Adaptive,
        MapId = "map",
        MonsterType = "STALKER",
        ObjectiveSpawnSetId = "objectives",
        SupportItemBudget = budget,
        DetectionFillRate = 1,
        DetectionDecayRate = 1,
        ChaseSpeed = speed,
        SearchDuration = 1,
        RouteModifier = "route",
        EscapeDoorTimerSeconds = 45,
        FallbackConfigId = "FIXED",
        FallbackConfigVersion = "V1",
        ContentWhitelistVersion = "WHITELIST",
        UnityCompatibilityVersion = "0.1.0",
        IsActive = true,
        IsProductionApproved = true
    };

    [Theory]
    [InlineData("AED-PRE-010-SURVIVAL-LOW-RELIEVE")]
    [InlineData("AED-PRE-020-NOISE-LOW-RELIEVE")]
    public void Relief_SelectsSmallestApprovedIncrease(
        string rule)
    {
        var baseline = Baseline();
        var candidates = new[]
        {
            Candidate(2, 9),
            Candidate(1, 9)
        };

        var result = AEDPreMatchCandidateV1.TrySelect(
            rule, baseline, candidates,
            out var selected, out var reason);

        Assert.True(result, reason);
        Assert.NotNull(selected);
        Assert.Equal(1, selected.SupportItemBudget);
        Assert.Equal(9, selected.ChaseSpeed);
    }

    [Fact]
    public void Pressure_OnlyIncreasesChaseSpeed()
    {
        var baseline = Baseline();

        var candidates = new[]
        {
            Candidate(0, 11),
            Candidate(0, 10),
            Candidate(1, 10)
        };

        var result = AEDPreMatchCandidateV1.TrySelect(
            "AED-PRE-030-BOTH-HIGH-INCREASE",
            baseline, candidates,
            out var selected, out var reason);

        Assert.True(result, reason);
        Assert.NotNull(selected);
        Assert.Equal(10, selected.ChaseSpeed);
        Assert.Equal(0, selected.SupportItemBudget);
    }

    [Fact]
    public void Hold_DoesNotCreateCandidate()
    {
        var result = AEDPreMatchCandidateV1.TrySelect(
            "AED-PRE-040-MIXED-HOLD",
            Baseline(),
            new[] { Candidate(1, 9) },
            out var selected,
            out var reason);

        Assert.False(result);
        Assert.Null(selected);
        Assert.Equal("HOLD_RULE", reason);
    }

    [Fact]
    public void UnauthorizedChange_IsRejected()
    {
        var invalid = Candidate(1, 9);
        invalid.SearchDuration = 99;

        var result = AEDPreMatchCandidateV1.TrySelect(
            "AED-PRE-010-SURVIVAL-LOW-RELIEVE",
            Baseline(),
            new[] { invalid },
            out var selected,
            out var reason);

        Assert.False(result);
        Assert.Null(selected);
        Assert.Equal("NO_APPROVED_NEXT_VALUE", reason);
    }

    [Fact]
    public void DuplicateNextValue_IsRejected()
    {
        var first = Candidate(1, 9);
        var second = Candidate(1, 9);
        second.ScenarioConfigVersion = "V2";

        var result = AEDPreMatchCandidateV1.TrySelect(
            "AED-PRE-010-SURVIVAL-LOW-RELIEVE",
            Baseline(),
            new[] { first, second },
            out var selected,
            out var reason);

        Assert.False(result);
        Assert.Null(selected);
        Assert.Equal("AMBIGUOUS_CANDIDATE", reason);
    }

    [Fact]
    public void UnapprovedCandidate_IsRejected()
    {
        var candidate = Candidate(1, 9);
        candidate.IsProductionApproved = false;

        var result = AEDPreMatchCandidateV1.TrySelect(
            "AED-PRE-010-SURVIVAL-LOW-RELIEVE",
            Baseline(),
            new[] { candidate },
            out var selected,
            out _);

        Assert.False(result);
        Assert.Null(selected);
    }
}
