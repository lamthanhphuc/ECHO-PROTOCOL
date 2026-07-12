namespace EchoProtocol.Api.Configurations;

public class AdminSeedSettings
{
    public const string SectionName = "AdminSeed";

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int InitialWalletBalance { get; set; }
}
