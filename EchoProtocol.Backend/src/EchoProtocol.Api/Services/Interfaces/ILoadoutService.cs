using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Inventory;

namespace EchoProtocol.Api.Services.Interfaces;

public interface ILoadoutService
{
    Task<ServiceResult<PlayerLoadoutResponse>> GetCurrentAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LoadoutItemResponse>> EquipAsync(
        Guid userId,
        EquipLoadoutRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UnequipLoadoutResponse>> UnequipAsync(
        Guid userId,
        UnequipLoadoutRequest request,
        CancellationToken cancellationToken = default);
}
