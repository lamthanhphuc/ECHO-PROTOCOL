namespace EchoProtocol.Api.Entities;
public sealed class SalvageCheckpoint
{
    public Guid MatchId {get;set;}
    public Guid OwnerId {get;set;}
    public long Revision {get;set;}
    public string StateJson {get;set;}="";
    public string ResultJson {get;set;}="";
    public DateTime SavedAtUtc {get;set;}
    public bool Failed {get;set;}
}
