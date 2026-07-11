namespace EchoProtocol.Api.DTOs.Auth;

public class UserSummaryResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
