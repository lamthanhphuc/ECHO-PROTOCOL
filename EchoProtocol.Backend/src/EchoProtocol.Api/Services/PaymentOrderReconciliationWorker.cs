namespace EchoProtocol.Api.Services;

public sealed class PaymentOrderReconciliationWorker(
    IServiceScopeFactory scopes, ILogger<PaymentOrderReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var offset = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                List<Guid> due;
                using (var scope = scopes.CreateScope())
                {
                    var service = scope.ServiceProvider.GetRequiredService<PaymentOrderReconciliationService>();
                    due = await service.DueAsync(stoppingToken, offset);
                    if (due.Count == 0 && offset > 0)
                    {
                        offset = 0;
                        due = await service.DueAsync(stoppingToken);
                    }
                }
                offset += due.Count;
                foreach (var id in due)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        var status = await scope.ServiceProvider.GetRequiredService<PaymentOrderReconciliationService>()
                            .ReconcileAsync(id, stoppingToken);
                        if (status is not null)
                            logger.LogInformation("Payment order {OrderId} reconciled as {Status}", id, status);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception exception)
                    { logger.LogError(exception, "Payment order reconciliation failed for {OrderId}", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            { logger.LogError(exception, "Could not load payment orders for reconciliation"); }
        }
    }
}
