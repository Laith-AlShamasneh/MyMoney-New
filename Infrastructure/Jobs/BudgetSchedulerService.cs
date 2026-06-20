using Application.Common.Constants;
using Application.Features.Budget;
using Application.Features.Budget.Jobs;
using Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

internal sealed class BudgetSchedulerService(
    IServiceScopeFactory             scopeFactory,
    ILogger<BudgetSchedulerService>  logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private DateTime _lastDailyRun = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                logger.LogError(ex, "Budget scheduler tick failed.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Fire once per calendar day at 03:00 UTC.
        if (now.Hour != 3) return;
        if (_lastDailyRun.Date == now.Date) return;

        _lastDailyRun = now;
        logger.LogInformation("Budget: Starting daily maintenance batch for {Date:yyyy-MM-dd}.", now.Date);

        using var scope      = scopeFactory.CreateScope();
        var       jobService = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();

        // Enqueue a single all-users maintenance job; the handler fans it out per-user.
        await jobService.EnqueueAsync(
            JobTypes.BudgetDailyMaintenance,
            new BudgetDailyMaintenancePayload(UserId: null),
            priority: 3, maxAttempts: 2, ct: ct);

        logger.LogInformation("Budget: Daily maintenance job enqueued for {Date:yyyy-MM-dd}.", now.Date);
    }
}
