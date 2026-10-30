using System;
using System.Collections.Generic;
using EchoProtocol.Auth;
using EchoProtocol.Settings;
using UnityEngine;

namespace EchoProtocol.Networking.Authority
{
        [Serializable] public sealed class PendingMatchResultRecord
        {
            public string matchId, ownerId;
            public long notBeforeUtc, nextLeaseUtc;
            [SerializeReference] public SubmitMatchResultRequestDto request;
            public bool rejected;
            public bool saved;
            public int attempts;
            public long nextAttemptUtc;
            public string lastErrorCode;
        }
        [Serializable] public sealed class PendingMatchResultQueue { public List<PendingMatchResultRecord> items = new(); }
    public sealed partial class MatchAuthorityRuntime
    {
        private const string ResultOutboxKey = "Echo.MatchResult.Outbox.v1";
        private PendingMatchResultQueue _resultQueue = new();
        private bool _outboxSending;
        public string ResultSaveStatus { get; private set; } = "";
        private bool HasQueuedResult(Guid id) => id != Guid.Empty && _resultQueue.items.Exists(x => x.matchId == id.ToString("D"));
        private void LoadResultOutbox()
        {
            try { _resultQueue = JsonUtility.FromJson<PendingMatchResultQueue>(PlayerPrefs.GetString(ResultOutboxKey, "")) ?? new PendingMatchResultQueue(); }
            catch (Exception) { _resultQueue = new PendingMatchResultQueue(); }
            _resultQueue.items ??= new List<PendingMatchResultRecord>();
            _resultQueue.items.RemoveAll(item => item == null);
        }
        private void SaveResultOutbox()
        {
            PlayerPrefs.SetString(ResultOutboxKey, JsonUtility.ToJson(_resultQueue));
            PlayerPrefs.Save();
        }
        private void CaptureResultOutbox()
        {
            if (HasQueuedResult(MatchId)) { _pendingBackendMatchResult = false; return; }
            if (!HasBinding || !TryBuildMatchResultRequest(out var request)) return;
            float remaining = _backendMatchStartedAtRealtime < 0 ? 0 : Mathf.Max(0,
                MinimumBackendMatchResultAgeSeconds - (Time.realtimeSinceStartup - _backendMatchStartedAtRealtime));
            _resultQueue.items.Add(new PendingMatchResultRecord { matchId = MatchId.ToString("D"),
                ownerId = AuthSession.CurrentUserId, request = request,
                notBeforeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)Math.Ceiling(remaining) });
            SaveResultOutbox();
            _pendingBackendMatchResult = false;
            SetResultSaveStatus(GameLanguage.Choose("Đang lưu kết quả trận…", "Saving match result…"));
        }
        private void SetResultSaveStatus(string text)
        {
            if (ResultSaveStatus == text) return;
            ResultSaveStatus = text;
            EchoProtocol.UI.GameUIFeedback.Instance?.Toast(text);
        }
        public string GetResultSaveStatus(string matchId)
        {
            var entry = _resultQueue.items.Find(item => item.matchId == matchId && item.ownerId == AuthSession.CurrentUserId);
            if (entry == null) return "";
            if (entry.saved) return GameLanguage.Choose("Đã lưu kết quả trận", "Match result saved");
            if (entry.rejected)
                return entry.lastErrorCode == "MATCH_LEASE_EXPIRED"
                    ? GameLanguage.Choose("Phiên xác thực đã hết hạn. Kết quả chỉ được giữ trên máy; chưa có thưởng từ máy chủ.", "Authority session expired. Result retained locally; no server reward confirmed.")
                    : GameLanguage.Choose("Máy chủ từ chối kết quả; bản trên máy được giữ lại. Mã: ", "Server rejected the result; local record retained. Code: ") + entry.lastErrorCode;
            return GameLanguage.Choose("Kết quả được giữ trên máy và sẽ tự gửi lại khi kết nối khả dụng.", "Result retained locally and will retry when the connection is available.");
        }
        public bool IsResultPermanentlyRejected(string matchId) => _resultQueue.items.Exists(item =>
            item.matchId == matchId && item.ownerId == AuthSession.CurrentUserId && item.rejected);

        public static bool IsPermanentResultFailure(long statusCode, string errorCode, bool durationCanRetry)
        {
            if (statusCode == 401 || statusCode == 408 || statusCode == 429 || statusCode >= 500) return false;
            if (errorCode == "MATCH_RESULT_INVALID_DURATION") return !durationCanRetry;
            return statusCode >= 400 && statusCode < 500;
        }
        private async void TickResultOutbox()
        {
            if (_outboxSending || _api == null || !AuthSession.IsAuthenticated) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var entry = _resultQueue.items.Find(x => !x.rejected && !x.saved && x.ownerId == AuthSession.CurrentUserId
                && x.nextAttemptUtc <= now && (x.notBeforeUtc <= now || x.nextLeaseUtc <= now));
            if (entry == null) return;
            if (!Guid.TryParse(entry.matchId, out var id) || entry.request == null)
            {
                entry.rejected = true;
                entry.lastErrorCode = "INVALID_LOCAL_RESULT";
                SaveResultOutbox();
                return;
            }
            _outboxSending = true;
            try
            {
                // Maintain the existing host lease while waiting for minimum result age,
                // even after Fusion has been shut down. This never revives an expired lease.
                if (entry.nextLeaseUtc <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    await _api.RenewLeaseAsync(id);
                    entry.nextLeaseUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 15;
                }
                if (!AuthSession.IsAuthenticated || entry.ownerId != AuthSession.CurrentUserId) return;
                if (entry.notBeforeUtc > DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return;
                // Submit even after lease renewal fails: an earlier request may have committed
                // before its response was lost. Backend checks exact replay before lease expiry.
                var result = await _api.SubmitResultAsync(id, entry.request);
                if (!AuthSession.IsAuthenticated || entry.ownerId != AuthSession.CurrentUserId) return;
                if (IsSuccessful(result))
                {
                    entry.saved = true;
                    entry.request = null;
                    entry.lastErrorCode = "";
                    if (MatchId == id) { _pendingBackendMatchResult = false; IsHostBinding = false; }
                    int savedCount = _resultQueue.items.FindAll(item => item.saved).Count;
                    for (int i = 0; savedCount > 20 && i < _resultQueue.items.Count;)
                        if (_resultQueue.items[i].saved) { _resultQueue.items.RemoveAt(i); savedCount--; } else i++;
                    SaveResultOutbox();
                    SetResultSaveStatus(GameLanguage.Choose("Đã lưu kết quả trận", "Match result saved"));
                }
                else
                {
                    // Preserve rejected payloads too, for recovery; do not retry permanent errors forever.
                    entry.lastErrorCode = result?.ErrorCode ?? "NO_RESPONSE";
                    entry.rejected = result != null && IsPermanentResultFailure(result.StatusCode,
                        entry.lastErrorCode, now < entry.notBeforeUtc + 120);
                    entry.attempts++;
                    entry.nextAttemptUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Min(60, 5L << Math.Min(4, entry.attempts - 1));
                    SaveResultOutbox();
                    SetResultSaveStatus(GetResultSaveStatus(entry.matchId));
                }
            }
            catch (Exception)
            {
                entry.nextAttemptUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 15;
                SaveResultOutbox();
                if (entry.ownerId == AuthSession.CurrentUserId)
                    SetResultSaveStatus(GameLanguage.Choose("Đã giữ kết quả trận để gửi lại", "Match result retained for retry"));
            }
            finally { _outboxSending = false; }
        }
    }
}
