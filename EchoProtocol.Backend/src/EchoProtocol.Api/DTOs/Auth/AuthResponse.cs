namespace EchoProtocol.Api.DTOs.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshExpiresAt { get; set; }

    public UserSummaryResponse User { get; set; } = null!;
    public WalletSummaryResponse Wallet { get; set; } = null!;
}
