using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EchoProtocol.Api.Data.Telemetry;

public sealed class TelemetryMatchStateDocument
{
    [BsonId]
    [BsonElement("_id")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid MatchId { get; set; }

    [BsonElement("researchCaptureEnabled")]
    public bool ResearchCaptureEnabled { get; set; }

    [BsonElement("hasAcceptedMatchStarted")]
    public bool HasAcceptedMatchStarted { get; set; }

    [BsonElement("terminalSequence")]
    public long? TerminalSequence { get; set; }

    [BsonElement("highestAcceptedSequence")]
    public long HighestAcceptedSequence { get; set; }

    [BsonElement("updatedAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAtUtc { get; set; }
}
