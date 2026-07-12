using EchoProtocol.Api.Entities;

namespace EchoProtocol.Api.Services.Interfaces;

public interface IJwtTokenService
{
    (string AccessToken, DateTime ExpiresAtUtc) GenerateToken(User user);
}
