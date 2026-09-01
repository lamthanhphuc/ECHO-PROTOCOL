using EchoProtocol.Api.Configurations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EchoProtocol.Api.Health;

public sealed class MongoDatabaseHealthCheck : IHealthCheck
{
    private readonly IMongoClient _client;
    private readonly string _databaseName;

    public MongoDatabaseHealthCheck(
        IMongoClient client,
        IOptions<MongoDbSettings> settings)
    {
        _client = client;
        _databaseName = settings.Value.DatabaseName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client
                .GetDatabase(_databaseName)
                .RunCommandAsync<BsonDocument>(
                    new BsonDocument("ping", 1),
                    cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy("MongoDB connection succeeded.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "MongoDB connectivity check failed.",
                ex);
        }
    }
}
