namespace EchoProtocol.Api.Services;

public sealed class PaymentFulfillmentRecoveryWorker(
    IServiceScopeFactory scopes, ILogger<PaymentFulfillmentRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                List<Guid> pending;
                using (var scope = scopes.CreateScope())
                    pending = await scope.ServiceProvider.GetRequiredService<PaymentRecoveryService>().PendingAsync(stoppingToken);
                foreach (var orderId in pending)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        var result = await scope.ServiceProvider.GetRequiredService<PaymentRecoveryService>().RecoverAsync(orderId, stoppingToken);
                        if (!result.IsSuccess) logger.LogWarning("Payment recovery pending for {OrderId}: {ErrorCode}", orderId, result.ErrorCode);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception exception) { logger.LogError(exception, "Payment recovery failed for {OrderId}", orderId); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Could not load payments pending recovery"); }
        }
    }
}
