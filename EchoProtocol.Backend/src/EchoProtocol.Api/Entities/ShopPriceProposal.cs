namespace EchoProtocol.Api.Entities;

public sealed class ShopPriceProposal
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public int PreviousPrice { get; set; }
    public int ProposedPrice { get; set; }
    public DateTime ItemUpdatedAtUtc { get; set; }
    public Guid ProposedByUserId { get; set; }
    public DateTime ProposedAtUtc { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewReason { get; set; }
}
