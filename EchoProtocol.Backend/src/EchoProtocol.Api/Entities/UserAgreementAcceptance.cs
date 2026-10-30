namespace EchoProtocol.Api.Entities;

public sealed class UserAgreementAcceptance
{
    public Guid UserId { get; set; }
    public string BundleVersion { get; set; } = "";
    public string ContentSha256 { get; set; } = "";
    public string DocumentVersions { get; set; } = "";
    public DateTime AcceptedAtUtc { get; set; }
    public string Locale { get; set; } = "";
    public bool AdultConfirmed { get; set; }
    public bool WarningAcknowledged { get; set; }
    public bool TermsAccepted { get; set; }
    public bool PrivacyNoticeAcknowledged { get; set; }
    public bool CommunityAccepted { get; set; }
    public bool TransactionsAccepted { get; set; }
}
