namespace EchoProtocol.Api.DTOs.Auth;

public class MeResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int WalletBalance { get; set; }
}
