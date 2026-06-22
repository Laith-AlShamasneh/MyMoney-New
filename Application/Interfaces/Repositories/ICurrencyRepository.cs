using Application.Features.Currency.DbModels;

namespace Application.Interfaces.Repositories;

public interface ICurrencyRepository
{
    // ── Currencies ────────────────────────────────────────────────────────────

    Task<IReadOnlyList<CurrencyDbModel>> GetCurrenciesAsync(
        bool includeInactive = false,
        CancellationToken ct = default);

    Task<CurrencyDbModel?> GetByCodeAsync(
        string code,
        CancellationToken ct = default);

    // ── User Preferences ──────────────────────────────────────────────────────

    Task<UserCurrencyPreferencesDbModel> GetUserPreferencesAsync(
        long userId,
        CancellationToken ct = default);

    Task<byte> UpsertUserPreferencesAsync(
        UpsertUserPreferencesDbModel model,
        CancellationToken ct = default);

    // ── Exchange Rates ────────────────────────────────────────────────────────

    Task<ExchangeRateDbModel?> GetCurrentRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken ct = default);

    Task<ExchangeRateDbModel?> GetHistoricalRateAsync(
        string   fromCurrency,
        string   toCurrency,
        DateOnly asOfDate,
        CancellationToken ct = default);

    Task<(IReadOnlyList<ExchangeRateDbModel> Items, int TotalCount)> GetRateListAsync(
        GetRateListDbModel model,
        CancellationToken ct = default);

    Task<(long RateId, byte ResultCode)> UpsertRateAsync(
        UpsertExchangeRateDbModel model,
        CancellationToken ct = default);

    Task<(int InsertedCount, int ArchivedCount)> BulkUpsertRatesAsync(
        int    providerId,
        string ratesJson,
        byte   sourceTypeId = 2,
        CancellationToken ct = default);

    Task<IReadOnlyList<StaleRatePairDbModel>> GetStalePairsAsync(
        int staleDays = 2,
        CancellationToken ct = default);

    Task<(ExchangeRateStatsSummaryDbModel Summary, IReadOnlyList<ProviderStatDbModel> Providers)>
        GetStatisticsAsync(CancellationToken ct = default);

    // ── Providers ─────────────────────────────────────────────────────────────

    Task<IReadOnlyList<ExchangeRateProviderDbModel>> GetActiveProvidersAsync(
        CancellationToken ct = default);

    // ── Conversion Log ────────────────────────────────────────────────────────

    Task<long> LogConversionAsync(
        LogConversionDbModel model,
        CancellationToken ct = default);

    // ── Dashboard ─────────────────────────────────────────────────────────────

    Task<(CurrencyDashboardSummaryDbModel Summary, IReadOnlyList<CurrencyBreakdownDbModel> Breakdown)>
        GetDashboardSummaryAsync(
            long     userId,
            string   displayCurrency,
            DateOnly? dateFrom,
            DateOnly? dateTo,
            CancellationToken ct = default);
}
