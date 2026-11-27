using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.MatchResults;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class MatchResultServiceTests
{
    [Fact]
    public async Task DebugResultKeepsNormalRewardPolicyAndReplayCannotRemoveTheFlag()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var request = harness.ValidRequest();
        request.UsedDebugCheats = true;
        var result = await harness.Service.SubmitAsync(harness.HostId, harness.MatchId, request);
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(RemoteFixedBalanceContract.Standard, (await harness.Db.MatchAuthorityBindings.SingleAsync()).MatchDataPolicy);
        request.UsedDebugCheats = false;
        Assert.Equal(ErrorCodes.MatchResultConflict,
            (await harness.Service.SubmitAsync(harness.HostId, harness.MatchId, request)).ErrorCode);
    }

    [Fact]
    public async Task V2RequiresCompleteEvidenceAndPersistsItWithoutChangingLegacyReplay()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var match = await harness.Db.MatchAuthorityBindings.SingleAsync();
        match.RewardPolicyVersion = "REWARD_V2";
        await harness.Db.SaveChangesAsync();
        var request = harness.ValidRequest();
        var missing = await harness.Service.SubmitAsync(harness.HostId, harness.MatchId, request);
        Assert.Equal(ErrorCodes.MatchResultInvalidPayload, missing.ErrorCode);
        request.EvidenceVersion = 2;
        foreach (var player in request.Players)
        {
            player.ObjectiveActions = 0;
            player.RescueActions = 0;
            player.EvidenceComplete = true;
        }
        request.Players[0].EvidenceComplete = false;
        var incomplete = await harness.Service.SubmitAsync(harness.HostId, harness.MatchId, request);
        Assert.Equal(ErrorCodes.MatchResultInvalidPayload, incomplete.ErrorCode);
        request.Players[0].EvidenceComplete = true;
        var accepted = await harness.Service.SubmitAsync(harness.HostId, harness.MatchId, request);
        Assert.True(accepted.IsSuccess);
        var stored = await harness.Db.MatchResults.Include(item => item.Players).SingleAsync();
        Assert.Equal(2, stored.EvidenceVersion);
        Assert.All(stored.Players, player => { Assert.Equal(0, player.ObjectiveActions); Assert.Equal(0, player.RescueActions); Assert.True(player.EvidenceComplete); });
        request.Players[0].ObjectiveActions = 1;
        var changed = await harness.Service.SubmitAsync(harness.HostId, harness.MatchId, request);
        Assert.Equal(ErrorCodes.MatchResultConflict, changed.ErrorCode);
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(10001, 2)]
    [InlineData(0, 3)]
    public async Task V2RejectsInvalidActionCountsAndEvidenceVersion(int actions, int version)
    {
        await using var harness = await UnitHarness.CreateAsync();
        var request = harness.ValidRequest();
        request.EvidenceVersion = version;
        foreach (var player in request.Players)
        {
            player.ObjectiveActions = actions;
            player.RescueActions = 0;
            player.EvidenceComplete = true;
        }
        var result = await harness.Service.SubmitAsync(harness.HostId, harness.MatchId, request);
        Assert.Equal(ErrorCodes.MatchResultInvalidPayload, result.ErrorCode);
        Assert.Empty(harness.Db.MatchResults);
    }
    [Fact]
    public async Task LobbyHistoryIsExcludedFromStartedResultRoster()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var visitor = Guid.NewGuid();
        harness.Db.Users.Add(new User { Id = visitor, Username = "visitor", Email = "visitor@example.test", PasswordHash = "unused" });
        harness.Db.MatchPlayerBindings.Add(new MatchPlayerBinding
        {
            Id = Guid.NewGuid(), MatchId = harness.MatchId, UserId = visitor,
            FusionActorNumber = 3, JoinProofId = Guid.NewGuid(), Participated = false,
            BoundAtUtc = Now.AddMinutes(-5).UtcDateTime, LastSeenAtUtc = Now.AddMinutes(-4).UtcDateTime,
            DisconnectedAtUtc = Now.AddMinutes(-4).UtcDateTime
        });
        await harness.Db.SaveChangesAsync();
        var result = await harness.Service.SubmitAsync(harness.HostId, harness.MatchId, harness.ValidRequest());
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.PlayerCount);
    }
    [Fact]
    public async Task AdminRecoveryRequiresExpiredLeaseAndReplaysWithoutDuplicateResult()
    {
        await using var harness = await UnitHarness.CreateAsync(leaseExpired: true);
        var user = await harness.Db.Users.SingleAsync(user => user.Id == harness.HostId);
        var request = harness.ValidRequest();
        var denied = await harness.Service.RecoverAsync(user.Id, harness.MatchId, request,
            Now.AddSeconds(-10).UtcDateTime, "Reviewed exported match evidence", CancellationToken.None);
        Assert.False(denied.IsSuccess);
        user.Role = UserRole.ADMIN;
        await harness.Db.SaveChangesAsync();
        var accepted = await harness.Service.RecoverAsync(user.Id, harness.MatchId, request,
            Now.AddSeconds(-10).UtcDateTime, "Reviewed exported match evidence", CancellationToken.None);
        Assert.True(accepted.IsSuccess);
        var replay = await harness.Service.RecoverAsync(user.Id, harness.MatchId, request,
            Now.AddSeconds(-10).UtcDateTime, "Reviewed exported match evidence", CancellationToken.None);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Data!.IsReplay);
        Assert.Contains(user.Id.ToString("D"), (await harness.Db.MatchResults.SingleAsync()).RecoveryAudit);
    }

    [Fact]
    public async Task RecoveryCannotOverrideActiveHostLease()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var user = await harness.Db.Users.SingleAsync(user => user.Id == harness.HostId);
        user.Role = UserRole.ADMIN;
        await harness.Db.SaveChangesAsync();
        var result = await harness.Service.RecoverAsync(user.Id, harness.MatchId, harness.ValidRequest(),
            Now.AddSeconds(-10).UtcDateTime, "Reviewed exported match evidence", CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Empty(harness.Db.MatchResults);
    }
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Category", "M4Unit")]
    public async Task ValidHostSubmitsResult()
    {
        await using var harness = await UnitHarness.CreateAsync();

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsReplay);
        Assert.Equal(MatchRewardStatus.Pending, result.Data.RewardStatus);
    }

    [Fact]
    public async Task LostSuccessResponseCanReplayAfterLeaseExpires()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var request = harness.ValidRequest();
        Assert.True((await harness.Service.SubmitAsync(harness.HostId, harness.MatchId, request)).IsSuccess);
        var match = await harness.Db.MatchAuthorityBindings.SingleAsync();
        match.LeaseExpiresAtUtc = Now.AddMinutes(-1).UtcDateTime;
        await harness.Db.SaveChangesAsync();
        var replay = await harness.Service.SubmitAsync(harness.HostId, harness.MatchId, request);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Data!.IsReplay);
        Assert.Single(await harness.Db.MatchResults.ToListAsync());
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task NonHostIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync();

        var result = await harness.Service.SubmitAsync(
            Guid.NewGuid(), harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchAuthorityForbidden, result.ErrorCode);
        Assert.Empty(harness.Db.MatchResults);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task MatchNotFoundIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync();

        var result = await harness.Service.SubmitAsync(
            harness.HostId, Guid.NewGuid(), harness.ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchNotFound, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task InvalidMatchStateIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync(MatchAuthorityStatus.Lobby);

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultInvalidState, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task InvalidPlayerRosterIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var request = harness.ValidRequest();
        request.Players.RemoveAt(1);

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultInvalidRoster, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task DuplicatePlayerIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var request = harness.ValidRequest();
        request.Players[1].UserId = request.Players[0].UserId;

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultDuplicatePlayer, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task FirstValidSubmissionPersistsOnce()
    {
        await using var harness = await UnitHarness.CreateAsync();

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await harness.Db.MatchResults.CountAsync());
        Assert.Equal(2, await harness.Db.MatchResultPlayers.CountAsync());
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task IdenticalRetryReturnsStoredResult()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var request = harness.ValidRequest();
        var first = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, request, CancellationToken.None);

        var retry = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.True(retry.Data!.IsReplay);
        Assert.Equal(first.Data!.SubmittedAtUtc, retry.Data.SubmittedAtUtc);
        Assert.Equal(1, await harness.Db.MatchResults.CountAsync());
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task ConflictingRetryIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync();
        await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);
        var changed = harness.ValidRequest();
        changed.ObjectiveCompletion = 0.5m;

        var conflict = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, changed, CancellationToken.None);

        Assert.False(conflict.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultConflict, conflict.ErrorCode);
        Assert.Equal(1, await harness.Db.MatchResults.CountAsync());
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task ResultPersistenceUsesServerTimestampsAndPendingReward()
    {
        await using var harness = await UnitHarness.CreateAsync();

        await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        harness.Db.ChangeTracker.Clear();
        var stored = await harness.Db.MatchResults.Include(item => item.Players).SingleAsync();
        Assert.Equal(harness.HostId, stored.SubmittedByUserId);
        Assert.Equal(Now.AddMinutes(-2).UtcDateTime, stored.StartedAtUtc);
        Assert.Equal(Now.UtcDateTime, stored.EndedAtUtc);
        Assert.Equal(120, stored.DurationSeconds);
        Assert.Equal(MatchRewardStatus.Pending, stored.RewardStatus);
        Assert.Equal(2, stored.Players.Count);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task SuccessfulSubmissionTransitionsMatchToEnded()
    {
        await using var harness = await UnitHarness.CreateAsync();

        await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        harness.Db.ChangeTracker.Clear();
        var match = await harness.Db.MatchAuthorityBindings.SingleAsync();
        Assert.Equal(MatchAuthorityStatus.Ended, match.Status);
        Assert.Equal(Now.UtcDateTime, match.EndedAtUtc);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task FailedSubmissionDoesNotPartiallyMutateMatch()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var invalid = harness.ValidRequest();
        invalid.Players.RemoveAt(1);

        await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, invalid, CancellationToken.None);

        harness.Db.ChangeTracker.Clear();
        Assert.Empty(harness.Db.MatchResults);
        var match = await harness.Db.MatchAuthorityBindings.SingleAsync();
        Assert.Equal(MatchAuthorityStatus.InMatch, match.Status);
        Assert.Null(match.EndedAtUtc);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task DisconnectedBoundPlayerIsIncludedInRoster()
    {
        await using var harness = await UnitHarness.CreateAsync(secondPlayerDisconnected: true);

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId,
            harness.ValidRequest(secondPlayerDisconnected: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Data!.Players,
            player => player.UserId == harness.PlayerId && player.Disconnected);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task NegativeGameplayStatIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync();
        var invalid = harness.ValidRequest();
        invalid.Players[0].ReviveCount = -1;

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, invalid, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultInvalidPayload, result.ErrorCode);
        Assert.Empty(harness.Db.MatchResults);
    }


    [Fact, Trait("Category", "M4Unit")]
    public async Task FortyFiveMinuteMatchIsAccepted()
    {
        await using var harness = await UnitHarness.CreateAsync(matchAge: TimeSpan.FromMinutes(45));

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2700, result.Data!.DurationSeconds);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task MatchBeyondSubmissionGraceIsRejected()
    {
        await using var harness = await UnitHarness.CreateAsync(
            matchAge: TimeSpan.FromMinutes(121).Add(TimeSpan.FromSeconds(1)));

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultInvalidDuration, result.ErrorCode);
    }

    [Theory]
    [InlineData(2761)]
    [InlineData(5400)]
    [InlineData(7200)]
    [InlineData(7260)]
    public async Task TwoHourGameplayAndSubmissionGraceAreAccepted(int durationSeconds)
    {
        await using var harness = await UnitHarness.CreateAsync(
            matchAge: TimeSpan.FromSeconds(durationSeconds));

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(durationSeconds, result.Data!.DurationSeconds);
        Assert.Single(harness.Db.MatchResults);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task ExpiredHostLeaseCannotSubmitResult()
    {
        await using var harness = await UnitHarness.CreateAsync(leaseExpired: true);

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchLeaseExpired, result.ErrorCode);
    }

    [Fact, Trait("Category", "M4Unit")]
    public async Task EndedMatchWithoutResultCannotSubmitLateResult()
    {
        await using var harness = await UnitHarness.CreateAsync(MatchAuthorityStatus.Ended);

        var result = await harness.Service.SubmitAsync(
            harness.HostId, harness.MatchId, harness.ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.MatchResultInvalidState, result.ErrorCode);
        Assert.Empty(harness.Db.MatchResults);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SalvageEvidenceIsSavedAndReplayCannotChangePaidLevels(bool debug)
    {
        await using var harness=await UnitHarness.CreateAsync();
        var match=await harness.Db.MatchAuthorityBindings.SingleAsync();match.MapId=MatchContentCatalog.SalvageMap;match.Difficulty="NORMAL";
        await harness.Db.SaveChangesAsync();var request=harness.ValidRequest();
        request.EvidenceVersion=2;request.SalvageCompletedLevels=1;request.SalvageCompletedDays=3;request.UsedDebugCheats=debug;
        foreach(var p in request.Players){p.ObjectiveActions=0;p.RescueActions=0;p.EvidenceComplete=true;}
        var submitted=await harness.Service.SubmitAsync(harness.HostId,harness.MatchId,request);Assert.True(submitted.IsSuccess,submitted.Message);
        var stored=await harness.Db.MatchResults.SingleAsync();Assert.Equal(1,stored.SalvageCompletedLevels);Assert.Equal(debug,stored.SalvageUsedDebugCheats);
        var replay=await harness.Service.SubmitAsync(harness.HostId,harness.MatchId,request);Assert.True(replay.IsSuccess);Assert.True(replay.Data!.IsReplay);
        request.SalvageCompletedLevels=0;
        var changed=await harness.Service.SubmitAsync(harness.HostId,harness.MatchId,request);Assert.Equal(ErrorCodes.MatchResultConflict,changed.ErrorCode);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LongSessionDurationExceptionIsSalvageOnly(bool salvage)
    {
        await using var harness=await UnitHarness.CreateAsync(matchAge:TimeSpan.FromHours(4));
        var request=harness.ValidRequest();
        if(salvage)
        {
            var match=await harness.Db.MatchAuthorityBindings.SingleAsync();match.MapId=MatchContentCatalog.SalvageMap;match.Difficulty="NORMAL";
            await harness.Db.SaveChangesAsync();request.EvidenceVersion=2;request.SalvageCompletedLevels=1;request.SalvageCompletedDays=3;
            foreach(var player in request.Players){player.EvidenceComplete=true;player.ObjectiveActions=0;player.RescueActions=0;}
        }
        var result=await harness.Service.SubmitAsync(harness.HostId,harness.MatchId,request);
        if(salvage)Assert.True(result.IsSuccess,result.Message);
        else Assert.Equal(ErrorCodes.MatchResultInvalidDuration,result.ErrorCode);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SalvageRecoveryUsesServerSnapshotAndNeverOverridesLiveLease(bool expired)
    {
        await using var harness=await UnitHarness.CreateAsync(status:expired?MatchAuthorityStatus.Ended:MatchAuthorityStatus.InMatch,leaseExpired:expired,matchAge:TimeSpan.FromMinutes(10));
        var match=await harness.Db.MatchAuthorityBindings.SingleAsync();match.MapId=MatchContentCatalog.SalvageMap;match.Difficulty="NORMAL";
        var request=harness.ValidRequest();request.EvidenceVersion=2;request.SalvageCompletedLevels=1;request.SalvageCompletedDays=3;
        foreach(var player in request.Players){player.EvidenceComplete=true;player.ObjectiveActions=0;player.RescueActions=0;}
        harness.Db.SalvageCheckpoints.Add(new EchoProtocol.Api.Entities.SalvageCheckpoint {MatchId=harness.MatchId,OwnerId=harness.HostId,Revision=1,
            StateJson="{}",ResultJson=System.Text.Json.JsonSerializer.Serialize(request,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),SavedAtUtc=match.StartedAtUtc!.Value.AddMinutes(5)});
        await harness.Db.SaveChangesAsync();
        var service=(MatchResultService)harness.Service;
        var denied=await service.RecoverSalvageCheckpointAsync(harness.PlayerId,harness.MatchId,default);Assert.False(denied.IsSuccess);
        var result=await service.RecoverSalvageCheckpointAsync(harness.HostId,harness.MatchId,default);
        if(!expired){Assert.False(result.IsSuccess);return;}
        Assert.True(result.IsSuccess,result.Message);
        var stored=await harness.Db.MatchResults.SingleAsync();Assert.Equal(300,stored.DurationSeconds);
        var replay=await service.RecoverSalvageCheckpointAsync(harness.HostId,harness.MatchId,default);Assert.True(replay.IsSuccess);Assert.True(replay.Data!.IsReplay);
    }
    [Fact]
    public async Task SalvageCheckpointRejectsWrongHostAndStaleWrites()
    {
        await using var h=await UnitHarness.CreateAsync();var match=await h.Db.MatchAuthorityBindings.SingleAsync();match.MapId=MatchContentCatalog.SalvageMap;match.Difficulty="NORMAL";
        await h.Db.SaveChangesAsync();
        var controller=new EchoProtocol.Api.Controllers.SalvageCheckpointsController(h.Db,new FixedTimeProvider(Now),null!,null!);
        void Identity(Guid id)=>controller.ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext {HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext {User=new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier,id.ToString())},"test"))}};
        var result=h.ValidRequest();result.EvidenceVersion=2;result.SalvageCompletedDays=0;result.SalvageCompletedLevels=0;
        var json=System.Text.Json.JsonSerializer.Serialize(new {owner=h.HostId.ToString(),version=1,cycle=1,day=1,funds=0,cargo=new object[128]});
        var upload=new EchoProtocol.Api.Controllers.SalvageCheckpointsController.SaveRequest {MatchId=h.MatchId,Revision=1,StateJson=json,Result=result};
        Identity(h.PlayerId);Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(await controller.Save(upload,default));
        Assert.Empty(h.Db.SalvageCheckpoints);
        Identity(h.HostId);Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await controller.Save(upload,default));
        upload.StateJson=json.Replace("funds\":0","funds\":999");
        Assert.IsType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>(await controller.Save(upload,default));
        Assert.Equal(json,(await h.Db.SalvageCheckpoints.SingleAsync()).StateJson);
        match.LeaseExpiresAtUtc=Now.AddSeconds(-1).UtcDateTime;await h.Db.SaveChangesAsync();upload.Revision=2;
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(await controller.Save(upload,default));
    }
    [Fact] public async Task SalvageCannotOmitEvidenceOrInjectItIntoAnOldMode()
    {
        await using var harness=await UnitHarness.CreateAsync();var request=harness.ValidRequest();
        request.SalvageCompletedLevels=0;request.SalvageCompletedDays=0;
        var injected=await harness.Service.SubmitAsync(harness.HostId,harness.MatchId,request);Assert.Equal(ErrorCodes.MatchResultInvalidPayload,injected.ErrorCode);
        var match=await harness.Db.MatchAuthorityBindings.SingleAsync();match.MapId=MatchContentCatalog.SalvageMap;match.Difficulty="NORMAL";await harness.Db.SaveChangesAsync();
        request.SalvageCompletedLevels=null;request.SalvageCompletedDays=null;
        var omitted=await harness.Service.SubmitAsync(harness.HostId,harness.MatchId,request);Assert.Equal(ErrorCodes.MatchResultInvalidPayload,omitted.ErrorCode);
    }

    private sealed class UnitHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private UnitHarness(
            SqliteConnection connection,
            AppDbContext db,
            Guid hostId,
            Guid playerId,
            Guid matchId)
        {
            _connection = connection;
            Db = db;
            HostId = hostId;
            PlayerId = playerId;
            MatchId = matchId;
            Service = new MatchResultService(db, new FixedTimeProvider(Now));
        }

        public AppDbContext Db { get; }
        public MatchResultService Service { get; }
        public Guid HostId { get; }
        public Guid PlayerId { get; }
        public Guid MatchId { get; }

        public static async Task<UnitHarness> CreateAsync(
            MatchAuthorityStatus status = MatchAuthorityStatus.InMatch,
            bool secondPlayerDisconnected = false,
            bool leaseExpired = false,
            TimeSpan? matchAge = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var hostId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var matchId = Guid.NewGuid();
            db.Users.AddRange(CreateUser(hostId, "host"), CreateUser(playerId, "player"));
            db.MatchAuthorityBindings.Add(new MatchAuthorityBinding
            {
                MatchId = matchId,
                FusionSessionName = "m4-unit",
                HostUserId = hostId,
                MaxPlayers = 4,
                Status = status,
                LeaseExpiresAtUtc = leaseExpired
                    ? Now.AddSeconds(-1).UtcDateTime
                    : Now.AddMinutes(1).UtcDateTime,
                CreatedAtUtc = Now.Subtract(matchAge ?? TimeSpan.FromMinutes(2)).AddMinutes(-1).UtcDateTime,
                UpdatedAtUtc = Now.Subtract(matchAge ?? TimeSpan.FromMinutes(2)).UtcDateTime,
                StartedAtUtc = Now.Subtract(matchAge ?? TimeSpan.FromMinutes(2)).UtcDateTime,
                EndedAtUtc = status == MatchAuthorityStatus.Ended
                    ? Now.AddSeconds(-10).UtcDateTime
                    : null,
                Players =
                [
                    CreateBinding(matchId, hostId, 1, disconnected: false),
                    CreateBinding(matchId, playerId, 2, secondPlayerDisconnected)
                ]
            });
            await db.SaveChangesAsync();
            return new UnitHarness(connection, db, hostId, playerId, matchId);
        }

        public SubmitMatchResultRequest ValidRequest(bool secondPlayerDisconnected = false) => new()
        {
            Outcome = MatchOutcome.WIN,
            ObjectiveCompletion = 1m,
            Players =
            [
                new SubmitMatchResultPlayerRequest
                {
                    UserId = HostId,
                    Survived = true,
                    Disconnected = false,
                    DetectionCount = 2,
                    DownedCount = 0,
                    ReviveCount = 1,
                    ObjectiveContribution = 1
                },
                new SubmitMatchResultPlayerRequest
                {
                    UserId = PlayerId,
                    Survived = !secondPlayerDisconnected,
                    Disconnected = secondPlayerDisconnected,
                    DetectionCount = 3,
                    DownedCount = 1,
                    ReviveCount = 0,
                    ObjectiveContribution = 1
                }
            ]
        };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static User CreateUser(Guid id, string name) => new()
        {
            Id = id,
            Email = $"{name}-{id:N}@echo.invalid",
            Username = $"{name}-{id:N}",
            PasswordHash = "not-used",
            Role = UserRole.PLAYER,
            Status = UserStatus.ACTIVE,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        };

        private static MatchPlayerBinding CreateBinding(
            Guid matchId,
            Guid userId,
            int actor,
            bool disconnected) => new()
        {
            Participated = true,
            Id = Guid.NewGuid(),
            MatchId = matchId,
            UserId = userId,
            FusionActorNumber = actor,
            JoinProofId = Guid.NewGuid(),
            BoundAtUtc = Now.AddMinutes(-3).UtcDateTime,
            LastSeenAtUtc = Now.AddMinutes(-1).UtcDateTime,
            DisconnectedAtUtc = disconnected ? Now.AddSeconds(-30).UtcDateTime : null
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
