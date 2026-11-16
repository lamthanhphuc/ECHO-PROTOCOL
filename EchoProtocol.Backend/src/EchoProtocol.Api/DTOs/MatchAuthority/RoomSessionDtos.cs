using System.ComponentModel.DataAnnotations;

namespace EchoProtocol.Api.DTOs.MatchAuthority;

public sealed record CreateRoomRequest(Guid RequestId, [Required, StringLength(128)] string FusionSessionName,
    [Required, StringLength(32)] string Region);
public sealed record PrepareRoomMatchRequest(Guid RequestId, [Range(2, 4)] int MaxPlayers);
public sealed record ActivateRoomRequest([StringLength(32)] string? ActualRegion);
public sealed record CloseRoomRequest(Guid ActionId, [Required, StringLength(500, MinimumLength = 10)] string Reason);
public sealed record RoomCloseStatusResponse(bool Closed, string? Reason);
public sealed record RoomSessionResponse(Guid RoomInstanceId, string FusionSessionName, string Region, string? ActualRegion,
    Guid HostUserId, DateTime CreatedAtUtc, DateTime LeaseExpiresAtUtc, DateTime? ActivatedAtUtc, Guid? CurrentMatchId,
    bool MatchPublished, DateTime? ClosedAtUtc, string? CloseReason, DateTime? HostExecutedAtUtc = null);
