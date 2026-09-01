namespace EchoProtocol.Api.Data.Telemetry;

public interface ITelemetryEventRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);

    Task<TelemetryWriteResult> InsertBatchAsync(
        IReadOnlyCollection<TelemetryEventDocument> events,
        CancellationToken cancellationToken);
}

public sealed record TelemetryWriteResult(IReadOnlyList<TelemetryWriteItemResult> Items);

public sealed record TelemetryWriteItemResult(
    Guid Id,
    TelemetryWriteStatus Status,
    string? Reason = null);

public enum TelemetryWriteStatus
{
    Accepted,
    DuplicateAlreadyAccepted,
    IdentityConflict,
    SequenceConflict,
    TransientFailure
}
