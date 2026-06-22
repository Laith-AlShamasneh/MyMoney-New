using Application.Features.Currency.DTOs;
using Shared.Results;

namespace Application.Interfaces.Services;

public interface ICurrencyService
{
    // ── Currencies ────────────────────────────────────────────────────────────

    Task<ServiceResult<IReadOnlyList<CurrencyDto>>> GetSupportedCurrenciesAsync(
        bool includeInactive = false,
        CancellationToken ct = default);

    Task<ServiceResult<CurrencyDto>> GetByCodeAsync(
        string code,
        CancellationToken ct = default);

    // ── User Preferences ──────────────────────────────────────────────────────

    Task<ServiceResult<UserCurrencyPreferencesDto>> GetUserPreferencesAsync(
        CancellationToken ct = default);

    Task<ServiceResult<UserCurrencyPreferencesDto>> UpdateUserPreferencesAsync(
        UpdateUserCurrencyPreferencesRequest request,
        CancellationToken ct = default);

    // ── Exchange Rates ────────────────────────────────────────────────────────

    Task<ServiceResult<ExchangeRateDto>> GetCurrentRateAsync(
        GetExchangeRateRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<ExchangeRateDto>> GetHistoricalRateAsync(
        GetHistoricalRateRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<ExchangeRateListResponse>> GetRateHistoryAsync(
        GetRateHistoryRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<SetManualRateResponse>> SetManualRateAsync(
        SetManualRateRequest request,
        CancellationToken ct = default);

    // ── Conversion ────────────────────────────────────────────────────────────

    Task<ServiceResult<ConvertAmountResponse>> ConvertAmountAsync(
        ConvertAmountRequest request,
        CancellationToken ct = default);

    // ── Statistics & Dashboard ────────────────────────────────────────────────

    Task<ServiceResult<ExchangeRateStatisticsDto>> GetStatisticsAsync(
        CancellationToken ct = default);

    Task<ServiceResult<CurrencyDashboardSummaryDto>> GetDashboardSummaryAsync(
        GetCurrencyDashboardRequest request,
        CancellationToken ct = default);

    // ── Sync ──────────────────────────────────────────────────────────────────

    Task<ServiceResult<SyncRatesResponse>> TriggerRateSyncAsync(
        CancellationToken ct = default);
}
