using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.DTOs.Scenarios;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using EchoProtocol.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class AEDDirectorApprovalV1Service(AppDbContext db, IAEDv2PhaseEvidenceVerifier canonicalVerifier, TimeProvider clock) : IAEDDirectorApprovalV1Service
{
    public async Task<ServiceResult<AEDDirectorResearchResponseV1>> SubmitResearchAsync(Guid callerId, Guid matchId, AEDDirectorResearchRequestV1 request, CancellationToken ct = default)
    {
        if (callerId == Guid.Empty || matchId == Guid.Empty || request == null || request.DecisionId == Guid.Empty || request.CompletedPhaseOrdinal < 1 || request.ExpectedRevision < 0 || !IsHash(request.RosterIdentity) || !IsHash(request.SnapshotFingerprint) || !IsHash(request.EvidenceFingerprint) || !IsHash(request.SourceFingerprint) || string.IsNullOrWhiteSpace(request.PayloadJson)) return Fail("Invalid director request", ErrorCodes.ValidationError);
        var binding = await db.MatchAuthorityBindings.SingleOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (binding == null) return Fail("Match not found", ErrorCodes.MatchNotFound);
        if (binding.HostUserId != callerId) return Fail("Only the match host may submit", ErrorCodes.MatchAuthorityForbidden);
        if (binding.LeaseExpiresAtUtc <= clock.GetUtcNow().UtcDateTime) return Fail("Match lease expired", ErrorCodes.MatchLeaseExpired);
        if (binding.Status != MatchAuthorityStatus.InMatch) return Fail("Match is not active", ErrorCodes.ScenarioDecisionConflict);
        var scenario = await db.ScenarioDecisions.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId && x.IsCurrent, ct);
        if (scenario == null || scenario.ResolutionMode != ScenarioResolutionMode.Adaptive || scenario.UsedFixedFallback) return Fail("Adaptive Normal scenario required", ErrorCodes.ScenarioDecisionConflict);
        var snapshot = await db.AdaptiveInputSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.SnapshotId == scenario.SnapshotId && x.MatchId == matchId, ct);
        if (snapshot == null || snapshot.RosterIdentity != request.RosterIdentity || snapshot.SnapshotContentFingerprint != request.SnapshotFingerprint) return Fail("Snapshot or roster is stale", ErrorCodes.ScenarioDecisionStaleRoster);
        if (!await canonicalVerifier.VerifyAsync(matchId, request.CompletedPhaseOrdinal, request.EvidenceFingerprint, ct)) return Fail("Canonical evidence is unverified", ErrorCodes.ScenarioApplyConflict);
        var source = await db.AEDPhase8VerifiedSourcesV1.AsNoTracking().Where(x => x.MatchId == matchId && x.CompletedPhaseOrdinal == request.CompletedPhaseOrdinal).OrderByDescending(x => x.VerifiedAtUtc).FirstOrDefaultAsync(ct);
        if (source == null || source.EvidenceFingerprint != request.EvidenceFingerprint || source.SourceFingerprint != request.SourceFingerprint || source.RosterIdentity != request.RosterIdentity) return Fail("Verified source evidence is missing or stale", ErrorCodes.ScenarioApplyConflict);
        var currentRevision = await db.ScenarioAdaptivePlansV2.CountAsync(x => x.MatchId == matchId && x.ApplyStatus == "APPLIED", ct);
        if (request.ExpectedRevision != currentRevision) return Fail("Director revision is stale", ErrorCodes.ScenarioDecisionConflict);
        if (!TryCanonicalPayload(request.PayloadJson, out var canonicalPayload, out var calculatedFingerprint) || !string.Equals(calculatedFingerprint, request.PayloadFingerprint, StringComparison.OrdinalIgnoreCase)) return Fail("Payload fingerprint mismatch", ErrorCodes.ValidationError);
        if (!SupportedDirector(request.DirectorKind) || !string.Equals(request.PolicyVersion, "AED_PHASE8_RESEARCH_V1", StringComparison.Ordinal)) return Fail("Unsupported director policy", ErrorCodes.ValidationError);
        var existing = await db.AEDDirectorDecisionsV1.SingleOrDefaultAsync(x => x.DecisionId == request.DecisionId, ct);
        if (existing != null) return SameSemantics(existing, request, matchId) ? ServiceResult<AEDDirectorResearchResponseV1>.Success(Map(existing, true)) : Fail("Decision replay conflicts", ErrorCodes.ScenarioDecisionConflict);
        var slot = await db.AEDDirectorDecisionsV1.AnyAsync(x => x.MatchId == matchId && x.CompletedPhaseOrdinal == request.CompletedPhaseOrdinal && x.DirectorKind == request.DirectorKind, ct);
        if (slot) return Fail("Director phase already has a decision", ErrorCodes.ScenarioDecisionConflict);
        var gate = AEDPhase8ApprovalGateV1.Evaluate(new AEDPhase8ApprovalEvidenceV1(source.CanonicalVerified, source.HistoricalSkillVerified, source.PressureVerified, source.SupplyLedgerVerified, source.MinionPopulationVerified, true, true, true, false, true, true), request.DirectorKind);
        if (!gate.ApprovedForResearch) return Fail(gate.ReasonCode, ErrorCodes.ScenarioApplyConflict);
        return Fail("Director payload policy validator is not available", ErrorCodes.ScenarioApplyConflict);
    }

    public async Task<ServiceResult<AEDDirectorResearchResponseV1>> GetDecisionAsync(Guid callerId, Guid matchId, Guid decisionId, CancellationToken ct = default)
    {
        var entity = await db.AEDDirectorDecisionsV1.AsNoTracking().SingleOrDefaultAsync(x => x.MatchId == matchId && x.DecisionId == decisionId, ct);
        if (entity == null) return Fail("Decision not found", ErrorCodes.ScenarioDecisionNotFound);
        if (entity.HostUserId != callerId) return Fail("Only the match host may read", ErrorCodes.MatchAuthorityForbidden);
        return ServiceResult<AEDDirectorResearchResponseV1>.Success(Map(entity, false));
    }

    public async Task<ServiceResult<AEDDirectorResearchResponseV1>> AbortResearchDecisionAsync(Guid callerId, Guid matchId, Guid decisionId, CancellationToken ct = default)
    {
        var entity = await db.AEDDirectorDecisionsV1.SingleOrDefaultAsync(x => x.MatchId == matchId && x.DecisionId == decisionId, ct);
        if (entity == null) return Fail("Decision not found", ErrorCodes.ScenarioDecisionNotFound);
        if (entity.HostUserId != callerId) return Fail("Only the match host may abort", ErrorCodes.MatchAuthorityForbidden);
        if (entity.Status == "ABORTED") return ServiceResult<AEDDirectorResearchResponseV1>.Success(Map(entity, true));
        if (entity.Status != "APPROVED_RESEARCH" || entity.AppliedAtUtc.HasValue) return Fail("Only research approvals can be aborted", ErrorCodes.ScenarioApplyConflict);
        entity.Status = "ABORTED"; entity.AbortedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return ServiceResult<AEDDirectorResearchResponseV1>.Success(Map(entity, false));
    }

    private static bool TryCanonicalPayload(string payload, out string canonical, out string fingerprint)
    {
        canonical = string.Empty; fingerprint = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            canonical = Canonicalize(document.RootElement);
            fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static string Canonicalize(JsonElement element) => element.ValueKind == JsonValueKind.Object
        ? "{" + string.Join(',', element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => JsonSerializer.Serialize(x.Name) + ":" + Canonicalize(x.Value))) + "}"
        : element.ValueKind == JsonValueKind.Array ? "[" + string.Join(',', element.EnumerateArray().Select(Canonicalize)) + "]" : element.GetRawText();
    private static bool IsHash(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);
    private static bool SupportedDirector(string kind) => kind is "TEAM_TOOL" or "MINION" or "STALKER";
    private static bool SameSemantics(AEDDirectorDecisionV1 entity, AEDDirectorResearchRequestV1 request, Guid matchId) => entity.MatchId == matchId && entity.CompletedPhaseOrdinal == request.CompletedPhaseOrdinal && entity.DirectorKind == request.DirectorKind && entity.PolicyVersion == request.PolicyVersion && entity.RosterIdentity == request.RosterIdentity && entity.SnapshotFingerprint == request.SnapshotFingerprint && entity.EvidenceFingerprint == request.EvidenceFingerprint && entity.SourceFingerprint == request.SourceFingerprint && entity.PayloadFingerprint == request.PayloadFingerprint && entity.ExpectedRevision == request.ExpectedRevision;
    private static AEDDirectorResearchResponseV1 Map(AEDDirectorDecisionV1 x, bool replay) => new(x.MatchId, x.DecisionId, x.CompletedPhaseOrdinal, x.DirectorKind, x.PolicyVersion, x.RosterIdentity, x.SnapshotFingerprint, x.EvidenceFingerprint, x.SourceFingerprint, x.PayloadFingerprint, x.ExpectedRevision, x.ApprovalScope, x.Status, x.ReasonCode, replay, false);
    private static ServiceResult<AEDDirectorResearchResponseV1> Fail(string message, string code) => ServiceResult<AEDDirectorResearchResponseV1>.Failure(message, code);
}
