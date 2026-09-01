namespace EchoProtocol.Api.DTOs.Telemetry;

public sealed class TelemetryBatchResponse
{
    public IReadOnlyList<TelemetryBatchAckItem> Items { get; init; } = [];
}

public sealed class TelemetryBatchAckItem
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? RejectReason { get; init; }
}

public static class TelemetryAckStatuses
{
    public const string Accepted = "ACCEPTED";
    public const string DuplicateAlreadyAccepted = "DUPLICATE_ALREADY_ACCEPTED";
    public const string PermanentlyRejected = "PERMANENTLY_REJECTED";
    public const string TransientFailure = "TRANSIENT_FAILURE";
}
