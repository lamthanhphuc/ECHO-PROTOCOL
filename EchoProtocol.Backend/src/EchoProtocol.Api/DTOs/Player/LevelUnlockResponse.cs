namespace EchoProtocol.Api.DTOs.Player;

public sealed record LevelUnlockResponse(Guid ItemId, string ItemName, string Category, int RequiredLevel,
    long ExperienceRemaining = 0, bool Owned = false, int Price = 0);
