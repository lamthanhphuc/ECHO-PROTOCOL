using EchoProtocol.Api.Common;
using EchoProtocol.Api.DTOs.Auth;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IAuthService
{
    Task<ServiceResult<UserSummaryResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeResponse>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
