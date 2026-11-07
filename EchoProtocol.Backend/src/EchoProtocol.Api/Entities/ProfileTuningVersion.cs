namespace EchoProtocol.Api.Entities;

public sealed class ProfileTuningVersion
{
    public string Version { get; set; } = "";
    public int Revision { get; set; }
    public string SettingsJson { get; set; } = "";
    public Guid PublishedByUserId { get; set; }
    public DateTime PublishedAtUtc { get; set; }
}
