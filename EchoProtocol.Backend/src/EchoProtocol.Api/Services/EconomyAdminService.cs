using System.ComponentModel.DataAnnotations;
using System.Data;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using EchoProtocol.Api.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EchoProtocol.Api.Services;

public sealed record ProposeShopPrice(Guid ItemId, int ExpectedPrice,
    [Range(0, 1000000)] int Price, [Required, StringLength(500, MinimumLength = 10)] string Reason,
    [Range(1, 30)] int? RequiredLevel = null, int? ExpectedRequiredLevel = null);

public sealed record ReviewShopPrice([Required] bool? Approve, [Required, StringLength(500, MinimumLength = 10)] string Reason);

public sealed partial class EconomyAdminService(AppDbContext db, TimeProvider clock)
{
    public async Task<ServiceResult<object>> GetAsync(Guid admin, CancellationToken ct)
    {
        var items = await db.ShopItems.AsNoTracking().OrderBy(x => x.ItemName).ThenBy(x => x.ItemId)
            .Select(x => new { x.ItemId, x.ItemName, x.Category, x.Price, x.RequiredLevel, x.IsActive }).ToArrayAsync(ct);
        var proposals = await db.Set<ShopPriceProposal>().AsNoTracking().OrderBy(x => x.Status == "PENDING" ? 0 : 1)
            .ThenByDescending(x => x.ProposedAtUtc).Take(100).ToArrayAsync(ct);
        var rewards = await db.RewardConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == RewardConfiguration.SharedId, ct)
            ?? new RewardConfiguration { PolicyKey = RewardPolicyV3.PolicyVersion };
        return ServiceResult<object>.Success(new { items, proposals, Rewards = rewards,
            RewardPolicy = rewards.PolicyKey, MaximumCurrencyPerMatch = rewards.Maximum,
            RewardTelemetry = await RewardTelemetryAsync(ct), WalletTelemetry = await WalletTelemetryAsync(ct) });
    }

    private async Task<object> RewardTelemetryAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-30);
        const int limit = 2000;
        var rows = await db.MatchResults.AsNoTracking().Where(x => x.EndedAtUtc >= cutoff &&
                x.Match.MatchDataPolicy == RemoteFixedBalanceContract.Standard &&
                x.RewardStatus == MatchRewardStatus.Completed && x.DurationSeconds > 0)
            .OrderByDescending(x => x.EndedAtUtc).ThenBy(x => x.MatchId).Take(limit + 1)
            .Select(x => new { x.MatchId, x.Match.MapId, x.Match.Difficulty, x.PlayerCount,
                x.Match.RewardPolicyVersion, x.DurationSeconds, x.Outcome,
                HasDisconnect = x.Players.Any(p => p.Disconnected) }).ToListAsync(ct);
        bool truncated = rows.Count > limit;
        if (truncated) rows.RemoveAt(limit);
        var ids = rows.Select(x => x.MatchId).ToArray();
        var grants = (await db.MatchRewardGrants.AsNoTracking().Where(x => ids.Contains(x.MatchId))
            .Select(x => new { x.MatchId, x.CurrencyAmount, x.ExperiencePointsAwarded }).ToListAsync(ct))
            .ToLookup(x => x.MatchId);
        var cohorts = rows.GroupBy(x => new { x.MapId, x.Difficulty, x.PlayerCount, x.RewardPolicyVersion })
            .Select(group =>
            {
                var wins = group.Where(x => x.Outcome == MatchOutcome.WIN && !x.HasDisconnect)
                    .Select(x => x.DurationSeconds / 60m).Order().ToArray();
                decimal? median = wins.Length == 0 ? null : wins.Length % 2 == 1 ? wins[wins.Length / 2]
                    : (wins[wins.Length / 2 - 1] + wins[wins.Length / 2]) / 2m;
                decimal playerHours = group.Sum(x => (decimal)x.DurationSeconds * x.PlayerCount) / 3600m;
                return new { group.Key.MapId, group.Key.Difficulty, group.Key.PlayerCount, group.Key.RewardPolicyVersion,
                    Matches = group.Count(), EligibleWins = wins.Length, MedianWinMinutes = median,
                    SuggestedReferenceMinutes = wins.Length >= 50 ? median : null,
                    CreditsPerPlayerHour = group.Sum(x => grants[x.MatchId].Sum(g => (decimal)g.CurrencyAmount)) / playerHours,
                    XpPerPlayerHour = group.Sum(x => grants[x.MatchId].Sum(g => (decimal)g.ExperiencePointsAwarded)) / playerHours };
            }).OrderBy(x => x.MapId).ThenBy(x => x.Difficulty).ThenBy(x => x.PlayerCount).ToArray();
        return new { Days = 30, Limit = limit, Truncated = truncated, Cohorts = cohorts };
    }

    public async Task<ServiceResult<object>> SaveRewardsAsync(Guid admin, RewardConfiguration rewards, CancellationToken ct)
    {
        if (rewards.PolicyKey is not ("REWARD_V1" or "REWARD_V2" or "REWARD_V3"))
            return Fail("Invalid reward policy", ErrorCodes.ValidationError);
        if (rewards.PolicyKey == "REWARD_V2" && !rewards.HasValidV2Settings())
            return Fail("Invalid V2 reward settings", ErrorCodes.ValidationError);
        var expectedEffectiveAtUtc = rewards.EffectiveAtUtc;
        if (rewards.PolicyKey == "REWARD_V3" && !rewards.HasValidV3Settings())
            return ServiceResult<object>.Failure("Invalid V3 reward configuration", ErrorCodes.ValidationError);

        rewards.Id = RewardConfiguration.SharedId;
        rewards.UpdatedByUserId = admin;

        var current = await db.RewardConfigurations.FindAsync(new object[] { rewards.Id }, ct);
        if (current != null && current.EffectiveAtUtc != expectedEffectiveAtUtc)
            return Fail("Mức thưởng vừa được cập nhật. Hãy tải lại rồi thử lại.");
        if (rewards.PolicyKey is "REWARD_V2" or "REWARD_V3")
        {
            var legacy = await db.MatchAuthorityBindings.Where(m => m.RewardPolicyVersion == null &&
                (m.Status == MatchAuthorityStatus.InMatch || db.MatchResults.Any(r =>
                    r.MatchId == m.MatchId && r.RewardStatus == MatchRewardStatus.Pending))).ToListAsync(ct);
            if (legacy.Count > 0 && current?.PolicyKey is "REWARD_V2" or "REWARD_V3")
                return Fail("Còn trận cũ chưa có cấu hình thưởng V1 để xử lý an toàn.", ErrorCodes.Conflict);
            var previous = current ?? new RewardConfiguration { PolicyKey = "REWARD_V1", Participation = 20,
                Objective = 30, Win = 50, Survival = 20, Contribution = 10, Disconnected = 10, Maximum = 130 };
            var snapshot = System.Text.Json.JsonSerializer.Serialize(previous);
            foreach (var match in legacy)
            {
                match.RewardPolicyVersion = "REWARD_V1";
                match.ProgressionPolicyVersion = "PROGRESSION_V1";
                match.RewardConfigurationJson = snapshot;
            }
        }
        rewards.EffectiveAtUtc = clock.GetUtcNow().UtcDateTime;
        if (current == null) db.RewardConfigurations.Add(rewards);
        else db.Entry(current).CurrentValues.SetValues(rewards);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.GetBaseException() is PostgresException { SqlState: "23505" })
        {
            return Fail("Mức thưởng vừa được cập nhật. Hãy tải lại rồi thử lại.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail("Mức thưởng vừa được cập nhật. Hãy tải lại rồi thử lại.");
        }
        return ServiceResult<object>.Success(rewards);
    }

    public async Task<ServiceResult<object>> ProposeAsync(Guid admin, ProposeShopPrice request, CancellationToken ct)
    {
        if (request.Price is < 0 or > 1000000 || request.Reason.Trim().Length < 10)
            return Fail("Giá hoặc lý do không hợp lệ.", ErrorCodes.ValidationError);
        if (request.RequiredLevel is < 1 or > 30 ||
            (request.RequiredLevel.HasValue && !request.ExpectedRequiredLevel.HasValue))
            return Fail("Cấp mua không hợp lệ.", ErrorCodes.ValidationError);
        var item = await db.ShopItems.AsNoTracking().SingleOrDefaultAsync(x => x.ItemId == request.ItemId, ct);
        if (item is null || !ShopItemCategories.IsSupported(item.Category))
            return Fail("Mặt hàng không có giá được quản trị.", ErrorCodes.NotFound);
        if (request.ExpectedPrice != item.Price ||
            (request.RequiredLevel.HasValue && request.ExpectedRequiredLevel != item.RequiredLevel))
            return Fail("Mặt hàng đã thay đổi. Hãy tải lại.");
        if (request.Price == item.Price && (request.RequiredLevel ?? item.RequiredLevel) == item.RequiredLevel)
            return Fail("Không có thay đổi giá hoặc cấp mua.");
        if (item.Category == ShopItemCategories.TeamTool && request.RequiredLevel is > 1 &&
            item.ItemName is not ("Core Stabilizer" or "Field Scanner"))
            return Fail("Công cụ đội thiết yếu phải mở từ cấp 1.", ErrorCodes.ValidationError);
        var proposal = new ShopPriceProposal
        {
            Id = Guid.NewGuid(), ItemId = item.ItemId, PreviousPrice = item.Price, ProposedPrice = request.Price,
            ItemUpdatedAtUtc = item.UpdatedAtUtc, ProposedByUserId = admin, ProposedAtUtc = clock.GetUtcNow().UtcDateTime,
            Reason = request.Reason.Trim()
        };
        proposal.PreviousRequiredLevel = request.RequiredLevel.HasValue ? item.RequiredLevel : null;
        proposal.ProposedRequiredLevel = request.RequiredLevel;
        db.Add(proposal);
        await db.SaveChangesAsync(ct);
        return ServiceResult<object>.Success(proposal);
    }

    public async Task<ServiceResult<object>> ApplyPriceAsync(Guid admin, ProposeShopPrice request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var audit = await ProposeAsync(admin, request, ct);
        if (!audit.IsSuccess) return audit;
        var change = (ShopPriceProposal)audit.Data!;
        var now = clock.GetUtcNow().UtcDateTime;
        var count = await db.ShopItems.Where(item => item.ItemId == change.ItemId &&
            item.Price == change.PreviousPrice && item.UpdatedAtUtc == change.ItemUpdatedAtUtc &&
            (change.PreviousRequiredLevel == null || item.RequiredLevel == change.PreviousRequiredLevel))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Price, change.ProposedPrice)
                .SetProperty(item => item.RequiredLevel, item => change.ProposedRequiredLevel ?? item.RequiredLevel)
                .SetProperty(item => item.UpdatedAtUtc, now), ct);
        if (count != 1) return Fail("Giá hiện hành đã thay đổi. Hãy tải lại bảng giá.");
        change.Status = "APPROVED";
        change.ReviewedByUserId = admin;
        change.ReviewedAtUtc = now;
        change.ReviewReason = "Áp dụng trực tiếp bởi quản trị viên";
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return audit;
    }

    public async Task<ServiceResult<object>> ReviewAsync(Guid admin, Guid id, ReviewShopPrice request, CancellationToken ct)
    {
        if (request.Approve is null || request.Reason.Trim().Length < 10)
            return Fail("Cần quyết định duyệt và lý do từ 10 ký tự.", ErrorCodes.ValidationError);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var proposal = await db.Set<ShopPriceProposal>().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (proposal is null) return Fail("Không tìm thấy đề xuất.", ErrorCodes.NotFound);
            if (proposal.Status != "PENDING") return Fail("Đề xuất đã được xử lý.");
            var now = clock.GetUtcNow().UtcDateTime;
            if (request.Approve.Value)
            {
                var applied = await db.ShopItems.Where(x => x.ItemId == proposal.ItemId && x.Price == proposal.PreviousPrice &&
                    x.UpdatedAtUtc == proposal.ItemUpdatedAtUtc &&
                    (proposal.PreviousRequiredLevel == null || x.RequiredLevel == proposal.PreviousRequiredLevel))
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.Price, proposal.ProposedPrice)
                        .SetProperty(x => x.RequiredLevel, x => proposal.ProposedRequiredLevel ?? x.RequiredLevel)
                        .SetProperty(x => x.UpdatedAtUtc, now), ct);
                if (applied != 1) return Fail("Mặt hàng đã thay đổi. Hãy tạo đề xuất mới.");
            }
            proposal.Status = request.Approve.Value ? "APPROVED" : "REJECTED";
            proposal.ReviewedByUserId = admin;
            proposal.ReviewedAtUtc = now;
            proposal.ReviewReason = request.Reason.Trim();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return ServiceResult<object>.Success(proposal);
        }
        catch (Exception e) when (e.GetBaseException() is PostgresException { SqlState: "40001" or "40P01" })
        { return Fail("Đề xuất hoặc giá đã thay đổi trong lúc duyệt."); }
    }

    private static ServiceResult<object> Fail(string message, string code = ErrorCodes.Conflict) =>
        ServiceResult<object>.Failure(message, code);
}
