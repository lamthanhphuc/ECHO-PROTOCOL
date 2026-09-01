namespace EchoProtocol.Api.Data.Telemetry;

public static class MongoTelemetryInitializer
{
    public static Task InitializeAsync(
        ITelemetryEventRepository repository,
        CancellationToken cancellationToken = default) =>
        repository.EnsureIndexesAsync(cancellationToken);
}
