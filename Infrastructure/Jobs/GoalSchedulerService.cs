using Application.Common.Constants;
using Application.Features.Goals.Jobs;
using Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

internal sealed class GoalSchedulerService(
    IServiceScopeFactory          scopeFactory,
    ILogger<GoalSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private DateTime _lastBehindScheduleRun  = DateTime.MinValue;
    private DateTime _lastAutoContributionRun = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                logger.LogError(ex, "Goal scheduler tick failed.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await MaybeEnqueueAutoContributionSyncAsync(now, ct);
        await MaybeEnqueueBehindScheduleCheckAsync(now, ct);
    }

    // Runs at 01:00 UTC — 30 min after recurring transactions (00:30 UTC)
    private async Task MaybeEnqueueAutoContributionSyncAsync(DateTime now, CancellationToken ct)
    {
        if (now.Hour != 1) return;
        if (_lastAutoContributionRun.Date == now.Date) return;

        var yesterday = DateOnly.FromDateTime(now.Date.AddDays(-1));
        await EnqueueAsync(
            JobTypes.GoalAutoContributionSync,
            new GoalAutoContributionSyncPayload(yesterday),
            priority: 2,
            ct);

        _lastAutoContributionRun = now;
        logger.LogInformation("Goals: Enqueued auto-contribution sync for {Date:yyyy-MM-dd}.", yesterday);
    }

    // Behind-schedule check runs at 09:00 UTC daily
    private async Task MaybeEnqueueBehindScheduleCheckAsync(DateTime now, CancellationToken ct)
    {
        if (now.Hour != 9) return;
        if (_lastBehindScheduleRun.Date == now.Date) return;

        await EnqueueAsync(
            JobTypes.GoalBehindScheduleCheck,
            new GoalBehindScheduleCheckPayload(),
            priority: 2,
            ct);

        _lastBehindScheduleRun = now;
        logger.LogInformation("Goals: Enqueued behind-schedule check.");
    }

    private async Task EnqueueAsync<TPayload>(
        string            jobType,
        TPayload          payload,
        byte              priority,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var jobService  = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();
        await jobService.EnqueueAsync(jobType, payload, priority, ct: ct);
    }
}
