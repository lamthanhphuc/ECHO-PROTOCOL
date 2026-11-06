using System.ComponentModel.DataAnnotations;

namespace EchoProtocol.Api.DTOs.Admin;

public sealed class CreateRemoteFixedVersionRequest
{
    [Required, StringLength(80, MinimumLength = 1)] public string Version { get; set; } = "";
    [Required, StringLength(64)] public string MapId { get; set; } = "";
    [Required, StringLength(16)] public string Difficulty { get; set; } = "";
    [Required, StringLength(8192)] public string SnapshotJson { get; set; } = "";
}

public sealed class ChangeRemoteFixedActivationRequest
{
    [StringLength(80)] public string ExpectedActiveVersion { get; set; } = "";
    [Required, StringLength(500, MinimumLength = 1)] public string Reason { get; set; } = "";
}

public sealed class RemoteFixedVersionsQuery : AdminPagedQuery
{
    public string? MapId { get; set; }
    public string? Difficulty { get; set; }
}

public class RemoteFixedVersionResponse
{
    public string Version { get; init; } = "";
    public string MapId { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public string ProtocolVersion { get; init; } = "";
    public string ContentHash { get; init; } = "";
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class RemoteFixedVersionDetailResponse : RemoteFixedVersionResponse
{
    public string SnapshotJson { get; init; } = "";
    public string? ActiveVersion { get; init; }
}

public sealed class RemoteFixedActivationResponse
{
    public string? PreviousActiveVersion { get; init; }
    public string? ActiveVersion { get; init; }
    public bool Changed { get; init; }
}

public sealed class RemoteFixedAuditResponse
{
    public Guid Id { get; init; }
    public string Action { get; init; } = "";
    public string MapId { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public string? PreviousVersion { get; init; }
    public string? TargetVersion { get; init; }
    public Guid AdminUserId { get; init; }
    public string Reason { get; init; } = "";
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class RemoteFixedStatusResponse
{
    public bool RemoteFixedEnabled { get; init; }
    public string MatchDataPolicy { get; init; } = "";
    public string ProtocolVersion { get; init; } = "";
    public IReadOnlyList<string> SupportedMaps { get; init; } = [];
}
