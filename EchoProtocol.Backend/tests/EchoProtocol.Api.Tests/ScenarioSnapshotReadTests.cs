using EchoProtocol.Api.Common;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Fact]
    public void V2FingerprintCanonicalizesJsonAndDecimalValues()
    {
        var first = CreateFingerprintSnapshot(" { \"b\": 2, \"a\": 1 } ", "{\"b\":2,\"a\":1}", 1.23456789m);
        var second = CreateFingerprintSnapshot("{\"a\":1,\"b\":2}", "{\"a\":1,\"b\":2}", 1.23456781m);
        var changed = CreateFingerprintSnapshot("{\"a\":1,\"b\":3}", "{\"a\":1,\"b\":2}", 1.23456789m);

        Assert.Equal(ScenarioFingerprint.Snapshot(first), ScenarioFingerprint.Snapshot(second));
        Assert.NotEqual(ScenarioFingerprint.Snapshot(first), ScenarioFingerprint.Snapshot(changed));
    }

    [Fact]
    public void V2FingerprintAcceptsPartialMissingProfiles()
    {
        var snapshot = CreateFingerprintSnapshot("[\"PROFILE_MISSING\"]", "{}", null);
        snapshot.Validity = AdaptiveSnapshotValidity.Partial;
        snapshot.Players =
        [
            new AdaptiveInputSnapshotPlayer { SnapshotId = snapshot.SnapshotId, UserId = Guid.NewGuid(), ProfileAvailable = false, DeferredDimensionsJson = "{}" },
            new AdaptiveInputSnapshotPlayer { SnapshotId = snapshot.SnapshotId, UserId = Guid.NewGuid(), ProfileAvailable = false, DeferredDimensionsJson = "{}" }
        ];

        var fingerprint = ScenarioFingerprint.Snapshot(snapshot);
        Assert.NotEmpty(fingerprint);
    }

    private static AdaptiveInputSnapshot CreateFingerprintSnapshot(
        string reasons, string deferred, decimal? objective)
    {
        var snapshot = new AdaptiveInputSnapshot
        {
            SnapshotId = Guid.Parse("00000000-0000-0000-0000-000000000010"),
            MatchId = Guid.Parse("00000000-0000-0000-0000-000000000011"), FingerprintVersion = "V2",
            DecisionPoint = "PRE_MATCH", RosterIdentity = "roster", TeamSize = 1,
            Validity = AdaptiveSnapshotValidity.Valid, ReasonCodesJson = reasons,
            ProfileFormulaSemanticId = "FORMULA", SurvivalComparisonKey = "SURVIVAL",
            NoiseComparisonKey = "NOISE", ObjectiveAggregationStatus = "AVAILABLE",
            ObjectiveComparisonKey = "OBJECTIVE", ObjectiveMeanObservedScore = objective,
            ToolUsageAggregationStatus = "UNAVAILABLE", ToolUsageComparisonKey = null
        };
        snapshot.Players.Add(new AdaptiveInputSnapshotPlayer
        {
            SnapshotId = snapshot.SnapshotId, UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"), ProfileAvailable = true,
            SurvivalStatus = "ACTIVE", NoiseStatus = "ACTIVE", SurvivalScore = 1.23456789m,
            NoiseScore = 2.34567891m, DeferredDimensionsJson = deferred
        });
        return snapshot;
    }

    private static async Task<(ScenarioSnapshotReadService Reader, Guid DecisionId)> PrepareReadAsync(Fixture f)
    {
        var decision = await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, Request());
        Assert.True(decision.IsSuccess, decision.Message);
        return (new ScenarioSnapshotReadService(f.Db, new FixedClock(Now)), decision.Data!.DecisionId);
    }

    [Fact]
    public async Task HostReadsPersistedSnapshotAndReplayHasStableIdentity()
    {
        await using var f = await Fixture.CreateAsync();
        var (reader, id) = await PrepareReadAsync(f);
        var first = await reader.GetAsync(f.HostId, f.MatchId, id);
        var replay = await reader.GetAsync(f.HostId, f.MatchId, id);
        Assert.True(first.IsSuccess, first.Message);
        Assert.Equal(first.Data!.SnapshotId, replay.Data!.SnapshotId);
        Assert.Equal(first.Data.SnapshotContentFingerprint, replay.Data.SnapshotContentFingerprint);
        Assert.True(first.Data.RosterCurrent && first.Data.ProfileRevisionsCurrent
            && first.Data.SnapshotFingerprintValid && first.Data.ProfileSemanticsSupported);
    }

    [Fact]
    public async Task NonHostCannotReadAndOtherMatchCannotLeak()
    {
        await using var f = await Fixture.CreateAsync();
        var (reader, id) = await PrepareReadAsync(f);
        Assert.Equal(ErrorCodes.MatchAuthorityForbidden,
            (await reader.GetAsync(f.PlayerId, f.MatchId, id)).ErrorCode);
        Assert.Equal(ErrorCodes.MatchNotFound,
            (await reader.GetAsync(f.HostId, Guid.NewGuid(), id)).ErrorCode);
        Assert.Equal(ErrorCodes.ScenarioDecisionNotFound,
            (await reader.GetAsync(f.HostId, f.MatchId, Guid.NewGuid())).ErrorCode);
    }

    [Fact]
    public async Task RosterOrProfileRevisionChangesRejectSnapshot()
    {
        await using var f = await Fixture.CreateAsync();
        var (reader, id) = await PrepareReadAsync(f);
        f.Db.PlayerAIProfiles.Single(x => x.UserId == f.HostId).ProfileRevision++;
        await f.Db.SaveChangesAsync();
        Assert.Equal(ErrorCodes.ScenarioDecisionStaleRoster,
            (await reader.GetAsync(f.HostId, f.MatchId, id)).ErrorCode);
        f.Db.PlayerAIProfiles.Single(x => x.UserId == f.HostId).ProfileRevision--;
        await f.Db.SaveChangesAsync();
        await f.AddRosterPlayerAsync();
        Assert.Equal(ErrorCodes.ScenarioDecisionStaleRoster,
            (await reader.GetAsync(f.HostId, f.MatchId, id)).ErrorCode);
    }

    [Fact]
    public async Task MissingAndColdStartProfilesKeepTheirStatusesWithoutScoreSubstitution()
    {
        await using var f = await Fixture.CreateAsync(includePlayerProfile: false);
        var (reader, id) = await PrepareReadAsync(f);
        var missing = (await reader.GetAsync(f.HostId, f.MatchId, id)).Data!.Players.Single(x => x.UserId == f.PlayerId);
        Assert.False(missing.ProfileAvailable);
        Assert.Null(missing.SurvivalScore);
        Assert.Equal("UNAVAILABLE", missing.SurvivalStatus);

        await using var cold = await Fixture.CreateAsync();
        var (coldReader, coldId) = await PrepareReadAsync(cold);
        var player = (await coldReader.GetAsync(cold.HostId, cold.MatchId, coldId)).Data!.Players.Single(x => x.UserId == cold.PlayerId);
        Assert.Equal("COLD_START", player.SurvivalStatus);
        Assert.Equal(0, player.SurvivalSampleCount);
    }

    [Fact]
    public async Task UnsupportedFormulaOrTamperedFingerprintIsRejected()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.PlayerAIProfiles.Single(x => x.UserId == f.HostId).ProfileFormulaVersion = "UNKNOWN";
        await f.Db.SaveChangesAsync();
        var (reader, id) = await PrepareReadAsync(f);
        Assert.Equal(ErrorCodes.ScenarioDecisionStaleRoster,
            (await reader.GetAsync(f.HostId, f.MatchId, id)).ErrorCode);

        await using var other = await Fixture.CreateAsync();
        var (otherReader, otherId) = await PrepareReadAsync(other);
        other.Db.AdaptiveInputSnapshots.Single().SnapshotContentFingerprint = new string('0', 64);
        await other.Db.SaveChangesAsync();
        Assert.Equal(ErrorCodes.ScenarioDecisionStaleRoster,
            (await otherReader.GetAsync(other.HostId, other.MatchId, otherId)).ErrorCode);
    }

    [Fact]
    public async Task ReadDoesNotMutateDatabase()
    {
        await using var f = await Fixture.CreateAsync();
        var (reader, id) = await PrepareReadAsync(f);
        var before = await f.Db.ScenarioDecisions.AsNoTracking()
            .Select(x => new { x.DecisionId, x.SnapshotId, x.ResolutionResult }).SingleAsync();
        await reader.GetAsync(f.HostId, f.MatchId, id);
        var after = await f.Db.ScenarioDecisions.AsNoTracking()
            .Select(x => new { x.DecisionId, x.SnapshotId, x.ResolutionResult }).SingleAsync();
        Assert.Equal(before, after);
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }
}
