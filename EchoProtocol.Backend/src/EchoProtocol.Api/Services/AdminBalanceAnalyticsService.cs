using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace EchoProtocol.Api.Services;

public sealed class AdminBalanceAnalyticsService(AppDbContext db)
{
    public async Task<ServiceResult<object>> GetAsync(int days, string? map, string? difficulty,
        int? teamSize, string? baseline, bool testOnly, CancellationToken ct)
    {
        if (days is < 1 or > 90 || teamSize is < 1 or > 4)
            return ServiceResult<object>.Failure("Khoảng ngày hoặc quy mô đội không hợp lệ.", ErrorCodes.ValidationError);

        var cutoff = DateTime.UtcNow.AddDays(-days);
        var policy = testOnly ? RemoteFixedBalanceContract.TestOnly : RemoteFixedBalanceContract.Standard;
        var query = db.MatchResults.AsNoTracking().Where(x => x.EndedAtUtc >= cutoff && x.Match.MatchDataPolicy == policy);
        if (!string.IsNullOrWhiteSpace(map)) query = query.Where(x => x.Match.MapId == map);
        if (!string.IsNullOrWhiteSpace(difficulty)) query = query.Where(x => x.Match.Difficulty == difficulty);
        if (teamSize.HasValue) query = query.Where(x => x.PlayerCount == teamSize);
        if (baseline == "LOCAL") query = query.Where(x => x.Match.BalanceSource == "LOCAL");
        else if (!string.IsNullOrWhiteSpace(baseline)) query = query.Where(x => x.Match.BalanceVersion == baseline);

        const int limit = 1000;
        var matches = await query.OrderByDescending(x => x.EndedAtUtc).ThenBy(x => x.MatchId).Take(limit + 1)
            .Select(x => new
            {
                x.MatchId, x.Outcome, x.DurationSeconds, x.Match.ResolutionMode,
                Players = x.Players.Count, Survivors = x.Players.Count(p => p.Survived)
            }).ToListAsync(ct);
        var truncated = matches.Count > limit;
        if (truncated) matches.RemoveAt(limit);
        var ids = matches.Select(x => x.MatchId).ToArray();
        var scenarios = (await db.ScenarioDecisions.AsNoTracking().Where(x => ids.Contains(x.MatchId) && x.IsCurrent)
            .Select(x => new { x.MatchId, x.ResolutionMode, x.UsedFixedFallback, x.FallbackReasonCode, Applied = x.ApplyReceipt != null })
            .ToListAsync(ct)).ToLookup(x => x.MatchId);
        var revisions = await db.AEDV02Decisions.AsNoTracking().Where(x => ids.Contains(x.MatchId))
            .Select(x => new
            {
                x.MatchId, x.ApprovalStatus, x.ApplyStatus, x.ReasonCode, x.ReceiptId, x.ChangedKey,
                ClientObserved = db.Set<Entities.AEDV02ClientObservation>().Any(c => c.DecisionId == x.DecisionId)
            }).ToListAsync(ct);
        var byMatch = revisions.ToLookup(x => x.MatchId);
        var legacyApplied = (await db.AEDDirectorDecisionsV1.AsNoTracking()
            .Where(x => ids.Contains(x.MatchId) && x.AppliedAtUtc != null && x.AbortedAtUtc == null)
            .Select(x => x.MatchId).Distinct().ToListAsync(ct)).ToHashSet();

        var cohorts = matches.GroupBy(match =>
        {
            var decisions = scenarios[match.MatchId];
            var adaptive = decisions.Where(x => x.ResolutionMode == ScenarioResolutionMode.Adaptive).ToArray();
            if (byMatch[match.MatchId].Any(x => x.ApplyStatus == "APPLIED" && x.ReceiptId != null && x.ChangedKey != "") ||
                adaptive.Any(x => x.Applied && !x.UsedFixedFallback) || (adaptive.Length > 0 && legacyApplied.Contains(match.MatchId)))
                return "ADAPTIVE_APPLIED";
            if (adaptive.Any(x => x.UsedFixedFallback) || byMatch[match.MatchId].Any(x => x.ReasonCode.StartsWith("HOLD_")))
                return "ADAPTIVE_FALLBACK";
            if (match.ResolutionMode == "ADAPTIVE" || adaptive.Length > 0 || byMatch.Contains(match.MatchId))
                return "ADAPTIVE_UNVERIFIED";
            return match.ResolutionMode == "FIXED" || decisions.Any(x => x.ResolutionMode == ScenarioResolutionMode.Fixed)
                ? "FIXED" : "UNKNOWN";
        }).Select(group => new
        {
            Group = group.Key, Matches = group.Count(), Wins = group.Count(x => x.Outcome == MatchOutcome.WIN),
            AverageSeconds = group.Average(x => x.DurationSeconds),
            Players = group.Sum(x => x.Players), Survivors = group.Sum(x => x.Survivors),
            RecentMatchIds = group.Take(5).Select(x => x.MatchId)
        });
        var approved = revisions.Where(x => x.ApprovalStatus == "APPROVED").ToArray();
        var applied = approved.Where(x => x.ApplyStatus == "APPLIED" && x.ReceiptId != null).ToArray();
        var reasons = revisions.Where(x => x.ReasonCode.StartsWith("HOLD_")).Select(x => (x.MatchId, Reason: x.ReasonCode))
            .Concat(scenarios.SelectMany(group => group.Where(x => x.UsedFixedFallback && x.FallbackReasonCode != null)
                .Select(x => (x.MatchId, Reason: x.FallbackReasonCode!))))
            .Distinct().GroupBy(x => x.Reason).Select(group => new { Reason = group.Key, Matches = group.Count() })
            .OrderByDescending(x => x.Matches).ThenBy(x => x.Reason);
        return ServiceResult<object>.Success(new
        {
            Total = matches.Count, truncated, limit, cohorts, reasons,
            Approved = approved.Length, Applied = applied.Length,
            ClientObserved = applied.Count(x => x.ClientObserved),
            FailedEffect = approved.Count(x => x.ApplyStatus == "FAILED_EFFECT")
        });
    }

    public async Task<ServiceResult<object>> PlayerAsync(Guid userId, int page, CancellationToken ct)
    {
        if (page is < 1 or > 10000)
            return ServiceResult<object>.Failure("Trang lịch sử không hợp lệ.", ErrorCodes.ValidationError);
        const int size = 30;
        var profile = await db.PlayerAIProfiles.AsNoTracking().Where(x => x.UserId == userId).Select(x => new
        {
            x.ProfileLineageId, x.ProfileRevision, x.ProfileFormulaVersion,
            x.SurvivalScore, SurvivalStatus = x.SurvivalStatus.ToString(), x.SurvivalSampleCount,
            x.NoiseScore, NoiseStatus = x.NoiseStatus.ToString(), x.NoiseSampleCount,
            x.ObjectiveScore, ObjectiveStatus = x.ObjectiveStatus.ToString(), x.ObjectiveSampleCount,
            x.ToolUsageScore, ToolUsageStatus = x.ToolUsageStatus.ToString(), x.ToolUsageSampleCount
        }).SingleOrDefaultAsync(ct);
        var query = db.MatchScores.AsNoTracking().Where(x => x.UserId == userId);
        var rows = await query.OrderByDescending(x => x.MatchEndTs).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size + 1).Select(x => new
            {
                x.Id, x.MatchId, x.MatchEndTs, x.Score, Dimension = x.Dimension.ToString(), x.ProfileLineageId,
                x.MatchScoreFormulaVersion, x.NormalizationConfigVersion, x.AlphaConfigVersion,
                Status = x.ContributionStatus.ToString(), x.RetractionReason
            }).ToListAsync(ct);
        return ServiceResult<object>.Success(new { profile, Rows = rows.Take(size), HasNext = rows.Count > size });
    }
}
