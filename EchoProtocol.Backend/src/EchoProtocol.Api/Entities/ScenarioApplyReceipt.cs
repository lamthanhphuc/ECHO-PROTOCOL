namespace EchoProtocol.Api.Entities;

public sealed class ScenarioApplyReceipt
{
    public Guid DecisionId { get; set; }
    public Guid MatchId { get; set; }
    public Guid ReportedByUserId { get; set; }
    public string AppliedScenarioConfigId { get; set; } = string.Empty;
    public string AppliedScenarioConfigVersion { get; set; } = string.Empty;
    public string AppliedScenarioConfigFingerprint { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; }
    public ScenarioDecision Decision { get; set; } = null!;
}
