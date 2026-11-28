using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Shop;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IPurchaseService
{
    Task<ServiceResult<PurchaseHistoryResponse>> GetOwnedHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PurchaseResponse>> PurchaseAsync(
        Guid userId,
        PurchaseRequest request,
        CancellationToken cancellationToken = default);
}
