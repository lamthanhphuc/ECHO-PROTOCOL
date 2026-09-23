using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Shop;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IPurchaseService
{
    Task<ServiceResult<PurchaseResponse>> PurchaseAsync(
        Guid userId,
        PurchaseRequest request,
        CancellationToken cancellationToken = default);
}
