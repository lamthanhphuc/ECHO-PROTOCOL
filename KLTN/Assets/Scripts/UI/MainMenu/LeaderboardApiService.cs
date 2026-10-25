using System;
using EchoProtocol.Api;

namespace EchoProtocol.UI.MainMenu
{
    [Serializable]
    public sealed class LeaderboardApiResponse
    {
        public bool success;
        public string message;
        public LeaderboardPageDto data;
        public string errorCode;
    }

    [Serializable]
    public sealed class LeaderboardPageDto
    {
        public LeaderboardEntryDto[] items;
        public int totalCount;
        public int page;
        public int pageSize;
        public string[] maps;
        public string[] difficulties;
    }

    [Serializable]
    public sealed class LeaderboardEntryDto
    {
        public int rank;
        public string matchId;
        public string displayName;
        public string avatarUrl;
        public long durationCentiseconds;
        public string squadLabel;
        public LeaderboardPlayerDto[] players;
    }

    [Serializable]
    public sealed class LeaderboardPlayerDto
    {
        public string userId;
        public string displayName;
        public string avatarUrl;
    }

    public enum LeaderboardMode
    {
        Solo,
        Team
    }

    public sealed class LeaderboardApiService
    {
        private readonly ApiClient _client;

        public LeaderboardApiService(ApiClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void GetPage(
            LeaderboardMode mode,
            string mapId,
            string difficulty,
            int page,
            int pageSize,
            Action<ApiResult<LeaderboardApiResponse>> callback)
        {
            _client.GetJson<LeaderboardApiResponse>(
                BuildEndpoint(mode, mapId, difficulty, page, pageSize),
                attachBearer: true,
                callback);
        }

        public static string BuildEndpoint(
            LeaderboardMode mode,
            string mapId,
            string difficulty,
            int page,
            int pageSize)
        {
            if (string.IsNullOrWhiteSpace(mapId))
                throw new ArgumentException("A map is required.", nameof(mapId));
            if (string.IsNullOrWhiteSpace(difficulty))
                throw new ArgumentException("A difficulty is required.", nameof(difficulty));
            var endpoint = "/api/leaderboards?mode="
                + (mode == LeaderboardMode.Team ? "TEAM" : "SOLO")
                + "&page=" + Math.Max(1, page)
                + "&pageSize=" + Math.Max(1, Math.Min(50, pageSize))
                + "&mapId=" + Uri.EscapeDataString(mapId)
                + "&difficulty=" + Uri.EscapeDataString(difficulty);
            return endpoint;
        }
    }

    public static class LeaderboardPresentation
    {
        public static string FormatDuration(long centiseconds)
        {
            if (centiseconds < 0) return "--.--";
            long minutes = centiseconds / 6000;
            long seconds = (centiseconds / 100) % 60;
            return minutes.ToString("00") + "." + seconds.ToString("00");
        }

        public static LeaderboardEntryDto[] OrderEntries(LeaderboardEntryDto[] entries)
        {
            if (entries == null) return Array.Empty<LeaderboardEntryDto>();
            var ordered = (LeaderboardEntryDto[])entries.Clone();
            Array.Sort(ordered, (a, b) =>
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                int comparison = a.durationCentiseconds.CompareTo(b.durationCentiseconds);
                return comparison != 0 ? comparison : a.rank.CompareTo(b.rank);
            });
            return ordered;
        }
    }
}
