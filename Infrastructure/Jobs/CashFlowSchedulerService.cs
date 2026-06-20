using Application.Features.CashFlow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

internal sealed class CashFlowSchedulerService(
    IServiceScopeFactory              scopeFactory,
    ILogger<CashFlowSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private DateTime _lastDailyRun = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Brief startup delay so the app fully initialises before the first tick.
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CashFlow scheduler tick failed.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Fire once per calendar day at 02:00 UTC (offset from FIL midnight run).
        if (now.Hour != 2) return;
        if (_lastDailyRun.Date == now.Date) return;

        _lastDailyRun = now;
        logger.LogInformation("CashFlow: Starting nightly forecast batch for {Date:yyyy-MM-dd}.", now.Date);

        using var scope          = scopeFactory.CreateScope();
        var       cashFlowService = scope.ServiceProvider.GetRequiredService<ICashFlowComputationService>();
        await cashFlowService.ProcessAllActiveUsersAsync(ct);

        logger.LogInformation("CashFlow: Nightly forecast batch completed for {Date:yyyy-MM-dd}.", now.Date);
    }
}
