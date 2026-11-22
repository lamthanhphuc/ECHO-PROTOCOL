using System;
using System.Threading;
using System.Threading.Tasks;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using EchoProtocol.Networking.Authority;

namespace EchoProtocol.UI.Result
{
    /// <summary>The state of the local player's server-issued reward for one match.</summary>
    public enum ResultRewardStatus
    {
        Unavailable = 0,
        Confirmed,
        Pending,
        NotFound,
        Unauthenticated,
        InvalidMatchId,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Amounts are null until the backend confirms the reward. The Result UI must never
    /// display a locally calculated amount as if it had been granted.
    /// </summary>
    public readonly struct ResultRewardInfo
    {
        public Guid MatchId { get; }
        public ResultRewardStatus Status { get; }
        public int? CreditsAwarded { get; }
        public long? ExperienceAwarded { get; }
        public int? BalanceAfter { get; }
        public long? TotalExperience { get; }
        public int? CurrentLevel { get; }
        public string PolicyVersion { get; }
        public string ProgressionPolicyVersion { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public string LevelNotification { get; }
        public bool HasConfirmedReward => Status == ResultRewardStatus.Confirmed
            && CreditsAwarded.HasValue && ExperienceAwarded.HasValue;

        private ResultRewardInfo(
            Guid matchId,
            ResultRewardStatus status,
            int? creditsAwarded,
            long? experienceAwarded,
            int? balanceAfter,
            long? totalExperience,
            int? currentLevel,
            string policyVersion,
            string progressionPolicyVersion,
            string errorCode,
            string message, string levelNotification = null)
        {
            MatchId = matchId;
            Status = status;
            CreditsAwarded = creditsAwarded;
            ExperienceAwarded = experienceAwarded;
            BalanceAfter = balanceAfter;
            TotalExperience = totalExperience;
            CurrentLevel = currentLevel;
            PolicyVersion = policyVersion ?? string.Empty;
            ProgressionPolicyVersion = progressionPolicyVersion ?? string.Empty;
            ErrorCode = errorCode ?? string.Empty;
            Message = message ?? string.Empty;
            LevelNotification = levelNotification ?? string.Empty;
        }

        internal static ResultRewardInfo WithoutAmounts(
            Guid matchId,
            ResultRewardStatus status,
            string errorCode = null,
            string message = null) =>
            new ResultRewardInfo(matchId, status, null, null, null, null, null,
                null, null, errorCode, message);

        internal static ResultRewardInfo FromConfirmed(
            Guid matchId,
            RewardMeResponseDto reward) =>
            new ResultRewardInfo(matchId, ResultRewardStatus.Confirmed,
                reward.currencyAmount, reward.experiencePointsAwarded,
                reward.balanceAfter, reward.currentExperiencePoints,
                reward.currentLevel, reward.policyVersion,
                reward.progressionPolicyVersion, null, null, BuildLevelNotification(reward));

        public static string BuildLevelNotification(RewardMeResponseDto reward)
        {
            if (reward == null) return string.Empty;
            string gift = "";
            if (reward.cosmeticRewards != null)
                foreach (var cosmetic in reward.cosmeticRewards)
                    if (cosmetic != null) gift += (gift.Length > 0 ? ", " : "") + cosmetic.displayName;
            if (gift.Length > 0) gift = EchoProtocol.Settings.GameLanguage.Choose("Đã nhận: ", "Received: ") + gift;
            if (reward.levelBefore < 1 || reward.levelAfter <= reward.levelBefore) return gift;
            string title = EchoProtocol.Settings.GameLanguage.Choose($"ĐẠT CẤP {reward.levelAfter}", $"LEVEL {reward.levelAfter} REACHED")
                + (gift.Length > 0 ? "\n" + gift : "");
            if (reward.unlockedItems == null || reward.unlockedItems.Length == 0) return title;
            var names = new System.Collections.Generic.List<string>();
            foreach (var item in reward.unlockedItems)
                if (item != null && !string.IsNullOrWhiteSpace(item.itemName) && names.Count < 2) names.Add(item.itemName);
            if (names.Count == 0) return title;
            string extra = reward.unlockedItems.Length > names.Count
                ? EchoProtocol.Settings.GameLanguage.Choose($" và {reward.unlockedItems.Length - names.Count} món khác", $" + {reward.unlockedItems.Length - names.Count} more") : "";
            return title + "\n" + EchoProtocol.Settings.GameLanguage.Choose("Mở quyền mua: ", "Purchase unlocked: ") + string.Join(", ", names) + extra;
        }
    }

    /// <summary>
    /// Reads the immutable reward grant for the authenticated local player. This bridge
    /// issues GET only; the host result outbox remains the sole result submitter.
    /// </summary>
    public static class ResultRewardBridge
    {
        public static async Task<ResultRewardInfo> LoadAsync(
            Guid matchId,
            CancellationToken cancellationToken = default)
        {
            if (matchId == Guid.Empty)
                return ResultRewardInfo.WithoutAmounts(matchId, ResultRewardStatus.InvalidMatchId);
            if (cancellationToken.IsCancellationRequested)
                return ResultRewardInfo.WithoutAmounts(matchId, ResultRewardStatus.Cancelled);
            if (!AuthSession.IsAuthenticated)
                return ResultRewardInfo.WithoutAmounts(matchId, ResultRewardStatus.Unauthenticated);

            // The existing match runtime may already have completed the same GET. A
            // completed reward is immutable, so its matching response is safe to reuse.
            var cached = MatchAuthorityRuntime.Instance?.LastLocalReward;
            if (TryReadConfirmed(matchId, cached, out var confirmed)) return confirmed;

            try
            {
                var client = AuthRuntime.EnsureExists().Client;
                if (client == null)
                    return ResultRewardInfo.WithoutAmounts(matchId, ResultRewardStatus.Unavailable);

                var response = await new MatchAuthorityApiService(client)
                    .GetMyRewardAsync(matchId);
                if (cancellationToken.IsCancellationRequested)
                    return ResultRewardInfo.WithoutAmounts(matchId, ResultRewardStatus.Cancelled);

                if (response == null)
                    return ResultRewardInfo.WithoutAmounts(matchId, ResultRewardStatus.Unavailable);
                if (!response.IsSuccess)
                    return FromFailure(matchId, response);
                if (response.Data == null || !response.Data.success || response.Data.data == null)
                    return ResultRewardInfo.WithoutAmounts(matchId,
                        ResultRewardStatus.Failed, "REWARD_INVALID_RESPONSE");

                var reward = response.Data.data;
                if (string.Equals(reward.rewardStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                    return ResultRewardInfo.WithoutAmounts(matchId, ResultRewardStatus.Pending);
                if (TryReadConfirmed(matchId, reward, out confirmed)) return confirmed;
                return ResultRewardInfo.WithoutAmounts(matchId,
                    ResultRewardStatus.Failed, "REWARD_INVALID_RESPONSE");
            }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested)
                    return ResultRewardInfo.WithoutAmounts(matchId, ResultRewardStatus.Cancelled);
                return ResultRewardInfo.WithoutAmounts(matchId,
                    ResultRewardStatus.Failed, "REWARD_REQUEST_FAILED", exception.Message);
            }
        }

        private static bool TryReadConfirmed(
            Guid matchId,
            RewardMeResponseDto reward,
            out ResultRewardInfo confirmed)
        {
            confirmed = default;
            if (reward == null
                || !Guid.TryParse(reward.matchId, out var rewardMatchId)
                || rewardMatchId != matchId
                || !string.Equals(reward.rewardStatus, "Completed", StringComparison.OrdinalIgnoreCase)
                || reward.currencyAmount < 0
                || reward.experiencePointsAwarded < 0
                || reward.currentLevel < 1
                || string.IsNullOrWhiteSpace(reward.policyVersion)
                || string.IsNullOrWhiteSpace(reward.progressionPolicyVersion)
                || string.IsNullOrWhiteSpace(reward.processedAtUtc))
            {
                return false;
            }

            confirmed = ResultRewardInfo.FromConfirmed(matchId, reward);
            return true;
        }

        private static ResultRewardInfo FromFailure(
            Guid matchId,
            ApiResult<ApiResponse<RewardMeResponseDto>> response)
        {
            var code = response.ErrorCode ?? string.Empty;
            ResultRewardStatus status;
            if (string.Equals(code, "REWARD_PENDING", StringComparison.Ordinal))
                status = ResultRewardStatus.Pending;
            else if (string.Equals(code, "REWARD_RESULT_NOT_FOUND", StringComparison.Ordinal)
                     || string.Equals(code, "REWARD_GRANT_NOT_FOUND", StringComparison.Ordinal))
                status = ResultRewardStatus.NotFound;
            else if (response.StatusCode == 401)
                status = ResultRewardStatus.Unauthenticated;
            else if (response.FailureKind == ApiFailureKind.Network
                     || response.FailureKind == ApiFailureKind.Timeout)
                status = ResultRewardStatus.Unavailable;
            else
                status = ResultRewardStatus.Failed;

            return ResultRewardInfo.WithoutAmounts(matchId, status, code, response.Message);
        }
    }
}
