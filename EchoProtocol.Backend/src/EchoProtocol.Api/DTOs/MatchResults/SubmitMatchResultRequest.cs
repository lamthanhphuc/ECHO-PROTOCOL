using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using EchoProtocol.Api.Enums;

namespace EchoProtocol.Api.DTOs.MatchResults;

public sealed class SubmitMatchResultRequest
{
    [Required]
    public MatchOutcome? Outcome { get; set; }

    [Range(typeof(decimal), "0", "1")]
    public decimal ObjectiveCompletion { get; set; }

    [Required, MinLength(1), MaxLength(4)]
    public List<SubmitMatchResultPlayerRequest> Players { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class SubmitMatchResultPlayerRequest
{
    public Guid UserId { get; set; }
    public bool Survived { get; set; }
    public bool Disconnected { get; set; }

    [Range(0, int.MaxValue)]
    public int DetectionCount { get; set; }

    [Range(0, int.MaxValue)]
    public int DownedCount { get; set; }

    [Range(0, int.MaxValue)]
    public int ReviveCount { get; set; }

    [Range(0, int.MaxValue)]
    public int ObjectiveContribution { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
