namespace EchoProtocol.Api.Services;

public static class MatchContentCatalog
{
    public sealed record Entry(string Map, string Mode, string Monster, string Route);

    public static readonly Entry[] Entries =
    [
        new("SciFi", "EXPEDITION", "STALKER", "DEFAULT_ROUTE"),
        new("Scene_A", "EXPEDITION", "LIZARD", "SCENE_A_AUTHORED"),
        new("Scene_B", "PURGE", "LIZARD", "SCENE_B_AUTHORED")
    ];

    public static readonly string[] Difficulties = ["EASY", "NORMAL", "HARD"];
}
