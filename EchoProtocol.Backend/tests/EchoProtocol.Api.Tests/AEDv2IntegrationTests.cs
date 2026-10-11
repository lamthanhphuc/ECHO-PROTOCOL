using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data.Telemetry;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace EchoProtocol.Api.Tests;

public sealed partial class ScenarioServiceTests
{
    [Fact, Trait("Category", "AEDv2")]
    public void AEDv2_BackendFingerprintMatchesUnityCatalog()
    {
        var normal = ScenarioAdaptivePlanV2Service.NormalPlanValues();
        Assert.Equal("d725f010f4f456aaaecc8a74def8cecd",
            ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(normal));
        normal[0] = 2;
        Assert.Equal("644d57e5f4ec57e7b4c265d21dc338df",
            ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(normal));
        normal[0] = 0;
        normal[2] = 1.5;
        Assert.Equal("b492dc7e6ea456b187bdc80c7967fd1b",
            ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(normal));
    }

    [Fact, Trait("Category", "AEDv2")]
    public void AEDv2_PhaseEvidenceFingerprintMatchesUnityContractV2()
    {
        Assert.Equal("2a3ad9de5f5f5ee16fbb36879049951b5e12ab83a96912b75fe84481fd22cd18",
            AEDv2PhaseEvidenceVerifier.ComputeCanonicalFingerprint(
                Guid.Parse("00000000-0000-0000-0000-000000000001"), "ZONE_1_OBJECTIVE", 1,
                "roster-fingerprint", DateTime.Parse("2026-10-08T00:00:00.123Z"),
                DateTime.Parse("2026-10-08T00:01:00.456Z"), 2, 1, 2, 0, 3, 4, 5,
                true, Array.Empty<string>()));
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_BoundaryRejectsFingerprintWithoutAcceptedPhaseEvidence()
    {
        await using var f = await ValidAEDv2Fixture();
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var verifier = new AEDv2PhaseEvidenceVerifier(f.Db, new NoAcceptedEvidenceRepository());
        Assert.False(await verifier.VerifyAsync(f.MatchId, 1, new string('a', 64)));
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_VerifiesPhaseOneBeforePhaseTwoStarts() => await AssertPhaseOneVerification(events => events);

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_VerifiesPhaseOneAfterPhaseTwoStarts() => await AssertPhaseOneVerification(events => events.Append(PhaseEvent(events[0].MatchId, "PHASE_STARTED", "PHASE_TWO", 4, 30)));

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_RejectsPhaseWithoutCompletion() => await AssertPhaseOneRejected(events => events.Where(e => e.EventType != "PHASE_COMPLETED"));

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_RejectsDuplicatePhaseCompletion() => await AssertPhaseOneRejected(events => events.Append(PhaseEvent(events[0].MatchId, "PHASE_COMPLETED", "PHASE_ONE", 4, 25)));

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_RejectsSequenceGapInsidePhase() => await AssertPhaseOneRejected(events => events.Select(e => e.EventSequence >= 2 && e.EventSequence <= 3 ? CopySequence(e, e.EventSequence + 1) : e));

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_RejectsWrongFingerprint() => await AssertPhaseOneRejected(events => events, fingerprintOverride: new string('f', 64));

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_RejectsWrongMatch()
    {
        await using var f = await ValidAEDv2Fixture();
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var repo = new AcceptedEvidenceRepository(PhaseEvidence(f));
        Assert.False(await new AEDv2PhaseEvidenceVerifier(f.Db, repo).VerifyAsync(Guid.NewGuid(), 1, PhaseFingerprint(f, 1)));
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_RejectsCrossPhaseEvent() => await AssertPhaseOneRejected(events => events.Select(e => e.EventType == "PLAYER_DOWNED" ? PhaseEvent(e.MatchId, e.EventType, "PHASE_TWO", e.EventSequence, 15, e.UserId) : e));

    private static async Task AssertPhaseOneVerification(Func<TelemetryEventDocument[], IEnumerable<TelemetryEventDocument>> mutate)
    {
        await using var f = await ValidAEDv2Fixture();
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var events = mutate(PhaseEvidence(f)).ToArray();
        Assert.True(await new AEDv2PhaseEvidenceVerifier(f.Db, new AcceptedEvidenceRepository(events)).VerifyAsync(f.MatchId, 1, PhaseFingerprint(f, 1)));
    }

    private static async Task AssertPhaseOneRejected(Func<TelemetryEventDocument[], IEnumerable<TelemetryEventDocument>> mutate, string? fingerprintOverride = null)
    {
        await using var f = await ValidAEDv2Fixture();
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var events = mutate(PhaseEvidence(f)).ToArray();
        Assert.False(await new AEDv2PhaseEvidenceVerifier(f.Db, new AcceptedEvidenceRepository(events)).VerifyAsync(f.MatchId, 1, fingerprintOverride ?? PhaseFingerprint(f, 1)));
    }

    private static TelemetryEventDocument[] PhaseEvidence(Fixture f)
    {
        var users = f.Db.MatchPlayerBindings.Where(x => x.MatchId == f.MatchId).Select(x => x.UserId).ToArray();
        return new[]
        {
            PhaseEvent(f.MatchId, "PHASE_STARTED", "PHASE_ONE", 1, 0),
            PhaseEvent(f.MatchId, "PLAYER_DOWNED", "PHASE_ONE", 2, 10, users[0]),
            PhaseEvent(f.MatchId, "PHASE_COMPLETED", "PHASE_ONE", 3, 20)
        };
    }

    private static string PhaseFingerprint(Fixture f, int ordinal)
    {
        var users = f.Db.MatchPlayerBindings.Where(x => x.MatchId == f.MatchId).Select(x => x.UserId).ToArray();
        var roster = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(f.MatchId.ToString("D") + "|" + string.Join(',', users.Select(x => x.ToString("D")).Order(StringComparer.Ordinal))))).ToLowerInvariant();
        return AEDv2PhaseEvidenceVerifier.ComputeCanonicalFingerprint(f.MatchId, "PHASE_ONE", ordinal, roster, DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(20), users.Length, 1, 0, 0, 0, 0, 0, true, Array.Empty<string>());
    }

    private static TelemetryEventDocument PhaseEvent(Guid matchId, string type, string phase, long sequence, int seconds, Guid? userId = null) => new()
    {
        Id = Guid.NewGuid(), MatchId = matchId, UserId = userId, EventType = type,
        EventSequence = sequence, Ts = DateTime.UnixEpoch.AddSeconds(seconds), SchemaVersion = "1.1",
        SemanticFingerprint = new string('a', 64), IngestedAt = DateTime.UnixEpoch,
        ValueJson = new BsonDocument { ["context"] = new BsonDocument { ["phase"] = phase }, ["data"] = new BsonDocument() }
    };

    private static TelemetryEventDocument CopySequence(TelemetryEventDocument source, long sequence) => new()
    {
        Id = source.Id, MatchId = source.MatchId, UserId = source.UserId, EventType = source.EventType,
        EventSequence = sequence, Ts = source.Ts, SchemaVersion = source.SchemaVersion,
        SemanticFingerprint = source.SemanticFingerprint, IngestedAt = source.IngestedAt, ValueJson = source.ValueJson
    };

    private sealed class AcceptedEvidenceRepository(IReadOnlyList<TelemetryEventDocument> events) : ITelemetryEventRepository
    {
        public Task<IReadOnlyList<TelemetryEventDocument>> LoadAcceptedMatchEventsAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TelemetryEventDocument>>(events.Where(e => e.MatchId == matchId).ToArray());
        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TelemetryWriteResult> AtomicCommitBatchAsync(IReadOnlyCollection<TelemetryEventDocument> events, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TelemetryWriteResult> InsertBatchAsync(IReadOnlyCollection<TelemetryEventDocument> events, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, TelemetryWriteItemResult>> LoadConflictsAsync(IReadOnlyCollection<TelemetryEventDocument> events, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, TelemetryMatchBoundary>> LoadMatchBoundariesAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_SourceReferenceMustMatchAcceptedOccurrence()
    {
        await using var f = await ValidAEDv2Fixture();
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();

        var events = PhaseEvidence(f);
        var downEvent = events.Single(x => x.EventType == "PLAYER_DOWNED");
        downEvent.ValueJson["context"].AsBsonDocument["occurrenceKey"] = "down-2";
        var users = f.Db.MatchPlayerBindings.Where(x => x.MatchId == f.MatchId).Select(x => x.UserId).ToArray();
        var roster = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(f.MatchId.ToString("D") + "|" + string.Join(",", users.Select(x => x.ToString("D")).Order(StringComparer.Ordinal))))).ToLowerInvariant();
        var reference = new AEDPhase8SourceReferenceV1(downEvent.Id, downEvent.EventSequence, downEvent.EventType, "down-2");
        var request = new AEDPhase8SourceUploadRequestV1(1, PhaseFingerprint(f, 1), roster, "TEST_CONTEXT", "P8_SOURCE_TEST_V1", new[] { reference }, Array.Empty<AEDPhase8MetricEvidenceV1>(), Array.Empty<AEDPhase8SupplyEvidenceV1>(), Array.Empty<AEDPhase8MinionEvidenceV1>());
        var verifier = new AEDPhase8SourceVerifierV1(f.Db, new AcceptedEvidenceRepository(events), new AEDv2PhaseEvidenceVerifier(f.Db, new AcceptedEvidenceRepository(events)));

        var valid = await verifier.VerifyAsync(f.MatchId, request);
        Assert.True(valid.CanonicalVerified);
        Assert.Equal("P8_SOURCE_AUTHORITATIVE_COVERAGE_INCOMPLETE", valid.ReasonCode);

        var invalid = await verifier.VerifyAsync(f.MatchId, request with { SourceEvents = new[] { reference with { OccurrenceKey = "forged-occurrence" } } });
        Assert.Equal("P8_SOURCE_EVENT_MISMATCH", invalid.ReasonCode);
        var duplicated = await verifier.VerifyAsync(f.MatchId, request with { SourceEvents = new[] { reference, reference } });
        Assert.Equal("P8_SOURCE_REFERENCE_INVALID", duplicated.ReasonCode);
        var missing = await verifier.VerifyAsync(f.MatchId, request with { SourceEvents = Array.Empty<AEDPhase8SourceReferenceV1>() });
        Assert.Equal("P8_SOURCE_COVERAGE_MISMATCH", missing.ReasonCode);

        var omitted = PhaseEvent(f.MatchId, "SECURITY_HOLD_INTERRUPTED", "PHASE_ONE", 3, 15);
        omitted.ValueJson["context"].AsBsonDocument["occurrenceKey"] = "hold-3";
        var expandedEvents = events
            .Select(x => x.EventType == "PHASE_COMPLETED"
                ? CopySequence(x, 4)
                : x)
            .Append(omitted)
            .ToArray();
        var expandedRepo = new AcceptedEvidenceRepository(expandedEvents);
        var expandedVerifier = new AEDPhase8SourceVerifierV1(f.Db, expandedRepo, new AEDv2PhaseEvidenceVerifier(f.Db, expandedRepo));
        var uncovered = await expandedVerifier.VerifyAsync(f.MatchId, request);
        Assert.True(uncovered.CanonicalVerified);
        Assert.Equal("P8_SOURCE_COVERAGE_MISMATCH", uncovered.ReasonCode);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_EmptyPhaseAcceptsEmptySourceReferences()
    {
        await using var f = await ValidAEDv2Fixture();
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();

        var events = new[]
        {
            PhaseEvent(f.MatchId, "PHASE_STARTED", "PHASE_ONE", 1, 0),
            PhaseEvent(f.MatchId, "PHASE_COMPLETED", "PHASE_ONE", 2, 20)
        };
        var users = f.Db.MatchPlayerBindings.Where(x => x.MatchId == f.MatchId).Select(x => x.UserId).ToArray();
        var roster = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(f.MatchId.ToString("D") + "|" + string.Join(",", users.Select(x => x.ToString("D")).Order(StringComparer.Ordinal))))).ToLowerInvariant();
        var fingerprint = AEDv2PhaseEvidenceVerifier.ComputeCanonicalFingerprint(f.MatchId, "PHASE_ONE", 1, roster, DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(20), users.Length, 0, 0, 0, 0, 0, 0, true, Array.Empty<string>());
        var request = new AEDPhase8SourceUploadRequestV1(1, fingerprint, roster, "TEST_CONTEXT", "P8_SOURCE_TEST_V1", Array.Empty<AEDPhase8SourceReferenceV1>(), Array.Empty<AEDPhase8MetricEvidenceV1>(), Array.Empty<AEDPhase8SupplyEvidenceV1>(), Array.Empty<AEDPhase8MinionEvidenceV1>());
        var repository = new AcceptedEvidenceRepository(events);
        var verifier = new AEDPhase8SourceVerifierV1(f.Db, repository, new AEDv2PhaseEvidenceVerifier(f.Db, repository));

        var result = await verifier.VerifyAsync(f.MatchId, request);
        Assert.True(result.CanonicalVerified);
        Assert.Equal("P8_SOURCE_AUTHORITATIVE_COVERAGE_INCOMPLETE", result.ReasonCode);
        Assert.False(result.HistoricalSkillVerified);
        Assert.False(result.PressureVerified);
        Assert.False(result.SupplyLedgerVerified);
        Assert.False(result.MinionPopulationVerified);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_PreMatchGameplayCommitIsRejected()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        var request = V2Request(scenario, 0, "PRE_MATCH", "SupportBonus", 0, 2, "RELIEVE", "COMMITTED");
        var first = await service.SubmitAsync(f.HostId, f.MatchId, request);
        Assert.Equal(ErrorCodes.ValidationError, first.ErrorCode);
        Assert.Empty(await f.Db.ScenarioAdaptivePlansV2.ToArrayAsync());
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_ShadowCannotBeConfirmedApplied()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        var request = V2Request(scenario, 0, "PRE_MATCH", "SupportBonus", 0, 2, "RELIEVE", "SHADOW_ONLY");
        var shadow = await service.SubmitAsync(f.HostId, f.MatchId, request);
        Assert.True(shadow.IsSuccess);
        Assert.Equal("NOT_APPLICABLE", shadow.Data!.ApplyStatus);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var applied = await service.ConfirmAppliedAsync(f.HostId, f.MatchId,
            request.DecisionId, request.ResultingPlanFingerprint);
        Assert.Equal(ErrorCodes.ScenarioApplyConflict, applied.ErrorCode);
        Assert.Null((await f.Db.ScenarioAdaptivePlansV2.SingleAsync()).AppliedAtUtc);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_RejectsNonHostStaleProfileAndTwoKeyChange()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        var request = V2Request(scenario, 0, "PRE_MATCH", "SupportBonus", 0, 2, "RELIEVE", "SHADOW_ONLY");
        Assert.Equal(ErrorCodes.MatchAuthorityForbidden,
            (await service.SubmitAsync(f.PlayerId, f.MatchId, request)).ErrorCode);
        request.PlanValues[4] = 8;
        request.ResultingPlanFingerprint = ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(request.PlanValues);
        Assert.Equal(ErrorCodes.ScenarioApplyConflict,
            (await service.SubmitAsync(f.HostId, f.MatchId, request)).ErrorCode);
        request.PlanValues[4] = 7.5;
        request.ResultingPlanFingerprint = ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(request.PlanValues);
        f.Db.PlayerAIProfiles.Single(x => x.UserId == f.PlayerId).ProfileRevision++;
        await f.Db.SaveChangesAsync();
        Assert.Equal(ErrorCodes.ScenarioDecisionStaleRoster,
            (await service.SubmitAsync(f.HostId, f.MatchId, request)).ErrorCode);
        Assert.Empty(await f.Db.ScenarioAdaptivePlansV2.ToArrayAsync());
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_BoundaryAccumulatesOneNewKeyAndRejectsStaleOrdinal()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var boundary = V2Request(scenario, 1, "ALLOWED_PHASE_BOUNDARY", "DetectionAcquireSeconds",
            1.25, 1.5, "RELIEVE", "COMMITTED");
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, boundary)).IsSuccess);
        Assert.Single(await f.Db.ScenarioAdaptivePlansV2.ToArrayAsync());
        var boundaryApplied = await service.ConfirmAppliedAsync(
            f.HostId, f.MatchId,
            boundary.DecisionId, boundary.ResultingPlanFingerprint);
        Assert.True(boundaryApplied.IsSuccess);
        var stale = V2Request(scenario, 1, "ALLOWED_PHASE_BOUNDARY", "ChaseSpeed",
            7.5, 8, "INCREASE_PRESSURE", "COMMITTED");
        Assert.Equal(ErrorCodes.ScenarioDecisionConflict,
            (await service.SubmitAsync(f.HostId, f.MatchId, stale)).ErrorCode);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_PendingPlanBlocksNextDecision()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(
            f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var first = V2Request(scenario, 1, "ALLOWED_PHASE_BOUNDARY",
            "DetectionAcquireSeconds", 1.25, 1.5, "RELIEVE", "COMMITTED");
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, first)).IsSuccess);
        var boundary = V2Request(scenario, 2, "ALLOWED_PHASE_BOUNDARY",
            "ChaseSpeed", 7.5, 8, "INCREASE_PRESSURE", "COMMITTED");
        var result = await service.SubmitAsync(f.HostId, f.MatchId, boundary);

        Assert.Equal(ErrorCodes.ScenarioApplyConflict, result.ErrorCode);
        Assert.Single(await f.Db.ScenarioAdaptivePlansV2.ToArrayAsync());
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_AbortedPlanCannotBeApplied()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(
            f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var request = V2Request(scenario, 1, "ALLOWED_PHASE_BOUNDARY",
            "DetectionAcquireSeconds", 1.25, 1.5, "RELIEVE", "COMMITTED");
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, request)).IsSuccess);
        Assert.True((await service.AbortPendingAsync(
            f.HostId, f.MatchId, request.DecisionId)).IsSuccess);

        var result = await service.ConfirmAppliedAsync(f.HostId, f.MatchId,
            request.DecisionId, request.ResultingPlanFingerprint);

        Assert.Equal(ErrorCodes.ScenarioApplyConflict, result.ErrorCode);
        Assert.Equal("ABORTED", (await f.Db.ScenarioAdaptivePlansV2.SingleAsync()).ApplyStatus);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_AbortedPlanIsNotPreviousPlan()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(
            f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();

        var aborted = V2Request(scenario, 1, "ALLOWED_PHASE_BOUNDARY",
            "DetectionAcquireSeconds", 1.25, 1.5, "RELIEVE", "COMMITTED");
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, aborted)).IsSuccess);
        Assert.True((await service.AbortPendingAsync(f.HostId, f.MatchId, aborted.DecisionId)).IsSuccess);
        var boundary = V2Request(scenario, 2, "ALLOWED_PHASE_BOUNDARY",
            "DetectionAcquireSeconds", 1.25, 1.5, "RELIEVE", "COMMITTED");
        var result = await service.SubmitAsync(f.HostId, f.MatchId, boundary);

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.Message}");
        Assert.Equal(ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(
            ScenarioAdaptivePlanV2Service.NormalPlanValues()),
            result.Data!.PreviousPlanFingerprint);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_AbortReplayIsIdempotent()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(
            f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var request = V2Request(scenario, 1, "ALLOWED_PHASE_BOUNDARY",
            "DetectionAcquireSeconds", 1.25, 1.5, "RELIEVE", "COMMITTED");
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, request)).IsSuccess);

        var aborted = await service.AbortPendingAsync(f.HostId, f.MatchId, request.DecisionId);
        var replay = await service.AbortPendingAsync(f.HostId, f.MatchId, request.DecisionId);

        Assert.True(aborted.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Data!.IsReplay);
        Assert.Equal("ABORTED", replay.Data.ApplyStatus);
        Assert.Single(await f.Db.ScenarioAdaptivePlansV2.ToArrayAsync());
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_AppliedPlanCannotBeAborted()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(
            f.HostId, f.MatchId, AdaptiveRequest())).Data!;
        var service = V2(f);
        f.Db.MatchAuthorityBindings.Single().Status = MatchAuthorityStatus.InMatch;
        await f.Db.SaveChangesAsync();
        var request = V2Request(scenario, 1, "ALLOWED_PHASE_BOUNDARY",
            "DetectionAcquireSeconds", 1.25, 1.5, "RELIEVE", "COMMITTED");
        Assert.True((await service.SubmitAsync(f.HostId, f.MatchId, request)).IsSuccess);
        Assert.True((await service.ConfirmAppliedAsync(f.HostId, f.MatchId,
            request.DecisionId, request.ResultingPlanFingerprint)).IsSuccess);

        var result = await service.AbortPendingAsync(f.HostId, f.MatchId, request.DecisionId);

        Assert.Equal(ErrorCodes.ScenarioApplyConflict, result.ErrorCode);
        Assert.Equal("APPLIED", (await f.Db.ScenarioAdaptivePlansV2.SingleAsync()).ApplyStatus);
    }

    [Fact, Trait("Category", "AEDv2")]
    public async Task AEDv2_FixedDecisionCannotAuthorizePlan()
    {
        await using var f = await ValidAEDv2Fixture();
        var scenario = (await f.Service.ResolvePreMatchAsync(f.HostId, f.MatchId, Request())).Data!;
        var request = V2Request(scenario, 0, "PRE_MATCH", "SupportBonus", 0, 2, "RELIEVE", "SHADOW_ONLY");
        Assert.Equal(ErrorCodes.ScenarioDecisionStaleRoster,
            (await V2(f).SubmitAsync(f.HostId, f.MatchId, request)).ErrorCode);
        Assert.Empty(await f.Db.ScenarioAdaptivePlansV2.ToArrayAsync());
    }

    private static async Task<Fixture> ValidAEDv2Fixture()
    {
        var f = await Fixture.CreateAsync();
        var player = f.Db.PlayerAIProfiles.Single(x => x.UserId == f.PlayerId);
        player.SurvivalStatus = ProfileDimensionStatus.Active;
        player.NoiseStatus = ProfileDimensionStatus.Active;
        player.SurvivalSampleCount = 2;
        player.NoiseSampleCount = 2;
        await f.Db.SaveChangesAsync();
        return f;
    }

    private static ScenarioAdaptivePlanV2Service V2(Fixture f) =>
        new(f.Db, new ScenarioSnapshotReadService(f.Db, new FixedClock(Now)), new FixedClock(Now), new AcceptEvidenceVerifier());

    private sealed class AcceptEvidenceVerifier : IAEDv2PhaseEvidenceVerifier
    {
        public Task<bool> VerifyAsync(Guid matchId, int phaseOrdinal, string evidenceFingerprint,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NoAcceptedEvidenceRepository : ITelemetryEventRepository
    {
        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TelemetryWriteResult> AtomicCommitBatchAsync(IReadOnlyCollection<TelemetryEventDocument> events, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TelemetryWriteResult> InsertBatchAsync(IReadOnlyCollection<TelemetryEventDocument> events, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, TelemetryWriteItemResult>> LoadConflictsAsync(IReadOnlyCollection<TelemetryEventDocument> events, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, TelemetryMatchBoundary>> LoadMatchBoundariesAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<TelemetryEventDocument>> LoadAcceptedMatchEventsAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TelemetryEventDocument>>(Array.Empty<TelemetryEventDocument>());
    }

    private static ResolveScenarioRequest AdaptiveRequest()
    {
        var request = Request();
        request.ResolutionMode = ScenarioResolutionMode.Adaptive;
        return request;
    }

    private static SubmitScenarioAdaptivePlanV2Request V2Request(
        ScenarioDecisionResponse scenario, int ordinal, string point, string key,
        double before, double after, string intent, string status, double[]? previous = null)
    {
        var old = previous ?? ScenarioAdaptivePlanV2Service.NormalPlanValues();
        var values = (double[])old.Clone();
        var index = key switch
        {
            "SupportBonus" => 0,
            "DetectionAcquireSeconds" => 2,
            "ChaseSpeed" => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
        values[index] = after;
        return new SubmitScenarioAdaptivePlanV2Request
        {
            DecisionId = Guid.NewGuid(), PhaseOrdinal = ordinal, DecisionPoint = point,
            PolicyVersion = ScenarioAdaptivePlanV2Service.PolicyVersion,
            BaselineVersion = ScenarioAdaptivePlanV2Service.BaselineVersion,
            PreviousPlanFingerprint = ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(old),
            ResultingPlanFingerprint = ScenarioAdaptivePlanV2Service.ComputePlanFingerprint(values),
            ChangedKey = key, PreviousValue = before, AppliedValue = after,
            AdaptationIntent = intent, DecisionReason = "TEST_EVIDENCE",
            SnapshotId = scenario.SnapshotId, SnapshotFingerprint = scenario.SnapshotContentFingerprint,
            RosterIdentity = scenario.RosterIdentity,
            EvidenceFingerprint = ordinal == 0 ? string.Empty : new string('a', 64),
            CommitStatus = status, PlanValues = values
        };
    }
}
