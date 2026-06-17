using Application.Common.Constants;
using Application.Features.FinancialIntelligence.Jobs;
using Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

internal sealed class FILSchedulerService(
    IServiceScopeFactory         scopeFactory,
    ILogger<FILSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private DateTime _lastHourlyRun  = DateTime.MinValue;
    private DateTime _lastDailyRun   = DateTime.MinValue;
    private DateTime _lastMonthlyRun = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Brief startup delay so the app fully initialises before the first tick.
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
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
                logger.LogError(ex, "FIL scheduler tick failed.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await MaybeEnqueueHourlyAsync(now, ct);
        await MaybeEnqueueDailyAsync(now, ct);
        await MaybeEnqueueMonthlyAsync(now, ct);
    }

    private async Task MaybeEnqueueHourlyAsync(DateTime now, CancellationToken ct)
    {
        if ((now - _lastHourlyRun).TotalHours < 1) return;

        await EnqueueAsync(
            JobTypes.HourlyAnomalyCheck,
            new HourlyAnomalyPayload(now.AddHours(-1)),
            priority: 2,
            ct);

        _lastHourlyRun = now;
        logger.LogInformation("FIL: Enqueued hourly anomaly check (window ending {Now:u}).", now);
    }

    private async Task MaybeEnqueueDailyAsync(DateTime now, CancellationToken ct)
    {
        // Fire once per calendar day, at midnight UTC (hour 0).
        if (now.Hour != 0) return;
        if (_lastDailyRun.Date == now.Date) return;

        var target = now.Date.AddDays(-1);
        await EnqueueAsync(
            JobTypes.DailyFILProcessing,
            new DailyFILPayload(target.Year, target.Month, target.Day),
            priority: 3,
            ct);

        _lastDailyRun = now;
        logger.LogInformation("FIL: Enqueued daily processing for {Date:yyyy-MM-dd}.", target);
    }

    private async Task MaybeEnqueueMonthlyAsync(DateTime now, CancellationToken ct)
    {
        // Fire on the 1st of each month at 01:00 UTC.
        if (now.Day != 1 || now.Hour != 1) return;
        if (_lastMonthlyRun.Year == now.Year && _lastMonthlyRun.Month == now.Month) return;

        var prev = now.AddMonths(-1);
        await EnqueueAsync(
            JobTypes.MonthlyFILProcessing,
            new MonthlyFILPayload(prev.Year, prev.Month),
            priority: 3,
            ct);

        _lastMonthlyRun = now;
        logger.LogInformation("FIL: Enqueued monthly processing for {Year}-{Month:D2}.", prev.Year, prev.Month);
    }

    private async Task EnqueueAsync<TPayload>(
        string            jobType,
        TPayload          payload,
        byte              priority,
        CancellationToken ct)
    {
        using var scope  = scopeFactory.CreateScope();
        var jobService   = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();
        await jobService.EnqueueAsync(jobType, payload, priority, ct: ct);
    }
}
