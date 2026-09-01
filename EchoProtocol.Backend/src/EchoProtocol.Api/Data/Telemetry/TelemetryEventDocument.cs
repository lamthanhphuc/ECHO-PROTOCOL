using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EchoProtocol.Api.Data.Telemetry;

public sealed class TelemetryEventDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; init; }

    [BsonElement("matchId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid MatchId { get; init; }

    [BsonElement("userId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    [BsonIgnoreIfNull]
    public Guid? UserId { get; init; }

    [BsonElement("eventType")]
    public string EventType { get; init; } = string.Empty;

    [BsonElement("ts")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Ts { get; init; }

    [BsonElement("eventSequence")]
    public long EventSequence { get; init; }

    [BsonElement("valueJson")]
    public BsonDocument ValueJson { get; init; } = new();

    [BsonElement("reasonCode")]
    [BsonIgnoreIfNull]
    public string? ReasonCode { get; init; }

    [BsonElement("schemaVersion")]
    public string SchemaVersion { get; init; } = string.Empty;

    [BsonElement("semanticFingerprint")]
    public string SemanticFingerprint { get; init; } = string.Empty;

    [BsonElement("ingestedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime IngestedAt { get; init; }
}
