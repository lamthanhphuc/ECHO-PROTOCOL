namespace EchoProtocol.Api.Entities;

public sealed class AdaptiveConfigurationVersion
{
    public string Version { get; set; } = string.Empty;
    public string Compatibility { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string ConfigurationJson { get; set; } = string.Empty;
    public Guid PublishedByUserId { get; set; }
    public DateTime PublishedAtUtc { get; set; }
}
