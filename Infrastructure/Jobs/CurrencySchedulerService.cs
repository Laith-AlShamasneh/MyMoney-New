using Application.Common.Constants;
using Application.Features.Currency.Jobs;
using Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

/// <summary>
/// Runs two daily scheduled currency jobs:
///   - ExchangeRateSync (02:00 UTC) — fetches latest rates from the default provider.
///   - ExchangeRateValidation (02:30 UTC) — detects stale rate pairs and logs warnings.
/// Both jobs run once per calendar day; duplicate enqueues within the same day are skipped.
/// </summary>
internal sealed class CurrencySchedulerService(
    IServiceScopeFactory             scopeFactory,
    ILogger<CurrencySchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(15);

    private DateTime _lastSyncRun       = DateTime.MinValue;
    private DateTime _lastValidationRun = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app fully start before first tick
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
                logger.LogError(ex, "Currency scheduler tick failed.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Exchange rate sync at 02:00 UTC
        if (now.Hour == 2 && now.Minute < 15 && _lastSyncRun.Date < now.Date)
        {
            await EnqueueSyncAsync(ct);
            _lastSyncRun = now;
        }

        // Validation check at 02:30 UTC
        if (now.Hour == 2 && now.Minute >= 30 && now.Minute < 45 && _lastValidationRun.Date < now.Date)
        {
            await EnqueueValidationAsync(ct);
            _lastValidationRun = now;
        }
    }

    private async Task EnqueueSyncAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var jobService = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();

            var payload = new ExchangeRateSyncPayload(
                ProviderCode:     "MANUAL",  // Replaced by active provider at runtime
                BaseCurrency:     "USD",
                TargetCurrencies: [],
                IsManualTrigger:  false);

            await jobService.EnqueueAsync(JobTypes.ExchangeRateSync, payload,
                priority: 2, ct: ct);

            logger.LogInformation("ExchangeRateSync job enqueued.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue ExchangeRateSync job.");
        }
    }

    private async Task EnqueueValidationAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var jobService = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();

            await jobService.EnqueueAsync(
                JobTypes.ExchangeRateValidation,
                new ExchangeRateValidationPayload(StaleDaysThreshold: 2),
                priority: 3,
                ct: ct);

            logger.LogInformation("ExchangeRateValidation job enqueued.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue ExchangeRateValidation job.");
        }
    }
}
