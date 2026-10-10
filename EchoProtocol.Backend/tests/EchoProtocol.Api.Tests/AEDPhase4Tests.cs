using EchoProtocol.Api.Services;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class AEDPhase4Tests
{
    private static readonly Guid UserA =
        Guid.Parse("00000000-0000-0000-0000-000000000101");

    private static readonly Guid UserB =
        Guid.Parse("00000000-0000-0000-0000-000000000102");

    private const string Context = "NORMAL:CONFIG_V1";

    private static AEDVerifiedSkillObservationV1 Sample(
        int match, Guid user, AEDSkillDimensionV1 dimension,
        decimal? score = 0.85m, int resolved = 3,
        bool verified = true, string context = Context)
    {
        return new AEDVerifiedSkillObservationV1(
            new Guid(match, 0, 0, new byte[8]),
            user, dimension, context,
            $"fingerprint-{match}-{dimension}",
            "AED_METRIC_OPPORTUNITY_V1",
            score, resolved, resolved, 0,
            verified, true);
    }

    [Fact, Trait("Category", "AEDv2")]
    public void ColdStartHasNoSkillScore()
    {
        var result = AEDHistoricalSkillProjectorV1.Project(
            UserA, Context,
            Array.Empty<AEDVerifiedSkillObservationV1>());

        Assert.All(result, x =>
        {
            Assert.Null(x.Score);
            Assert.Equal(AEDSkillConfidenceV1.ColdStart,
                x.Confidence);
            Assert.False(x.ReadyForIncrease);
        });
    }

    [Fact, Trait("Category", "AEDv2")]
    public void SkillRequiresDistinctMatchesAndResolvedOpportunities()
    {
        var source = Enumerable.Range(1, 3)
            .Select(i => Sample(i, UserA,
                AEDSkillDimensionV1.Objective))
            .ToArray();

        var result = AEDHistoricalSkillProjectorV1.Project(
            UserA, Context, source)
            .Single(x =>
                x.Dimension == AEDSkillDimensionV1.Objective);

        Assert.Equal(3, result.DistinctMatches);
        Assert.Equal(9, result.ResolvedOpportunities);
        Assert.Equal(0.85m, result.Score);
        Assert.Equal(AEDSkillConfidenceV1.Sufficient,
            result.Confidence);
        Assert.True(result.ReadyForIncrease);
    }

    [Fact, Trait("Category", "AEDv2")]
    public void ManyEpisodesFromOneMatchAreNotIndependentMatches()
    {
        var source = Enumerable.Range(1, 10)
            .Select(i => Sample(1, UserA,
                AEDSkillDimensionV1.Evasion,
                resolved: 2) with
                {
                    SourceFingerprint = $"episode-{i}"
                });

        var result = AEDHistoricalSkillProjectorV1.Project(
            UserA, Context, source)
            .Single(x =>
                x.Dimension == AEDSkillDimensionV1.Evasion);

        Assert.Equal(1, result.DistinctMatches);
        Assert.Equal(AEDSkillConfidenceV1.Insufficient,
            result.Confidence);
        Assert.False(result.ReadyForIncrease);
    }

    [Fact, Trait("Category", "AEDv2")]
    public void UnverifiedSourceCannotCreateTrustedSkill()
    {
        var source = Enumerable.Range(1, 3)
            .Select(i => Sample(i, UserA,
                AEDSkillDimensionV1.Survival,
                verified: false));

        var result = AEDHistoricalSkillProjectorV1.Project(
            UserA, Context, source)
            .Single(x =>
                x.Dimension == AEDSkillDimensionV1.Survival);

        Assert.Equal(AEDSkillConfidenceV1.Invalid,
            result.Confidence);
        Assert.Null(result.Score);
        Assert.False(result.ReadyForIncrease);
    }

    [Fact, Trait("Category", "AEDv2")]
    public void RiskStyleNeverAuthorizesPressureIncrease()
    {
        var source = Enumerable.Range(1, 3)
            .Select(i => Sample(i, UserA,
                AEDSkillDimensionV1.RiskStyle));

        var result = AEDHistoricalSkillProjectorV1.Project(
            UserA, Context, source)
            .Single(x =>
                x.Dimension == AEDSkillDimensionV1.RiskStyle);

        Assert.False(result.ReadyForIncrease);
    }

    [Fact, Trait("Category", "AEDv2")]
    public void ColdStartTeammateBlocksTeamConfidence()
    {
        var dimensions = new[]
        {
            AEDSkillDimensionV1.Survival,
            AEDSkillDimensionV1.Evasion,
            AEDSkillDimensionV1.Objective
        };

        var source = dimensions.SelectMany(d =>
            Enumerable.Range(1, 3)
                .Select(i => Sample(i, UserA, d)));

        var profiles = AEDHistoricalSkillProjectorV1.Project(
            UserA, Context, source);

        var team = AEDTeamSkillProjectorV1.Project(
            Guid.NewGuid(), new[] { UserA, UserB },
            profiles);

        Assert.True(team.HasUncertainPlayer);
        Assert.False(team.ConfidenceComplete);
        Assert.Null(team.Mean);
        Assert.Null(team.Weakest);
    }

    [Fact, Trait("Category", "AEDv2")]
    public void MixedComparisonContextsBlockMemberSkill()
    {
        var dimensions = new[]
        {
            AEDSkillDimensionV1.Survival,
            AEDSkillDimensionV1.Evasion,
            AEDSkillDimensionV1.Objective
        };
        var source = dimensions.Select((dimension, index) =>
            new AEDHistoricalSkillDimensionV1(
                UserA, dimension, $"context-{index}",
                0.9m, 3, 8, 0,
                AEDSkillConfidenceV1.Sufficient, true));

        var team = AEDTeamSkillProjectorV1.Project(
            Guid.NewGuid(), new[] { UserA }, source);

        Assert.False(team.ConfidenceComplete);
        Assert.Null(team.Members.Single().Score);
    }

    [Fact, Trait("Category", "AEDv2")]
    public void IncreaseRequiresBackendVerifier()
    {
        var team = new AEDTeamSkillSnapshotV1(
            Guid.NewGuid(), 1,
            new[]
            {
                new AEDTeamMemberSkillV1(UserA, 0.9m, true)
            },
            0.9m, 0.9m, 0m, false, true);

        var decision = AEDPhase4SafetyGateV1.Evaluate(
            team, "Quiet",
            pressureSourceVerified: true,
            rosterObserved: true,
            safeBoundary: true,
            backendMetricVerifierSupported: false);

        Assert.Equal(AEDPhase4CandidateV1.Hold,
            decision.Candidate);
    }

    [Fact, Trait("Category", "AEDv2")]
    public void CriticalPressureCanProduceReliefCandidateForColdStart()
    {
        var team = new AEDTeamSkillSnapshotV1(
            Guid.NewGuid(), 1,
            new[]
            {
                new AEDTeamMemberSkillV1(UserA, null, false)
            },
            null, null, null, true, false);

        var decision = AEDPhase4SafetyGateV1.Evaluate(
            team, "Critical",
            pressureSourceVerified: true,
            rosterObserved: false,
            safeBoundary: true,
            backendMetricVerifierSupported: false);

        Assert.Equal(AEDPhase4CandidateV1.RelieveCandidate,
            decision.Candidate);
    }
}
