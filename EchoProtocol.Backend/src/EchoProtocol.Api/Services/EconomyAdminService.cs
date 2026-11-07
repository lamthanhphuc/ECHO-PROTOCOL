using System.ComponentModel.DataAnnotations;
using System.Data;
using EchoProtocol.Api.Common;
using EchoProtocol.Api.Data;
using EchoProtocol.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EchoProtocol.Api.Services;

public sealed record ProposeShopPrice(Guid ItemId, int ExpectedPrice,
    [Range(0, 1000000)] int Price, [Required, StringLength(500, MinimumLength = 10)] string Reason);

public sealed record ReviewShopPrice([Required] bool? Approve, [Required, StringLength(500, MinimumLength = 10)] string Reason);

public sealed class EconomyAdminService(AppDbContext db, IConfiguration config, TimeProvider clock)
{
    private bool CanReview(Guid admin) => config.GetSection("EconomyAdmin:PriceApproverUserIds")
        .Get<string[]>()?.Any(x => Guid.TryParse(x, out var id) && id == admin) == true;

    public async Task<ServiceResult<object>> GetAsync(Guid admin, CancellationToken ct)
    {
        var items = await db.ShopItems.AsNoTracking().OrderBy(x => x.ItemName).ThenBy(x => x.ItemId)
            .Select(x => new { x.ItemId, x.ItemName, x.Category, x.Price, x.IsActive }).ToArrayAsync(ct);
        var proposals = await db.Set<ShopPriceProposal>().AsNoTracking().OrderBy(x => x.Status == "PENDING" ? 0 : 1)
            .ThenByDescending(x => x.ProposedAtUtc).Take(100).ToArrayAsync(ct);
        return ServiceResult<object>.Success(new { items, proposals, CanReview = CanReview(admin), AdminId = admin,
            RewardPolicy = RewardPolicyV1.PolicyVersion, RewardPolicyV1.MaximumCurrencyPerMatch });
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

    public async Task<ServiceResult<object>> ReviewAsync(Guid admin, Guid id, ReviewShopPrice request, CancellationToken ct)
    {
        if (!CanReview(admin)) return Fail("Tài khoản thiếu quyền duyệt giá.", "FORBIDDEN");
        if (request.Approve is null || request.Reason.Trim().Length < 10)
            return Fail("Cần quyết định duyệt và lý do từ 10 ký tự.", ErrorCodes.ValidationError);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var proposal = await db.Set<ShopPriceProposal>().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (proposal is null) return Fail("Không tìm thấy đề xuất.", ErrorCodes.NotFound);
            if (proposal.ProposedByUserId == admin) return Fail("Người đề xuất không thể tự duyệt.", "FORBIDDEN");
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
