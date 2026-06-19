using Application.Common.Constants;
using Application.Features.RecurringTransactions.Jobs;
using Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

internal sealed class RecurringTransactionSchedulerService(
    IServiceScopeFactory                              scopeFactory,
    ILogger<RecurringTransactionSchedulerService>     logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval  = TimeSpan.FromMinutes(1);
    private static readonly int      DaysAhead     = 3;

    private DateTime _lastProcessingRun        = DateTime.MinValue;
    private DateTime _lastNotificationRun      = DateTime.MinValue;

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
                logger.LogError(ex, "Recurring transaction scheduler tick failed.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await MaybeEnqueueProcessingAsync(now, ct);
        await MaybeEnqueueNotificationsAsync(now, ct);
    }

    private async Task MaybeEnqueueProcessingAsync(DateTime now, CancellationToken ct)
    {
        // Fire once per calendar day at 00:30 UTC
        if (now.Hour != 0 || now.Minute < 30) return;
        if (_lastProcessingRun.Date == now.Date) return;

        var processingDate = DateOnly.FromDateTime(now.Date);

        await EnqueueAsync(
            JobTypes.ProcessRecurringTransactions,
            new ProcessRecurringTransactionsPayload(processingDate),
            priority: 3,
            ct);

        _lastProcessingRun = now;
        logger.LogInformation(
            "Recurring: Enqueued processing for {Date:yyyy-MM-dd}.", processingDate);
    }

    private async Task MaybeEnqueueNotificationsAsync(DateTime now, CancellationToken ct)
    {
        // Fire once per calendar day at 08:00 UTC
        if (now.Hour != 8) return;
        if (_lastNotificationRun.Date == now.Date) return;

        await EnqueueAsync(
            JobTypes.SendUpcomingPaymentNotification,
            new SendUpcomingPaymentNotificationPayload(DaysAhead),
            priority: 2,
            ct);

        _lastNotificationRun = now;
        logger.LogInformation(
            "Recurring: Enqueued upcoming payment notifications ({DaysAhead} days ahead).", DaysAhead);
    }

    private async Task EnqueueAsync<TPayload>(
        string            jobType,
        TPayload          payload,
        byte              priority,
        CancellationToken ct)
    {
        using var scope      = scopeFactory.CreateScope();
        var       jobService = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();
        await jobService.EnqueueAsync(jobType, payload, priority, ct: ct);
    }
}
