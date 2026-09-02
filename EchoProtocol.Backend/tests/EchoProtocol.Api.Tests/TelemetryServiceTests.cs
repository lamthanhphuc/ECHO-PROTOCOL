using System.Globalization;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.MatchAuthority;
using EchoProtocol.Api.DTOs.Telemetry;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class TelemetryServiceTests
{
    [Fact]
    public async Task IngestBatchAsync_CanonicalV11MatchStart_StoresValueJsonAndReturnsAcceptedAck()
    {
        var repository = new FakeTelemetryEventRepository();
        var request = CreateMatchStarted();

        var result = await CreateService(repository)
            .IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TelemetryAckStatuses.Accepted, Assert.Single(result.Data!.Items).Status);
        var stored = Assert.Single(repository.InsertedEvents);
        Assert.Equal("1.1", stored.SchemaVersion);
        Assert.Equal(1, stored.EventSequence);
        Assert.Equal("RESEARCH_FACILITY", stored.ValueJson["data"]!["mapId"]!.AsString);
        Assert.NotEmpty(stored.SemanticFingerprint);
    }

    [Theory]
    [InlineData("BOGUS_EVENT", "TELEMETRY_EVENT_TYPE_UNSUPPORTED")]
    [InlineData("PUZZLE_FAILED", "TELEMETRY_RESERVED_EVENT_NOT_EMITTED")]
    [InlineData("MONSTER_TARGET_ACQUIRED", "TELEMETRY_RESERVED_EVENT_NOT_EMITTED")]
    public async Task IngestBatchAsync_UnsupportedOrReservedEvent_ReturnsPermanentReject(
        string eventType,
        string expectedReason)
    {
        var repository = new FakeTelemetryEventRepository();
        var request = CreateMatchStarted();
        request.Events[0].EventType = eventType;

        var result = await CreateService(repository)
            .IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        AssertReject(result, expectedReason);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_UnknownTopLevelEventField_ReturnsPermanentReject()
    {
        var repository = new FakeTelemetryEventRepository();
        var request = CreateMatchStarted();
        request.Events[0].ExtensionData = new Dictionary<string, JsonElement>
        {
            ["eventId"] = JsonSerializer.SerializeToElement("duplicate")
        };

        var result = await CreateService(repository)
            .IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        AssertReject(result, "TELEMETRY_UNKNOWN_FIELD");
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_UnknownBatchRootField_ReturnsEnvelopeFailure()
    {
        var repository = new FakeTelemetryEventRepository();
        var request = CreateMatchStarted();
        request.ExtensionData = new Dictionary<string, JsonElement>
        {
            ["batchId"] = JsonSerializer.SerializeToElement("not-canonical")
        };

        var result = await CreateService(repository)
            .IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
        Assert.Empty(repository.InsertedEvents);
    }

    [Theory]
    [InlineData("contextExtra", "TELEMETRY_UNKNOWN_FIELD")]
    [InlineData("dataExtra", "TELEMETRY_UNKNOWN_FIELD")]
    [InlineData("positionExtra", "TELEMETRY_UNKNOWN_FIELD")]
    [InlineData("badReason", "TELEMETRY_REASON_CODE_INVALID")]
    [InlineData("badEnum", "TELEMETRY_INVALID_ENUM_TOKEN")]
    [InlineData("missingRequired", "TELEMETRY_COMMON_CONTEXT_INVALID")]
    public async Task IngestBatchAsync_StrictV11RejectsMalformedFields(string caseName, string expectedReason)
    {
        var repository = new FakeTelemetryEventRepository();
        var userId = Guid.NewGuid();
        var item = CreateNoiseEvent(userId, Guid.NewGuid(), 2);
        item = caseName switch
        {
            "contextExtra" => WithValueJson(item, NoiseValue(2, contextExtra: true)),
            "dataExtra" => WithValueJson(item, NoiseValue(2, dataExtra: true)),
            "positionExtra" => WithValueJson(item, NoiseValue(2, positionExtra: true)),
            "badReason" => WithReason(item, "not_upper"),
            "badEnum" => WithValueJson(item, NoiseValue(2, noiseType: "WHISPER")),
            "missingRequired" => WithValueJson(item, JsonSerializer.SerializeToElement(new
            {
                context = new
                {
                    eventSequence = 2,
                    authorityTick = 42,
                    scenarioConfigVersion = "TEST-SCENARIO-1",
                    configSource = "FIXED",
                    phase = "CORE_COLLECTION",
                    position = new { x = 1.0, y = 2.0, z = 3.0 }
                },
                data = new { noiseEventId = "noise-42", noiseType = "SPRINT", loudness = 0.7 }
            })),
            _ => item
        };

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [item] }, userId, CancellationToken.None);

        AssertReject(result, expectedReason);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_PlayerRevivedWithValidReviverGuid_Accepts()
    {
        var repository = new FakeTelemetryEventRepository();
        var revivedUserId = Guid.NewGuid();
        var item = CreatePlayerRevived(revivedUserId, Guid.NewGuid(), Guid.NewGuid().ToString("D"));

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [item] }, revivedUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TelemetryAckStatuses.Accepted, Assert.Single(result.Data!.Items).Status);
        Assert.Single(repository.InsertedEvents);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task IngestBatchAsync_PlayerRevivedWithInvalidReviverGuid_ReturnsPermanentReject(string reviverPlayerId)
    {
        var repository = new FakeTelemetryEventRepository();
        var revivedUserId = Guid.NewGuid();
        var item = CreatePlayerRevived(revivedUserId, Guid.NewGuid(), reviverPlayerId);

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [item] }, revivedUserId, CancellationToken.None);

        AssertReject(result, "TELEMETRY_PLAYER_REVIVED_INVALID");
        Assert.Empty(repository.InsertedEvents);
    }

    [Theory]
    [InlineData("2.0", "TELEMETRY_SCHEMA_UNSUPPORTED")]
    [InlineData("1.0", "TELEMETRY_LEGACY_V10_UNSUPPORTED")]
    public async Task IngestBatchAsync_SchemaDispatcherRejectsUnsupportedPaths(string schemaVersion, string expectedReason)
    {
        var repository = new FakeTelemetryEventRepository();
        var request = CreateMatchStarted();
        request.Events[0].SchemaVersion = schemaVersion;

        var result = await CreateService(repository)
            .IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        AssertReject(result, expectedReason);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_FrozenV10ShapeRemainsExplicitlyUnsupported()
    {
        var repository = new FakeTelemetryEventRepository();
        var request = CreateMatchStarted();
        request.Events[0].SchemaVersion = "1.0";
        request.Events[0].ValueJson = JsonSerializer.SerializeToElement(new
        {
            context = new
            {
                teamSize = 1,
                scenarioConfigVersion = "TEST-SCENARIO-1"
            },
            data = new { mapId = "RESEARCH_FACILITY" }
        });

        var result = await CreateService(repository)
            .IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        AssertReject(result, "TELEMETRY_LEGACY_V10_UNSUPPORTED");
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_MatchStartedSequenceOtherThanOne_ReturnsPermanentReject()
    {
        var repository = new FakeTelemetryEventRepository();

        var result = await CreateService(repository)
            .IngestBatchAsync(CreateMatchStarted(sequence: 2), Guid.NewGuid(), CancellationToken.None);

        AssertReject(result, "TELEMETRY_MATCH_STARTED_INVALID");
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_ResearchEnabledFromStoredMatchStart_AcceptsValidResearchEvent()
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        repository.Boundaries[matchId] = new TelemetryMatchBoundary(true, null);

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [CreateResearchAttack(matchId, 10)] }, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(TelemetryAckStatuses.Accepted, Assert.Single(result.Data!.Items).Status);
        Assert.Single(repository.InsertedEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task IngestBatchAsync_ResearchDisabledOrMissingFromStoredMatchStart_RejectsResearchEvent(bool? enabled)
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        if (enabled.HasValue)
        {
            repository.Boundaries[matchId] = new TelemetryMatchBoundary(enabled.Value, null);
        }

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [CreateResearchAttack(matchId, 10)] }, Guid.NewGuid(), CancellationToken.None);

        AssertReject(result, "TELEMETRY_RESEARCH_CAPTURE_NOT_ENABLED");
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_SameBatchResearchUsesMatchStartedSequenceNotArrayOrder()
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        var start = CreateMatchStarted(matchId, researchCaptureEnabled: true).Events[0];
        var research = CreateResearchAttack(matchId, 2);

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [research, start] }, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(result.Data!.Items, item => Assert.Equal(TelemetryAckStatuses.Accepted, item.Status));
        Assert.Equal([2L, 1L], repository.InsertedEvents.Select(item => item.EventSequence).ToArray());
    }

    [Fact]
    public async Task IngestBatchAsync_RejectedSameBatchMatchStartedDoesNotEnableResearch()
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        var start = CreateMatchStarted(matchId, researchCaptureEnabled: true).Events[0];
        var phase = CreatePhaseStarted(matchId, 1);
        var research = CreateResearchAttack(matchId, 2);

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [phase, start, research] }, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("TELEMETRY_DUPLICATE_SEQUENCE_IN_BATCH", result.Data!.Items[0].RejectReason);
        Assert.Equal("TELEMETRY_DUPLICATE_SEQUENCE_IN_BATCH", result.Data.Items[1].RejectReason);
        Assert.Equal("TELEMETRY_RESEARCH_CAPTURE_NOT_ENABLED", result.Data.Items[2].RejectReason);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_ConflictingSameBatchMatchStartedOrderDoesNotChangeResearchEligibility()
    {
        var matchId = Guid.NewGuid();
        var startEnabled = CreateMatchStarted(matchId, researchCaptureEnabled: true).Events[0];
        var startDisabled = CreateMatchStarted(matchId, researchCaptureEnabled: false).Events[0];
        var research = CreateResearchAttack(matchId, 2);

        var first = await CreateService(new FakeTelemetryEventRepository())
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [startEnabled, startDisabled, research] }, Guid.NewGuid(), CancellationToken.None);
        var second = await CreateService(new FakeTelemetryEventRepository())
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [startDisabled, startEnabled, research] }, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(
            first.Data!.Items.Select(item => (item.Status, item.RejectReason)).ToArray(),
            second.Data!.Items.Select(item => (item.Status, item.RejectReason)).ToArray());
        Assert.Equal("TELEMETRY_DUPLICATE_SEQUENCE_IN_BATCH", first.Data.Items[0].RejectReason);
        Assert.Equal("TELEMETRY_DUPLICATE_SEQUENCE_IN_BATCH", first.Data.Items[1].RejectReason);
        Assert.Equal("TELEMETRY_RESEARCH_CAPTURE_NOT_ENABLED", first.Data.Items[2].RejectReason);
    }

    [Fact]
    public async Task IngestBatchAsync_SameIdConflictingMatchStartedDoesNotEnableResearch()
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        var startEnabled = CreateMatchStarted(matchId, researchCaptureEnabled: true).Events[0];
        var startDisabled = CreateMatchStarted(matchId, researchCaptureEnabled: false).Events[0];
        startDisabled.Id = startEnabled.Id;
        var research = CreateResearchAttack(matchId, 2);

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [startEnabled, startDisabled, research] }, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("TELEMETRY_DUPLICATE_ID_IN_BATCH", result.Data!.Items[0].RejectReason);
        Assert.Equal("TELEMETRY_DUPLICATE_ID_IN_BATCH", result.Data.Items[1].RejectReason);
        Assert.Equal("TELEMETRY_RESEARCH_CAPTURE_NOT_ENABLED", result.Data.Items[2].RejectReason);
        Assert.Empty(repository.InsertedEvents);
    }

    [Theory]
    [InlineData(TelemetryWriteStatus.IdentityConflict, "TELEMETRY_IDENTITY_CONFLICT")]
    [InlineData(TelemetryWriteStatus.SequenceConflict, "TELEMETRY_SEQUENCE_CONFLICT")]
    public async Task IngestBatchAsync_StoredConflictMatchStartedDoesNotEnableResearch(
        TelemetryWriteStatus status,
        string reason)
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        var start = CreateMatchStarted(matchId, researchCaptureEnabled: true).Events[0];
        repository.ConflictResults[start.Id] = new TelemetryWriteItemResult(start.Id, status, reason);
        var research = CreateResearchAttack(matchId, 2);

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [start, research] }, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(reason, result.Data!.Items[0].RejectReason);
        Assert.Equal("TELEMETRY_RESEARCH_CAPTURE_NOT_ENABLED", result.Data.Items[1].RejectReason);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_SameBatchResearchBeforeDisabledMatchStarted_RejectsResearch()
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        var start = CreateMatchStarted(matchId, researchCaptureEnabled: false).Events[0];
        var research = CreateResearchAttack(matchId, 2);

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [research, start] }, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(TelemetryAckStatuses.PermanentlyRejected, result.Data!.Items[0].Status);
        Assert.Equal("TELEMETRY_RESEARCH_CAPTURE_NOT_ENABLED", result.Data.Items[0].RejectReason);
        Assert.Equal(TelemetryAckStatuses.Accepted, result.Data.Items[1].Status);
        Assert.Single(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_ResearchHeardAtCanonicalUtcZ_Accepts()
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        repository.Boundaries[matchId] = new TelemetryMatchBoundary(true, null);

        var result = await CreateService(repository)
            .IngestBatchAsync(
                new TelemetryBatchRequest { Events = [CreateResearchInvestigateStarted(matchId, 10, "2026-08-26T02:12:29.900Z")] },
                Guid.NewGuid(),
                CancellationToken.None);

        Assert.Equal(TelemetryAckStatuses.Accepted, Assert.Single(result.Data!.Items).Status);
    }

    [Theory]
    [InlineData("2026-08-26T02:12:29.900+07:00")]
    [InlineData("2026-08-26T02:12:29.900")]
    [InlineData("not-a-date")]
    public async Task IngestBatchAsync_ResearchHeardAtNonCanonicalUtc_ReturnsPermanentReject(string heardAt)
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        repository.Boundaries[matchId] = new TelemetryMatchBoundary(true, null);

        var result = await CreateService(repository)
            .IngestBatchAsync(
                new TelemetryBatchRequest { Events = [CreateResearchInvestigateStarted(matchId, 10, heardAt)] },
                Guid.NewGuid(),
                CancellationToken.None);

        AssertReject(result, "TELEMETRY_TIMESTAMP_NOT_UTC");
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_InvalidResearchEnum_ReturnsPermanentReject()
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        repository.Boundaries[matchId] = new TelemetryMatchBoundary(true, null);
        var research = CreateResearchAttack(matchId, 10);
        research.ValueJson = ResearchAttackValue(10, outcome: "DOWNED");

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [research] }, Guid.NewGuid(), CancellationToken.None);

        AssertReject(result, "TELEMETRY_INVALID_ENUM_TOKEN");
        Assert.Empty(repository.InsertedEvents);
    }

    [Theory]
    [InlineData(TelemetryWriteStatus.DuplicateAlreadyAccepted, "DUPLICATE_ALREADY_ACCEPTED", null)]
    [InlineData(TelemetryWriteStatus.IdentityConflict, "PERMANENTLY_REJECTED", "TELEMETRY_IDENTITY_CONFLICT")]
    [InlineData(TelemetryWriteStatus.SequenceConflict, "PERMANENTLY_REJECTED", "TELEMETRY_SEQUENCE_CONFLICT")]
    public async Task IngestBatchAsync_RepositoryIdempotencyAndConflicts_MapToCanonicalAcks(
        TelemetryWriteStatus writeStatus,
        string ackStatus,
        string? rejectReason)
    {
        var repository = new FakeTelemetryEventRepository
        {
            ResultFactory = events => new TelemetryWriteResult(events
                .Select(item => new TelemetryWriteItemResult(item.Id, writeStatus, rejectReason))
                .ToArray())
        };

        var result = await CreateService(repository)
            .IngestBatchAsync(CreateMatchStarted(), Guid.NewGuid(), CancellationToken.None);

        var ack = Assert.Single(result.Data!.Items);
        Assert.Equal(ackStatus, ack.Status);
        Assert.Equal(rejectReason, ack.RejectReason);
    }

    [Fact]
    public async Task IngestBatchAsync_DuplicateIdInsideBatch_RejectsAllOccurrences()
    {
        var repository = new FakeTelemetryEventRepository();
        var request = CreateMatchStarted();
        var duplicate = CreatePhaseStarted(request.Events[0].MatchId, 2);
        duplicate.Id = request.Events[0].Id;
        request.Events.Add(duplicate);

        var result = await CreateService(repository)
            .IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("TELEMETRY_DUPLICATE_ID_IN_BATCH", result.Data!.Items[0].RejectReason);
        Assert.Equal("TELEMETRY_DUPLICATE_ID_IN_BATCH", result.Data.Items[1].RejectReason);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_DuplicateSequenceInsideBatch_RejectsAllOccurrences()
    {
        var repository = new FakeTelemetryEventRepository();
        var request = CreateMatchStarted();
        request.Events.Add(CreatePhaseStarted(request.Events[0].MatchId, 1));

        var result = await CreateService(repository)
            .IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("TELEMETRY_DUPLICATE_SEQUENCE_IN_BATCH", result.Data!.Items[0].RejectReason);
        Assert.Equal("TELEMETRY_DUPLICATE_SEQUENCE_IN_BATCH", result.Data.Items[1].RejectReason);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_PartialAcknowledgementKeepsValidItem()
    {
        var repository = new FakeTelemetryEventRepository();
        var request = CreateMatchStarted();
        request.Events.Add(CreateNoiseEvent(Guid.NewGuid(), request.Events[0].MatchId, 2));

        var result = await CreateService(repository)
            .IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(TelemetryAckStatuses.Accepted, result.Data!.Items[0].Status);
        Assert.Equal(ErrorCodes.TelemetryUserMismatch, result.Data.Items[1].RejectReason);
        Assert.Single(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_BoundHostDelegation_AcceptsForeignPlayerEvent()
    {
        var repository = new FakeTelemetryEventRepository();
        var hostUserId = Guid.NewGuid();
        var playerUserId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var authority = new FakeMatchAuthorityService { IsTelemetryDelegationAllowed = true };

        var result = await CreateService(repository, authority)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [CreateNoiseEvent(playerUserId, matchId, 2)] }, hostUserId, CancellationToken.None);

        Assert.Equal(TelemetryAckStatuses.Accepted, Assert.Single(result.Data!.Items).Status);
        Assert.Equal((hostUserId, matchId, playerUserId), authority.LastTelemetryCheck);
        Assert.Equal(playerUserId, Assert.Single(repository.InsertedEvents).UserId);
    }

    [Fact]
    public async Task IngestBatchAsync_UnauthorizedSystemTelemetry_ReturnsPermanentReject()
    {
        var repository = new FakeTelemetryEventRepository();
        var authority = new FakeMatchAuthorityService { IsSystemTelemetryAllowed = false };

        var result = await CreateService(repository, authority)
            .IngestBatchAsync(CreateMatchStarted(), Guid.NewGuid(), CancellationToken.None);

        AssertReject(result, ErrorCodes.TelemetryUserMismatch);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_ExistingMatchEndedRejectsLaterNewEvent()
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        repository.Boundaries[matchId] = new TelemetryMatchBoundary(false, 5);

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [CreatePhaseStarted(matchId, 6)] }, Guid.NewGuid(), CancellationToken.None);

        AssertReject(result, "TELEMETRY_MATCH_TERMINAL_SEQUENCE_EXCEEDED");
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_SameBatchMatchEndedRejectsPostTerminalBySequence()
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [CreatePhaseStarted(matchId, 6), CreateMatchEnded(matchId, 5)] }, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("TELEMETRY_MATCH_TERMINAL_SEQUENCE_EXCEEDED", result.Data!.Items[0].RejectReason);
        Assert.Equal(TelemetryAckStatuses.Accepted, result.Data.Items[1].Status);
        Assert.Single(repository.InsertedEvents);
        Assert.Equal(5, repository.InsertedEvents[0].EventSequence);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IngestBatchAsync_RejectedSameBatchMatchEndedDoesNotBlockLaterEvent(bool reverseOrder)
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        var phaseAtFive = CreatePhaseStarted(matchId, 5);
        var endAtFive = CreateMatchEnded(matchId, 5);
        var phaseAtSix = CreatePhaseStarted(matchId, 6);
        var events = reverseOrder
            ? new List<TelemetryEventRequest> { endAtFive, phaseAtFive, phaseAtSix }
            : [phaseAtFive, endAtFive, phaseAtSix];

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = events }, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("TELEMETRY_DUPLICATE_SEQUENCE_IN_BATCH", result.Data!.Items[0].RejectReason);
        Assert.Equal("TELEMETRY_DUPLICATE_SEQUENCE_IN_BATCH", result.Data.Items[1].RejectReason);
        Assert.Equal(TelemetryAckStatuses.Accepted, result.Data.Items[2].Status);
        Assert.Single(repository.InsertedEvents);
        Assert.Equal(6, repository.InsertedEvents[0].EventSequence);
    }

    [Theory]
    [InlineData(TelemetryWriteStatus.IdentityConflict, "TELEMETRY_IDENTITY_CONFLICT")]
    [InlineData(TelemetryWriteStatus.SequenceConflict, "TELEMETRY_SEQUENCE_CONFLICT")]
    public async Task IngestBatchAsync_StoredConflictMatchEndedDoesNotBlockLaterEvent(
        TelemetryWriteStatus status,
        string reason)
    {
        var repository = new FakeTelemetryEventRepository();
        var matchId = Guid.NewGuid();
        var end = CreateMatchEnded(matchId, 5);
        repository.ConflictResults[end.Id] = new TelemetryWriteItemResult(end.Id, status, reason);

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [end, CreatePhaseStarted(matchId, 6)] }, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(reason, result.Data!.Items[0].RejectReason);
        Assert.Equal(TelemetryAckStatuses.Accepted, result.Data.Items[1].Status);
        Assert.Single(repository.InsertedEvents);
        Assert.Equal(6, repository.InsertedEvents[0].EventSequence);
    }

    [Fact]
    public async Task IngestBatchAsync_MatchEndedDuplicateRetryPreservesRepositoryIdempotency()
    {
        var repository = new FakeTelemetryEventRepository
        {
            ResultFactory = events => new TelemetryWriteResult(events
                .Select(item => new TelemetryWriteItemResult(
                    item.Id,
                    TelemetryWriteStatus.DuplicateAlreadyAccepted))
                .ToArray())
        };
        var matchId = Guid.NewGuid();
        repository.Boundaries[matchId] = new TelemetryMatchBoundary(false, 5);

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [CreateMatchEnded(matchId, 5)] }, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(TelemetryAckStatuses.DuplicateAlreadyAccepted, Assert.Single(result.Data!.Items).Status);
    }

    [Theory]
    [InlineData("2026-08-26T02:12:29.900Z", true)]
    [InlineData("2026-08-26T02:12:29.900+07:00", false)]
    [InlineData("2026-08-26T02:12:29.900", false)]
    [InlineData("not-a-date", false)]
    public void TelemetryBatchRequest_TsPreservesRawJsonAndServiceRejectsInvalidPerItem(string ts, bool shouldParse)
    {
        var json = $$"""
        {
          "events": [
            {
              "id": "{{Guid.NewGuid():D}}",
              "matchId": "{{Guid.NewGuid():D}}",
              "userId": null,
              "eventType": "MATCH_STARTED",
              "ts": "{{ts}}",
              "valueJson": {
                "context": {
                  "eventSequence": 1,
                  "authorityTick": null,
                  "scenarioConfigVersion": "TEST-SCENARIO-1",
                  "policyVersion": "TEST-POLICY-1",
                  "configSource": "FIXED",
                  "teamSize": 1,
                  "buildVersion": "TEST-BUILD-1",
                  "mapContentVersion": "TEST-MAP-1",
                  "contentWhitelistVersion": "TEST-WHITELIST-1",
                  "researchCaptureEnabled": false
                },
                "data": { "mapId": "RESEARCH_FACILITY" }
              },
              "reasonCode": "MATCH_READY",
              "schemaVersion": "1.1"
            }
          ]
        }
        """;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var request = JsonSerializer.Deserialize<TelemetryBatchRequest>(json, options);

        Assert.NotNull(request);
        Assert.Equal(JsonValueKind.String, Assert.Single(request!.Events).Ts.ValueKind);

        var parsed = TelemetryService.TryParseCanonicalUtcTimestamp(Assert.Single(request.Events).Ts, out var parsedTsUtc);
        Assert.Equal(shouldParse, parsed);
        if (shouldParse)
        {
            Assert.Equal(DateTimeKind.Utc, parsedTsUtc.Kind);
        }
    }

    [Fact]
    public async Task IngestBatchAsync_MixedValidAndInvalidTimestamp_BatchPerItemAcknowledgementsAreReturned()
    {
        var repository = new FakeTelemetryEventRepository();
        var matchA = Guid.NewGuid();
        var matchB = Guid.NewGuid();
        var matchC = Guid.NewGuid();

        var validA = CreateMatchStarted(matchA, sequence: 1, researchCaptureEnabled: false).Events[0];
        validA.Ts = UtcTs(DateTime.UtcNow);

        var invalid = CreateMatchStarted(matchB, sequence: 1, researchCaptureEnabled: false).Events[0];
        invalid.Ts = JsonSerializer.SerializeToElement("2026-08-26T02:12:29.900+07:00");

        var validC = CreateMatchStarted(matchC, sequence: 1, researchCaptureEnabled: false).Events[0];
        validC.Ts = UtcTs(DateTime.UtcNow.AddMinutes(1));

        var result = await CreateService(repository)
            .IngestBatchAsync(new TelemetryBatchRequest { Events = [validA, invalid, validC] }, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.Items.Count);
        Assert.Equal(TelemetryAckStatuses.Accepted, result.Data.Items[0].Status);
        Assert.Equal(TelemetryAckStatuses.PermanentlyRejected, result.Data.Items[1].Status);
        Assert.Equal("TELEMETRY_TIMESTAMP_NOT_UTC", result.Data.Items[1].RejectReason);
        Assert.Equal(TelemetryAckStatuses.Accepted, result.Data.Items[2].Status);
    }

    [Theory]
    [InlineData(-8)]
    [InlineData(1)]
    public async Task IngestBatchAsync_TimestampOutsideWindow_ReturnsPerItemReject(int dayOffset)
    {
        var repository = new FakeTelemetryEventRepository();
        var request = CreateMatchStarted();
        request.Events[0].Ts = UtcTs(DateTime.UtcNow.AddDays(dayOffset));

        var result = await CreateService(repository)
            .IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        AssertReject(result, "TELEMETRY_TIMESTAMP_OUT_OF_RANGE");
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_ValueJsonOverLimit_ReturnsPerItemReject()
    {
        var repository = new FakeTelemetryEventRepository();
        var request = CreateMatchStarted();
        request.Events[0].ValueJson = JsonSerializer.SerializeToElement(new
        {
            context = new
            {
                eventSequence = 1,
                authorityTick = (long?)null,
                scenarioConfigVersion = "TEST-SCENARIO-1",
                policyVersion = "TEST-POLICY-1",
                configSource = "FIXED",
                teamSize = 1,
                buildVersion = "TEST-BUILD-1",
                mapContentVersion = "TEST-MAP-1",
                contentWhitelistVersion = "TEST-WHITELIST-1",
                researchCaptureEnabled = false
            },
            data = new { mapId = new string('x', 33_000) }
        });

        var result = await CreateService(repository)
            .IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        AssertReject(result, "TELEMETRY_VALUE_JSON_TOO_LARGE");
        Assert.Empty(repository.InsertedEvents);
    }

    private static JsonElement UtcTs(DateTime? value = null)
    {
        var utc = (value ?? DateTime.UtcNow).ToUniversalTime();
        return JsonSerializer.SerializeToElement(
            utc.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
                CultureInfo.InvariantCulture));
    }

    private static TelemetryEventRequest WithValueJson(TelemetryEventRequest item, JsonElement valueJson)
    {
        item.ValueJson = valueJson;
        return item;
    }

    private static TelemetryEventRequest WithReason(TelemetryEventRequest item, string reason)
    {
        item.ReasonCode = reason;
        return item;
    }

    private static TelemetryService CreateService(
        FakeTelemetryEventRepository repository,
        FakeMatchAuthorityService? authority = null) =>
        new(
            repository,
            authority ?? new FakeMatchAuthorityService(),
            Options.Create(new MongoDbSettings
            {
                DatabaseName = "test",
                TelemetryCollectionName = "telemetry_events",
                MaxBatchSize = 500,
                SupportedSchemaVersion = "1.1",
                MaxValueJsonBytes = 32_768,
                MaxFutureSkewMinutes = 5,
                MaxEventAgeDays = 7
            }));

    private static TelemetryBatchRequest CreateMatchStarted(
        Guid? matchId = null,
        long sequence = 1,
        bool researchCaptureEnabled = false) => new()
    {
        Events =
        [
            new TelemetryEventRequest
            {
                Id = Guid.NewGuid(),
                MatchId = matchId ?? Guid.NewGuid(),
                UserId = null,
                EventType = "MATCH_STARTED",
                Ts = UtcTs(),
                ValueJson = MatchStartedValue(sequence, researchCaptureEnabled),
                ReasonCode = "MATCH_READY",
                SchemaVersion = "1.1"
            }
        ]
    };

    private static TelemetryEventRequest CreateMatchEnded(
        Guid matchId,
        long sequence) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = matchId,
        UserId = null,
        EventType = "MATCH_ENDED",
        Ts = UtcTs(),
        ValueJson = JsonSerializer.SerializeToElement(new
        {
            context = new
            {
                eventSequence = sequence,
                authorityTick = 42,
                scenarioConfigVersion = "TEST-SCENARIO-1",
                policyVersion = "TEST-POLICY-1",
                configSource = "FIXED",
                phase = "MATCH_END"
            },
            data = new
            {
                outcome = "SUCCESS",
                durationSeconds = 10.0,
                survivorCount = 1
            }
        }),
        ReasonCode = "TEAM_ESCAPED",
        SchemaVersion = "1.1"
    };
    private static TelemetryEventRequest CreatePhaseStarted(
        Guid matchId,
        long sequence) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = matchId,
        UserId = null,
        EventType = "PHASE_STARTED",
        Ts = UtcTs(),
        ValueJson = JsonSerializer.SerializeToElement(new
        {
            context = new
            {
                eventSequence = sequence,
                authorityTick = 42,
                scenarioConfigVersion = "TEST-SCENARIO-1",
                policyVersion = "TEST-POLICY-1",
                configSource = "FIXED",
                phase = "CORE_COLLECTION"
            },
            data = new { }
        }),
        ReasonCode = null,
        SchemaVersion = "1.1"
    };

    private static TelemetryEventRequest CreateNoiseEvent(Guid userId, Guid matchId, long sequence) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = matchId,
        UserId = userId,
        EventType = "NOISE_EMITTED",
        Ts = UtcTs(),
        ValueJson = NoiseValue(sequence),
        ReasonCode = "PLAYER_SPRINT",
        SchemaVersion = "1.1"
    };

    private static TelemetryEventRequest CreatePlayerRevived(
        Guid userId,
        Guid matchId,
        string reviverPlayerId) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = matchId,
        UserId = userId,
        EventType = "PLAYER_REVIVED",
        Ts = UtcTs(),
        ValueJson = JsonSerializer.SerializeToElement(new
        {
            context = new
            {
                eventSequence = 2,
                authorityTick = 42,
                scenarioConfigVersion = "TEST-SCENARIO-1",
                policyVersion = "TEST-POLICY-1",
                configSource = "FIXED",
                phase = "FINAL_HUNT"
            },
            data = new
            {
                reviverPlayerId,
                reviveCount = 1,
                usedFirstAidKit = false
            }
        }),
        ReasonCode = "TEAMMATE_REVIVE",
        SchemaVersion = "1.1"
    };

    private static TelemetryEventRequest CreateResearchAttack(Guid matchId, long sequence) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = matchId,
        UserId = null,
        EventType = "MONSTER_ATTACK_RESOLVED",
        Ts = UtcTs(),
        ValueJson = ResearchAttackValue(sequence),
        ReasonCode = null,
        SchemaVersion = "1.1"
    };

    private static TelemetryEventRequest CreateResearchInvestigateStarted(
        Guid matchId,
        long sequence,
        string heardAt) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = matchId,
        UserId = null,
        EventType = "MONSTER_INVESTIGATE_STARTED",
        Ts = UtcTs(),
        ValueJson = JsonSerializer.SerializeToElement(new
        {
            context = new
            {
                eventSequence = sequence,
                authorityTick = 42,
                scenarioConfigVersion = "TEST-SCENARIO-1",
                policyVersion = "TEST-POLICY-1",
                configSource = "FIXED",
                phase = "CORE_COLLECTION",
                monsterType = "LISTENER",
                monsterId = "listener-1",
                researchCaptureEnabled = true
            },
            data = new
            {
                investigationEpisodeId = "investigation-1",
                noiseEventId = "noise-1",
                noiseType = "SPRINT",
                heardAt,
                selectionReason = "INITIAL_HIGHEST_AUDIBILITY"
            }
        }),
        ReasonCode = null,
        SchemaVersion = "1.1"
    };

    private static JsonElement MatchStartedValue(long sequence, bool researchCaptureEnabled) =>
        JsonSerializer.SerializeToElement(new
        {
            context = new
            {
                eventSequence = sequence,
                authorityTick = (long?)null,
                scenarioConfigVersion = "TEST-SCENARIO-1",
                policyVersion = "TEST-POLICY-1",
                configSource = "FIXED",
                teamSize = 1,
                buildVersion = "TEST-BUILD-1",
                mapContentVersion = "TEST-MAP-1",
                contentWhitelistVersion = "TEST-WHITELIST-1",
                researchCaptureEnabled
            },
            data = new { mapId = "RESEARCH_FACILITY" }
        });

    private static JsonElement NoiseValue(
        long sequence,
        string noiseType = "SPRINT",
        bool contextExtra = false,
        bool dataExtra = false,
        bool positionExtra = false)
    {
        var position = new Dictionary<string, object?> { ["x"] = 1.0, ["y"] = 2.0, ["z"] = 3.0 };
        if (positionExtra) position["w"] = 4.0;
        var context = new Dictionary<string, object?>
        {
            ["eventSequence"] = sequence,
            ["authorityTick"] = 42,
            ["scenarioConfigVersion"] = "TEST-SCENARIO-1",
            ["policyVersion"] = "TEST-POLICY-1",
            ["configSource"] = "FIXED",
            ["phase"] = "CORE_COLLECTION",
            ["position"] = position
        };
        if (contextExtra) context["extra"] = "x";
        var data = new Dictionary<string, object?>
        {
            ["noiseEventId"] = "noise-42",
            ["noiseType"] = noiseType,
            ["loudness"] = 0.7,
            ["hearingRadius"] = 12.0
        };
        if (dataExtra) data["extra"] = "x";
        return JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["context"] = context, ["data"] = data });
    }

    private static JsonElement ResearchAttackValue(long sequence, string outcome = "HIT") =>
        JsonSerializer.SerializeToElement(new
        {
            context = new
            {
                eventSequence = sequence,
                authorityTick = 42,
                scenarioConfigVersion = "TEST-SCENARIO-1",
                policyVersion = "TEST-POLICY-1",
                configSource = "FIXED",
                phase = "FINAL_HUNT",
                monsterType = "STALKER",
                monsterId = "stalker-1",
                researchCaptureEnabled = true
            },
            data = new { attackEpisodeId = "attack-1", outcome }
        });

    private static void AssertReject(ServiceResult<TelemetryBatchResponse> result, string expectedReason)
    {
        Assert.True(result.IsSuccess);
        var ack = Assert.Single(result.Data!.Items);
        Assert.Equal(TelemetryAckStatuses.PermanentlyRejected, ack.Status);
        Assert.Equal(expectedReason, ack.RejectReason);
    }

    private sealed class FakeTelemetryEventRepository : ITelemetryEventRepository
    {
        public List<TelemetryEventDocument> InsertedEvents { get; } = [];
        public Dictionary<Guid, TelemetryMatchBoundary> Boundaries { get; } = [];
        public Dictionary<Guid, TelemetryWriteItemResult> ConflictResults { get; } = [];
        public Func<IReadOnlyCollection<TelemetryEventDocument>, TelemetryWriteResult>? ResultFactory { get; init; }

        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TelemetryWriteResult> AtomicCommitBatchAsync(
            IReadOnlyCollection<TelemetryEventDocument> events,
            CancellationToken cancellationToken = default)
        {
            return InsertBatchAsync(events, cancellationToken);
        }

        public Task<TelemetryWriteResult> InsertBatchAsync(
            IReadOnlyCollection<TelemetryEventDocument> events,
            CancellationToken cancellationToken)
        {
            var result = ResultFactory?.Invoke(events) ?? new TelemetryWriteResult(events
                .Select(item => ConflictResults.TryGetValue(item.Id, out var conflict)
                    ? conflict
                    : new TelemetryWriteItemResult(item.Id, TelemetryWriteStatus.Accepted))
                .ToArray());
            var resultsById = result.Items.ToDictionary(item => item.Id);
            InsertedEvents.AddRange(events.Where(item =>
                resultsById.TryGetValue(item.Id, out var write) &&
                write.Status == TelemetryWriteStatus.Accepted));

            return Task.FromResult(result);
        }

        public Task<IReadOnlyDictionary<Guid, TelemetryWriteItemResult>> LoadConflictsAsync(
            IReadOnlyCollection<TelemetryEventDocument> events,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<Guid, TelemetryWriteItemResult> result = events
                .Where(item => ConflictResults.ContainsKey(item.Id))
                .ToDictionary(item => item.Id, item => ConflictResults[item.Id]);
            return Task.FromResult(result);
        }

        public Task<IReadOnlyDictionary<Guid, TelemetryMatchBoundary>> LoadMatchBoundariesAsync(
            IReadOnlyCollection<Guid> matchIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<Guid, TelemetryMatchBoundary> result = Boundaries
                .Where(pair => matchIds.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            return Task.FromResult(result);
        }
    }

    private sealed class FakeMatchAuthorityService : IMatchAuthorityService
    {
        public bool IsTelemetryDelegationAllowed { get; init; }
        public bool IsSystemTelemetryAllowed { get; init; } = true;
        public (Guid Host, Guid Match, Guid Player)? LastTelemetryCheck { get; private set; }

        public Task<bool> CanSubmitTelemetryAsync(
            Guid submittingUserId,
            Guid matchId,
            Guid eventUserId,
            CancellationToken cancellationToken)
        {
            LastTelemetryCheck = (submittingUserId, matchId, eventUserId);
            return Task.FromResult(IsTelemetryDelegationAllowed);
        }

        public Task<bool> CanSubmitSystemTelemetryAsync(Guid submittingUserId, Guid matchId, CancellationToken cancellationToken) =>
            Task.FromResult(IsSystemTelemetryAllowed);

        public Task<ServiceResult<MatchAuthorityResponse>> CreateAsync(Guid hostUserId, CreateMatchAuthorityRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResult<JoinProofResponse>> IssueJoinProofAsync(Guid userId, Guid matchId, IssueJoinProofRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResult<MatchPlayerBindingResponse>> BindPlayerAsync(Guid hostUserId, Guid matchId, BindMatchPlayerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResult<MatchAuthorityResponse>> RenewLeaseAsync(Guid hostUserId, Guid matchId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResult<MatchAuthorityResponse>> StartAsync(Guid hostUserId, Guid matchId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResult<MatchPlayerBindingResponse>> MarkPlayerDisconnectedAsync(Guid hostUserId, Guid matchId, int fusionActorNumber, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResult<MatchAuthorityResponse>> EndAsync(Guid hostUserId, Guid matchId, EndMatchAuthorityRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
