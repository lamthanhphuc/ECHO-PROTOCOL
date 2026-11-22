namespace EchoProtocol.Api.Services;

public static class ProgressionCurveV3
{
    public const string Version = "LEVEL_V3";
    public const int LevelCap = 30;

    public static long RequiredForNextLevel(int level)
    {
        if (level < 1 || level > LevelCap) throw new ArgumentOutOfRangeException(nameof(level));
        long n = level - 1;
        return ((250 + 25 * n + 3 * n * n + 24) / 25) * 25;
    }

    public static long Threshold(int level)
    {
        if (level < 1 || level > LevelCap) throw new ArgumentOutOfRangeException(nameof(level));
        long total = 0;
        for (int current = 1; current < level; current++) total += RequiredForNextLevel(current);
        return total;
    }

    public static int GetLevel(long experience)
    {
        if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
        int level = 1;
        while (level < LevelCap && experience >= Threshold(level + 1)) level++;
        return level;
    }
}
