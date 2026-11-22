namespace EchoProtocol.Api.DTOs.Player;

public sealed record ProgressionCosmeticResponse(string RewardKey, string Category, int RequiredLevel,
    string DisplayName, bool Owned, bool Equipped, DateTime? GrantedAtUtc);

public sealed record EquipProgressionCosmeticRequest(string Category, string? RewardKey);
