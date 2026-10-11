using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AEDPreMatchPolicyV1Tests
{
    [Fact, Trait("Category", "AEDv2")]
    public void SelectRule_UsesDeterministicPriority()
    {
        var snapshot = new AdaptiveInputSnapshot
        {
            Validity = AdaptiveSnapshotValidity.Valid,
            TeamSize = 2,
            SurvivalAggregationStatus = "AVAILABLE",
            NoiseAggregationStatus = "AVAILABLE",
            SurvivalObservedActiveCount = 2,
            NoiseObservedActiveCount = 2,
            SurvivalMeanObservedScore = 0m,
            NoiseMeanObservedScore = 29.0625m
        };

        string? Evaluate() => AEDPreMatchPolicyV1.SelectRule(
            snapshot, 30m, 70m, 30m, 70m);

        Assert.Equal(
            "AED-PRE-010-SURVIVAL-LOW-RELIEVE",
            Evaluate());

        snapshot.SurvivalMeanObservedScore = 50m;

        Assert.Equal(
            "AED-PRE-020-NOISE-LOW-RELIEVE",
            Evaluate());

        snapshot.SurvivalMeanObservedScore = 70m;
        snapshot.NoiseMeanObservedScore = 70m;

        Assert.Equal(
            "AED-PRE-030-BOTH-HIGH-INCREASE",
            Evaluate());

        snapshot.SurvivalMeanObservedScore = 50m;
        snapshot.NoiseMeanObservedScore = 50m;

        Assert.Equal(
            "AED-PRE-040-MIXED-HOLD",
            Evaluate());

        snapshot.Validity = AdaptiveSnapshotValidity.Partial;
        Assert.Null(Evaluate());

        snapshot.Validity = AdaptiveSnapshotValidity.Valid;
        snapshot.SurvivalObservedActiveCount = 1;
        Assert.Null(Evaluate());

        snapshot.SurvivalObservedActiveCount = 2;
        Assert.Null(AEDPreMatchPolicyV1.SelectRule(
            snapshot, 70m, 30m, 30m, 70m));
    }
}
