using System;
using System.Collections.Generic;
using EchoProtocol.Api;
using EchoProtocol.Auth;
using UnityEngine;

namespace EchoProtocol.Telemetry.Unity
{
    public sealed class TelemetryApiTransport : ITelemetryTransport
    {
        private readonly ApiClient _apiClient;

        public TelemetryApiTransport(ApiClient apiClient)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        }

        public void SendBatch(string batchJson, Action<TelemetryTransportResult> callback)
        {
            if (string.IsNullOrWhiteSpace(TokenStorage.GetAccessToken()))
            {
                callback?.Invoke(new TelemetryTransportResult(false, null, "AUTH_TOKEN_UNAVAILABLE"));
                return;
            }

            _apiClient.PostRawJson<TelemetryBatchApiResponse>(
                ApiEndpoints.TelemetryBatch,
                batchJson,
                true,
                result => callback?.Invoke(MapResult(result)));
        }

        private static TelemetryTransportResult MapResult(ApiResult<TelemetryBatchApiResponse> result)
        {
            if (result == null || !result.IsSuccess || result.Data == null || result.Data.data == null)
            {
                var error = result == null
                    ? "NO_TRANSPORT_RESULT"
                    : string.IsNullOrWhiteSpace(result.ErrorCode)
                        ? result.Message
                        : result.ErrorCode;
                return new TelemetryTransportResult(false, null, error);
            }

            var acknowledgements = new List<TelemetryAckItem>();
            var items = result.Data.data.items ?? Array.Empty<TelemetryAckItemDto>();
            foreach (var item in items)
            {
                if (item == null || !Guid.TryParse(item.id, out var id))
                {
                    continue;
                }

                if (!TryParseStatus(item.status, out var status))
                {
                    continue;
                }

                acknowledgements.Add(new TelemetryAckItem(id, status, item.rejectReason));
            }

            return new TelemetryTransportResult(true, acknowledgements);
        }

        private static bool TryParseStatus(string value, out TelemetryAckStatus status)
        {
            switch (value)
            {
                case "ACCEPTED":
                    status = TelemetryAckStatus.Accepted;
                    return true;
                case "DUPLICATE_ALREADY_ACCEPTED":
                    status = TelemetryAckStatus.DuplicateAlreadyAccepted;
                    return true;
                case "PERMANENTLY_REJECTED":
                    status = TelemetryAckStatus.PermanentlyRejected;
                    return true;
                case "TRANSIENT_FAILURE":
                    status = TelemetryAckStatus.TransientFailure;
                    return true;
                default:
                    status = default;
                    return false;
            }
        }
    }

    [Serializable]
    public sealed class TelemetryBatchApiResponse
    {
        public bool success;
        public string message;
        public TelemetryBatchAckDataDto data;
        public string errorCode;
    }

    [Serializable]
    public sealed class TelemetryBatchAckDataDto
    {
        public TelemetryAckItemDto[] items;
    }

    [Serializable]
    public sealed class TelemetryAckItemDto
    {
        public string id;
        public string status;
        public string rejectReason;
    }
}
