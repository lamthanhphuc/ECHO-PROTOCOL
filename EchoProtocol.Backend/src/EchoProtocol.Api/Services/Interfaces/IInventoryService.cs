using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Inventory;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IInventoryService
{
    Task<ServiceResult<InventoryResponse>> GetCurrentAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
