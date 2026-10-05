namespace EchoProtocol.Api.Entities;

public class RefreshSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public Guid? ReplacedBySessionId { get; set; }

    public Guid ConcurrencyToken { get; set; }

    public User User { get; set; } = null!;
}
