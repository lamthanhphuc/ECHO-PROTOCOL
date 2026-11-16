namespace EchoProtocol.Api.Entities;

public sealed class ChatWordPolicy
{
    public int Id { get; set; } = 1;
    public string WordsJson { get; set; } = "[]";
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
