namespace EchoProtocol.Api.Data.Telemetry;

public interface ITelemetryEventRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);

    Task<TelemetryWriteResult> AtomicCommitBatchAsync(
        IReadOnlyCollection<TelemetryEventDocument> events,
        CancellationToken cancellationToken = default);

    Task<TelemetryWriteResult> InsertBatchAsync(
        IReadOnlyCollection<TelemetryEventDocument> events,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, TelemetryWriteItemResult>> LoadConflictsAsync(
        IReadOnlyCollection<TelemetryEventDocument> events,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, TelemetryMatchBoundary>> LoadMatchBoundariesAsync(
        IReadOnlyCollection<Guid> matchIds,
        CancellationToken cancellationToken);
}

public sealed record TelemetryMatchBoundary(bool? ResearchCaptureEnabled, long? TerminalSequence);

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
    PermanentRejected,
    TransientFailure
}
