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
    [Range(0, 1000000)] int Price, [Required, StringLength(500, MinimumLength = 10)] string Reason);

public sealed record ReviewShopPrice([Required] bool? Approve, [Required, StringLength(500, MinimumLength = 10)] string Reason);

public sealed class EconomyAdminService(AppDbContext db, TimeProvider clock)
{
    public async Task<ServiceResult<object>> GetAsync(Guid admin, CancellationToken ct)
    {
        var items = await db.ShopItems.AsNoTracking().OrderBy(x => x.ItemName).ThenBy(x => x.ItemId)
            .Select(x => new { x.ItemId, x.ItemName, x.Category, x.Price, x.IsActive }).ToArrayAsync(ct);
        var proposals = await db.Set<ShopPriceProposal>().AsNoTracking().OrderBy(x => x.Status == "PENDING" ? 0 : 1)
            .ThenByDescending(x => x.ProposedAtUtc).Take(100).ToArrayAsync(ct);
        var rewards = await db.RewardConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == RewardConfiguration.SharedId, ct) ?? new();
        return ServiceResult<object>.Success(new { items, proposals, Rewards = rewards,
            RewardPolicy = rewards.PolicyKey, MaximumCurrencyPerMatch = rewards.Maximum });
    }

    public async Task<ServiceResult<object>> SaveRewardsAsync(Guid admin, RewardConfiguration rewards, CancellationToken ct)
    {
        if (rewards.PolicyKey is not ("REWARD_V1" or "REWARD_V2"))
            return Fail("Invalid reward policy", ErrorCodes.ValidationError);
        if (rewards.PolicyKey == "REWARD_V2" && !rewards.HasValidV2Settings())
            return Fail("Invalid V2 reward settings", ErrorCodes.ValidationError);
        var expectedEffectiveAtUtc = rewards.EffectiveAtUtc;
        rewards.Id = RewardConfiguration.SharedId;
        rewards.UpdatedByUserId = admin;

        var current = await db.RewardConfigurations.FindAsync(new object[] { rewards.Id }, ct);
        if (current != null && current.EffectiveAtUtc != expectedEffectiveAtUtc)
            return Fail("Mức thưởng vừa được cập nhật. Hãy tải lại rồi thử lại.");
        if (rewards.PolicyKey == "REWARD_V2")
        {
            var legacy = await db.MatchAuthorityBindings.Where(m => m.RewardPolicyVersion == null &&
                (m.Status == MatchAuthorityStatus.InMatch || db.MatchResults.Any(r =>
                    r.MatchId == m.MatchId && r.RewardStatus == MatchRewardStatus.Pending))).ToListAsync(ct);
            if (legacy.Count > 0 && current?.PolicyKey == "REWARD_V2")
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
        var item = await db.ShopItems.AsNoTracking().SingleOrDefaultAsync(x => x.ItemId == request.ItemId, ct);
        if (item is null || !ShopItemCategories.IsSupported(item.Category))
            return Fail("Mặt hàng không có giá được quản trị.", ErrorCodes.NotFound);
        if (request.ExpectedPrice != item.Price || request.Price == item.Price)
            return Fail("Giá hiện hành đã thay đổi hoặc giá mới trùng giá hiện hành.");
        var proposal = new ShopPriceProposal
        {
            Id = Guid.NewGuid(), ItemId = item.ItemId, PreviousPrice = item.Price, ProposedPrice = request.Price,
            ItemUpdatedAtUtc = item.UpdatedAtUtc, ProposedByUserId = admin, ProposedAtUtc = clock.GetUtcNow().UtcDateTime,
            Reason = request.Reason.Trim()
        };
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
            item.Price == change.PreviousPrice && item.UpdatedAtUtc == change.ItemUpdatedAtUtc)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Price, change.ProposedPrice)
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
                    x.UpdatedAtUtc == proposal.ItemUpdatedAtUtc)
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.Price, proposal.ProposedPrice)
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
