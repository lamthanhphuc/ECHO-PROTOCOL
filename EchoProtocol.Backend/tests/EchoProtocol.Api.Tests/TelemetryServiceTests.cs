using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Configurations;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Telemetry;
using EchoProtocol.Api.DTOs.MatchAuthority;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.Extensions.Options;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed class TelemetryServiceTests
{
    [Fact]
    public async Task IngestBatchAsync_CanonicalV11MatchStart_StoresValueJsonAndReturnsAcceptedAck()
    {
        var repository = new FakeTelemetryEventRepository();
        var service = CreateService(repository);
        var request = CreateMatchStarted();

        var result = await service.IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var ack = Assert.Single(result.Data!.Items);
        Assert.Equal(request.Events[0].Id, ack.Id);
        Assert.Equal(TelemetryAckStatuses.Accepted, ack.Status);
        Assert.Null(ack.RejectReason);
        var stored = Assert.Single(repository.InsertedEvents);
        Assert.Equal("1.1", stored.SchemaVersion);
        Assert.Equal(1, stored.EventSequence);
        Assert.Equal("RESEARCH_FACILITY", stored.ValueJson["data"]!["mapId"]!.AsString);
        Assert.NotEmpty(stored.SemanticFingerprint);
    }

    [Fact]
    public async Task IngestBatchAsync_RepositoryDuplicate_ReturnsDuplicateAlreadyAcceptedAck()
    {
        var repository = new FakeTelemetryEventRepository
        {
            ResultFactory = events => new TelemetryWriteResult(events
                .Select(item => new TelemetryWriteItemResult(
                    item.Id,
                    TelemetryWriteStatus.DuplicateAlreadyAccepted))
                .ToArray())
        };
        var service = CreateService(repository);

        var result = await service.IngestBatchAsync(
            CreateMatchStarted(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            TelemetryAckStatuses.DuplicateAlreadyAccepted,
            Assert.Single(result.Data!.Items).Status);
    }

    [Fact]
    public async Task IngestBatchAsync_MixedValidAndForeignUser_ReturnsPerItemPartialAck()
    {
        var repository = new FakeTelemetryEventRepository();
        var service = CreateService(repository);
        var authenticatedUserId = Guid.NewGuid();
        var request = CreateMatchStarted();
        request.Events.Add(CreateNoiseEvent(Guid.NewGuid(), request.Events[0].MatchId, 2));

        var result = await service.IngestBatchAsync(
            request, authenticatedUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Items.Count);
        Assert.Equal(TelemetryAckStatuses.Accepted, result.Data.Items[0].Status);
        Assert.Equal(TelemetryAckStatuses.PermanentlyRejected, result.Data.Items[1].Status);
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
        var authority = new FakeMatchAuthorityService
        {
            IsTelemetryDelegationAllowed = true
        };
        var service = CreateService(repository, authority);
        var request = new TelemetryBatchRequest
        {
            Events = [CreateNoiseEvent(playerUserId, matchId, 2)]
        };

        var result = await service.IngestBatchAsync(
            request, hostUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TelemetryAckStatuses.Accepted, Assert.Single(result.Data!.Items).Status);
        Assert.Equal((hostUserId, matchId, playerUserId), authority.LastTelemetryCheck);
        Assert.Equal(playerUserId, Assert.Single(repository.InsertedEvents).UserId);
    }

    [Fact]
    public async Task IngestBatchAsync_UnsupportedSchema_ReturnsPermanentPerItemReject()
    {
        var repository = new FakeTelemetryEventRepository();
        var service = CreateService(repository);
        var request = CreateMatchStarted();
        request.Events[0].SchemaVersion = "2.0";

        var result = await service.IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var ack = Assert.Single(result.Data!.Items);
        Assert.Equal(TelemetryAckStatuses.PermanentlyRejected, ack.Status);
        Assert.Equal(ErrorCodes.TelemetrySchemaUnsupported, ack.RejectReason);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_BatchOverLimit_ReturnsEnvelopeFailure()
    {
        var repository = new FakeTelemetryEventRepository();
        var service = CreateService(repository);
        var request = CreateMatchStarted();
        request.Events = Enumerable.Range(0, 501)
            .Select(index =>
            {
                var item = CreateMatchStarted().Events[0];
                item.Id = Guid.NewGuid();
                item.MatchId = request.Events[0].MatchId;
                item.ValueJson = MatchStartedValue(index + 1);
                return item;
            })
            .ToList();

        var result = await service.IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_DuplicateIdInsideBatch_RejectsOnlySecondOccurrence()
    {
        var repository = new FakeTelemetryEventRepository();
        var service = CreateService(repository);
        var request = CreateMatchStarted();
        var duplicate = CreateMatchStarted().Events[0];
        duplicate.Id = request.Events[0].Id;
        duplicate.MatchId = request.Events[0].MatchId;
        duplicate.EventType = "PHASE_STARTED";
        duplicate.ReasonCode = null;
        duplicate.ValueJson = PhaseValue(2, "CORE_COLLECTION");
        request.Events.Add(duplicate);

        var result = await service.IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TelemetryAckStatuses.Accepted, result.Data!.Items[0].Status);
        Assert.Equal(TelemetryAckStatuses.PermanentlyRejected, result.Data.Items[1].Status);
        Assert.Equal("TELEMETRY_DUPLICATE_ID_IN_BATCH", result.Data.Items[1].RejectReason);
        Assert.Single(repository.InsertedEvents);
    }

    [Theory]
    [InlineData(-8)]
    [InlineData(1)]
    public async Task IngestBatchAsync_TimestampOutsideWindow_ReturnsPerItemReject(int dayOffset)
    {
        var repository = new FakeTelemetryEventRepository();
        var service = CreateService(repository);
        var request = CreateMatchStarted();
        request.Events[0].Ts = DateTime.UtcNow.AddDays(dayOffset);

        var result = await service.IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "TELEMETRY_TIMESTAMP_OUT_OF_RANGE",
            Assert.Single(result.Data!.Items).RejectReason);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_ValueJsonOverLimit_ReturnsPerItemReject()
    {
        var repository = new FakeTelemetryEventRepository();
        var service = CreateService(repository);
        var request = CreateMatchStarted();
        request.Events[0].ValueJson = JsonSerializer.SerializeToElement(new
        {
            context = CommonContext(1),
            data = new { mapId = new string('x', 33_000) }
        });

        var result = await service.IngestBatchAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "TELEMETRY_VALUE_JSON_TOO_LARGE",
            Assert.Single(result.Data!.Items).RejectReason);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task IngestBatchAsync_CanonicalNoiseEvent_MapsUserAndSequence()
    {
        var repository = new FakeTelemetryEventRepository();
        var service = CreateService(repository);
        var userId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var request = new TelemetryBatchRequest
        {
            Events = [CreateNoiseEvent(userId, matchId, 2)]
        };

        var result = await service.IngestBatchAsync(request, userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TelemetryAckStatuses.Accepted, Assert.Single(result.Data!.Items).Status);
        var stored = Assert.Single(repository.InsertedEvents);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal(2, stored.EventSequence);
        Assert.Equal("noise-42", stored.ValueJson["data"]!["noiseEventId"]!.AsString);
    }

    [Fact]
    public async Task IngestBatchAsync_NoiseReasonMismatch_ReturnsPermanentReject()
    {
        var repository = new FakeTelemetryEventRepository();
        var service = CreateService(repository);
        var userId = Guid.NewGuid();
        var noise = CreateNoiseEvent(userId, Guid.NewGuid(), 2);
        noise.ReasonCode = "CORE_DROP";

        var result = await service.IngestBatchAsync(
            new TelemetryBatchRequest { Events = [noise] },
            userId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "TELEMETRY_NOISE_EVENT_INVALID",
            Assert.Single(result.Data!.Items).RejectReason);
        Assert.Empty(repository.InsertedEvents);
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

    private static TelemetryBatchRequest CreateMatchStarted() => new()
    {
        Events =
        [
            new TelemetryEventRequest
            {
                Id = Guid.NewGuid(),
                MatchId = Guid.NewGuid(),
                UserId = null,
                EventType = "MATCH_STARTED",
                Ts = DateTime.UtcNow,
                ValueJson = MatchStartedValue(1),
                ReasonCode = "MATCH_READY",
                SchemaVersion = "1.1"
            }
        ]
    };

    private static TelemetryEventRequest CreateNoiseEvent(Guid userId, Guid matchId, long sequence) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = matchId,
        UserId = userId,
        EventType = "NOISE_EMITTED",
        Ts = DateTime.UtcNow,
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
                position = new { x = 1.0, y = 2.0, z = 3.0 }
            },
            data = new
            {
                noiseEventId = "noise-42",
                noiseType = "SPRINT",
                loudness = 0.7,
                hearingRadius = 12.0
            }
        }),
        ReasonCode = "PLAYER_SPRINT",
        SchemaVersion = "1.1"
    };

    private static JsonElement MatchStartedValue(long sequence) =>
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
                researchCaptureEnabled = false
            },
            data = new { mapId = "RESEARCH_FACILITY" }
        });

    private static JsonElement PhaseValue(long sequence, string phase) =>
        JsonSerializer.SerializeToElement(new
        {
            context = new
            {
                eventSequence = sequence,
                authorityTick = 42,
                scenarioConfigVersion = "TEST-SCENARIO-1",
                policyVersion = "TEST-POLICY-1",
                configSource = "FIXED",
                phase
            },
            data = new { }
        });

    private static object CommonContext(long sequence) => new
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
        researchCaptureEnabled = false
    };

    private sealed class FakeTelemetryEventRepository : ITelemetryEventRepository
    {
        public List<TelemetryEventDocument> InsertedEvents { get; } = [];
        public Func<IReadOnlyCollection<TelemetryEventDocument>, TelemetryWriteResult>? ResultFactory { get; init; }

        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TelemetryWriteResult> InsertBatchAsync(
            IReadOnlyCollection<TelemetryEventDocument> events,
            CancellationToken cancellationToken)
        {
            InsertedEvents.AddRange(events);
            return Task.FromResult(ResultFactory?.Invoke(events) ?? new TelemetryWriteResult(events
                .Select(item => new TelemetryWriteItemResult(item.Id, TelemetryWriteStatus.Accepted))
                .ToArray()));
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
