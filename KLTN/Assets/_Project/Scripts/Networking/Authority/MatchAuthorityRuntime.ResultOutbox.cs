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
            public SubmitMatchResultRequestDto request;
            public bool rejected;
        }
        [Serializable] public sealed class PendingMatchResultQueue { public List<PendingMatchResultRecord> items = new(); }
    public sealed partial class MatchAuthorityRuntime
    {
        private const string ResultOutboxKey = "Echo.MatchResult.Outbox.v1";
        private PendingMatchResultQueue _resultQueue = new();
        private bool _outboxSending;
        private float _outboxRetryAt;
        public string ResultSaveStatus { get; private set; } = "";
        private bool HasQueuedResult(Guid id) => id != Guid.Empty && _resultQueue.items.Exists(x => x.matchId == id.ToString("D"));
        private void LoadResultOutbox()
        {
            try { _resultQueue = JsonUtility.FromJson<PendingMatchResultQueue>(PlayerPrefs.GetString(ResultOutboxKey, "")) ?? new PendingMatchResultQueue(); }
            catch (Exception) { _resultQueue = new PendingMatchResultQueue(); }
            _resultQueue.items ??= new List<PendingMatchResultRecord>();
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
        private async void TickResultOutbox()
        {
            if (_outboxSending || _api == null || !AuthSession.IsAuthenticated || Time.unscaledTime < _outboxRetryAt) return;
            var entry = _resultQueue.items.Find(x => !x.rejected && x.ownerId == AuthSession.CurrentUserId
                && (x.notBeforeUtc <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    || x.nextLeaseUtc <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            if (entry == null) return;
            if (!Guid.TryParse(entry.matchId, out var id)) return;
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
                if (entry.notBeforeUtc > DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return;
                var result = await _api.SubmitResultAsync(id, entry.request);
                if (IsSuccessful(result))
                {
                    _resultQueue.items.Remove(entry);
                    if (MatchId == id) { _pendingBackendMatchResult = false; IsHostBinding = false; }
                    SaveResultOutbox();
                    SetResultSaveStatus(GameLanguage.Choose("Đã lưu kết quả trận", "Match result saved"));
                }
                else
                {
                    // Preserve rejected payloads too, for recovery; do not retry permanent errors forever.
                    entry.rejected = result != null && result.FailureKind == EchoProtocol.Api.ApiFailureKind.Business
                        && result.ErrorCode != "MATCH_RESULT_INVALID_DURATION";
                    SaveResultOutbox();
                    SetResultSaveStatus(GameLanguage.Choose(entry.rejected
                        ? "Máy chủ chưa chấp nhận kết quả. Dữ liệu đã được giữ lại."
                        : "Chưa lưu được kết quả. Đã giữ dữ liệu và sẽ thử lại.", entry.rejected
                        ? "Server rejected the result. Data has been retained."
                        : "Result not saved yet. Data retained; retrying."));
                    _outboxRetryAt = Time.unscaledTime + 5f;
                }
            }
            catch (Exception)
            {
                _outboxRetryAt = Time.unscaledTime + 5f;
                SetResultSaveStatus(GameLanguage.Choose("Đã giữ kết quả trận để gửi lại", "Match result retained for retry"));
            }
            finally { _outboxSending = false; }
        }
    }
}
