namespace EchoProtocol.Api.Configurations;

public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string DatabaseName { get; set; } = string.Empty;
    public string TelemetryCollectionName { get; set; } = "telemetry_events";
    public int MaxBatchSize { get; set; } = 500;
    public string SupportedSchemaVersion { get; set; } = "1.1";
    public int MaxValueJsonBytes { get; set; } = 32_768;
    public int MaxFutureSkewMinutes { get; set; } = 5;
    public int MaxEventAgeDays { get; set; } = 7;
}
