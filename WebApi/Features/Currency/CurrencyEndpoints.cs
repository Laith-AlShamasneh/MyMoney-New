using Application.Features.Currency.DTOs;
using Application.Interfaces.Services;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Currency;

public static class CurrencyEndpoints
{
    public static void MapCurrencyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/currency")
                       .WithTags("Currency")
                       .RequireAuthorization();

        // ── Currencies ────────────────────────────────────────────────────────

        /// <summary>Returns all supported ISO 4217 currencies.</summary>
        group.MapPost("list", async (
            ICurrencyService  service,
            CancellationToken ct) =>
        {
            var result = await service.GetSupportedCurrenciesAsync(ct: ct);
            return result.ToHttpResponse();
        });

        /// <summary>Returns a single currency by its ISO code.</summary>
        group.MapPost("get", async (
            GetExchangeRateRequest request,
            ICurrencyService       service,
            CancellationToken      ct) =>
        {
            // Reuse GetExchangeRateRequest.FromCurrency as the code to look up
            var result = await service.GetByCodeAsync(request.FromCurrency, ct);
            return result.ToHttpResponse();
        });

        // ── User Preferences ──────────────────────────────────────────────────

        /// <summary>Returns the caller's currency preferences.</summary>
        group.MapPost("preferences/get", async (
            ICurrencyService  service,
            CancellationToken ct) =>
        {
            var result = await service.GetUserPreferencesAsync(ct);
            return result.ToHttpResponse();
        });

        /// <summary>Creates or updates the caller's currency preferences.</summary>
        group.MapPost("preferences/update", async (
            UpdateUserCurrencyPreferencesRequest request,
            ICurrencyService                     service,
            CancellationToken                    ct) =>
        {
            var result = await service.UpdateUserPreferencesAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<UpdateUserCurrencyPreferencesRequest>>();

        // ── Exchange Rates ────────────────────────────────────────────────────

        /// <summary>Returns the current exchange rate for a currency pair.</summary>
        group.MapPost("rates/current", async (
            GetExchangeRateRequest request,
            ICurrencyService       service,
            CancellationToken      ct) =>
        {
            var result = await service.GetCurrentRateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetExchangeRateRequest>>();

        /// <summary>Returns the exchange rate effective on a specific historical date.</summary>
        group.MapPost("rates/historical", async (
            GetHistoricalRateRequest request,
            ICurrencyService         service,
            CancellationToken        ct) =>
        {
            var result = await service.GetHistoricalRateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetHistoricalRateRequest>>();

        /// <summary>Returns paginated exchange rate history with optional filters.</summary>
        group.MapPost("rates/history", async (
            GetRateHistoryRequest request,
            ICurrencyService      service,
            CancellationToken     ct) =>
        {
            var result = await service.GetRateHistoryAsync(request, ct);
            return result.ToHttpResponse();
        });

        /// <summary>
        /// Sets a manual exchange rate for a currency pair.
        /// Existing active rate is archived; new rate becomes active.
        /// </summary>
        group.MapPost("rates/set", async (
            SetManualRateRequest request,
            ICurrencyService     service,
            CancellationToken    ct) =>
        {
            var result = await service.SetManualRateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<SetManualRateRequest>>();

        /// <summary>Returns exchange rate system statistics.</summary>
        group.MapPost("rates/statistics", async (
            ICurrencyService  service,
            CancellationToken ct) =>
        {
            var result = await service.GetStatisticsAsync(ct);
            return result.ToHttpResponse();
        });

        /// <summary>Triggers an immediate exchange rate sync job.</summary>
        group.MapPost("rates/sync", async (
            ICurrencyService  service,
            CancellationToken ct) =>
        {
            var result = await service.TriggerRateSyncAsync(ct);
            return result.ToHttpResponse();
        });

        // ── Conversion ────────────────────────────────────────────────────────

        /// <summary>
        /// Converts an amount between currencies.
        /// Pass AsOfDate (yyyy-MM-dd) to use historical rate.
        /// </summary>
        group.MapPost("convert", async (
            ConvertAmountRequest request,
            ICurrencyService     service,
            CancellationToken    ct) =>
        {
            var result = await service.ConvertAmountAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<ConvertAmountRequest>>();

        // ── Dashboard ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a currency-aware financial summary for the dashboard.
        /// All totals are expressed in the user's display currency.
        /// </summary>
        group.MapPost("dashboard", async (
            GetCurrencyDashboardRequest request,
            ICurrencyService            service,
            CancellationToken           ct) =>
        {
            var result = await service.GetDashboardSummaryAsync(request, ct);
            return result.ToHttpResponse();
        });
    }
}
