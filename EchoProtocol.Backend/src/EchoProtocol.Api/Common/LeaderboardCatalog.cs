namespace EchoProtocol.Api.Common;

public static class LeaderboardCatalog
{
    public static readonly string[] Maps = ["SciFi", "Scene_A", "Scene_B"];
    public static readonly string[] Difficulties = ["EASY", "NORMAL", "HARD"];

    public static bool IsKnownMap(string? mapId) =>
        mapId is not null && Maps.Contains(mapId, StringComparer.Ordinal);

    public static bool IsKnownDifficulty(string? difficulty) =>
        difficulty is not null && Difficulties.Contains(difficulty, StringComparer.Ordinal);
}
