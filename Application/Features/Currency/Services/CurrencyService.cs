using Application.Common.Constants;
using Application.Features.Currency.DbModels;
using Application.Features.Currency.DTOs;
using Application.Features.Currency.Jobs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.Finance;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.Currency.Services;

internal sealed class CurrencyService(
    ICurrencyRepository         currencyRepository,
    ICurrencyConversionService  conversionService,
    IUserContext                userContext,
    IMessageProvider            messageProvider,
    IBackgroundJobService       backgroundJobService) : ICurrencyService
{
    // ── Currencies ────────────────────────────────────────────────────────────

    public async Task<ServiceResult<IReadOnlyList<CurrencyDto>>> GetSupportedCurrenciesAsync(
        bool includeInactive = false, CancellationToken ct = default)
    {
        var db  = await currencyRepository.GetCurrenciesAsync(includeInactive, ct);
        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Currency.CurrenciesLoaded, ct);
        return ServiceResultFactory.Success<IReadOnlyList<CurrencyDto>>(db.Select(Map).ToList(), InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<CurrencyDto>> GetByCodeAsync(
        string code, CancellationToken ct = default)
    {
        var db = await currencyRepository.GetByCodeAsync(code.ToUpperInvariant(), ct);
        if (db is null)
            return ServiceResultFactory.Failure<CurrencyDto>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Currency.CurrencyNotFound, ct));

        return ServiceResultFactory.Success(Map(db), InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Common.Success, ct));
    }

    // ── User Preferences ──────────────────────────────────────────────────────

    public async Task<ServiceResult<UserCurrencyPreferencesDto>> GetUserPreferencesAsync(
        CancellationToken ct = default)
    {
        var db  = await currencyRepository.GetUserPreferencesAsync(userContext.UserId, ct);
        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Currency.PreferencesLoaded, ct);
        return ServiceResultFactory.Success(MapPrefs(db), InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<UserCurrencyPreferencesDto>> UpdateUserPreferencesAsync(
        UpdateUserCurrencyPreferencesRequest request, CancellationToken ct = default)
    {
        var model = new UpsertUserPreferencesDbModel
        {
            UserId              = userContext.UserId,
            BaseCurrencyCode    = request.BaseCurrencyCode.ToUpperInvariant(),
            DisplayCurrencyCode = request.DisplayCurrencyCode.ToUpperInvariant(),
            NumberFormatId      = request.NumberFormatId,
            SymbolStyleId       = request.SymbolStyleId,
            NegativeFormatId    = request.NegativeFormatId,
            CurrencyPositionId  = request.CurrencyPositionId,
        };

        var resultCode = await currencyRepository.UpsertUserPreferencesAsync(model, ct);

        if (resultCode == 1)
            return ServiceResultFactory.Failure<UserCurrencyPreferencesDto>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Currency.CurrencyNotFound, ct));

        var updated = await currencyRepository.GetUserPreferencesAsync(userContext.UserId, ct);
        return ServiceResultFactory.Success(MapPrefs(updated), InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Currency.PreferencesUpdated, ct));
    }

    // ── Exchange Rates ────────────────────────────────────────────────────────

    public async Task<ServiceResult<ExchangeRateDto>> GetCurrentRateAsync(
        GetExchangeRateRequest request, CancellationToken ct = default)
    {
        var db = await currencyRepository.GetCurrentRateAsync(
            request.FromCurrency.ToUpperInvariant(),
            request.ToCurrency.ToUpperInvariant(), ct);

        if (db is null)
            return ServiceResultFactory.Failure<ExchangeRateDto>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Currency.ExchangeRateNotFound, ct));

        return ServiceResultFactory.Success(MapRate(db), InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Common.Success, ct));
    }

    public async Task<ServiceResult<ExchangeRateDto>> GetHistoricalRateAsync(
        GetHistoricalRateRequest request, CancellationToken ct = default)
    {
        if (!DateOnly.TryParse(request.AsOfDate, out var asOf))
            return ServiceResultFactory.Failure<ExchangeRateDto>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Currency.InvalidDate, ct));

        var db = await currencyRepository.GetHistoricalRateAsync(
            request.FromCurrency.ToUpperInvariant(),
            request.ToCurrency.ToUpperInvariant(), asOf, ct);

        if (db is null)
            return ServiceResultFactory.Failure<ExchangeRateDto>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Currency.ExchangeRateNotFound, ct));

        return ServiceResultFactory.Success(MapRate(db), InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Common.Success, ct));
    }

    public async Task<ServiceResult<ExchangeRateListResponse>> GetRateHistoryAsync(
        GetRateHistoryRequest request, CancellationToken ct = default)
    {
        var model = new GetRateListDbModel
        {
            FromCurrency = request.FromCurrency?.ToUpperInvariant(),
            ToCurrency   = request.ToCurrency?.ToUpperInvariant(),
            DateFrom     = DateOnly.TryParse(request.DateFrom, out var df) ? df : null,
            DateTo       = DateOnly.TryParse(request.DateTo,   out var dt) ? dt : null,
            PageNumber   = request.PageNumber,
            PageSize     = request.PageSize,
        };

        var (items, total) = await currencyRepository.GetRateListAsync(model, ct);
        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Currency.RatesLoaded, ct);
        return ServiceResultFactory.Success(
            new ExchangeRateListResponse(items.Select(MapRate).ToList(), total, request.PageNumber, request.PageSize),
            InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<SetManualRateResponse>> SetManualRateAsync(
        SetManualRateRequest request, CancellationToken ct = default)
    {
        if (!DateOnly.TryParse(request.EffectiveDate, out var effDate))
            return ServiceResultFactory.Failure<SetManualRateResponse>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Currency.InvalidDate, ct));

        var providers      = await currencyRepository.GetActiveProvidersAsync(ct);
        var manualProvider = providers.FirstOrDefault(p => p.Code == "MANUAL");
        if (manualProvider is null)
            return ServiceResultFactory.Failure<SetManualRateResponse>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Currency.InvalidProvider, ct));

        var model = new UpsertExchangeRateDbModel
        {
            FromCurrency  = request.FromCurrency.ToUpperInvariant(),
            ToCurrency    = request.ToCurrency.ToUpperInvariant(),
            Rate          = request.Rate,
            ProviderId    = manualProvider.ProviderId,
            EffectiveDate = effDate,
            SourceTypeId  = (byte)ExchangeRateSourceType.Manual,
            CreatedBy     = userContext.UserId,
        };

        var (rateId, resultCode) = await currencyRepository.UpsertRateAsync(model, ct);

        if (resultCode != 0)
        {
            var errKey = resultCode switch
            {
                1 => MessageKeys.Currency.CurrencyNotFound,
                2 => MessageKeys.Currency.RateMustBePositive,
                _ => MessageKeys.Currency.InvalidProvider,
            };
            return ServiceResultFactory.Failure<SetManualRateResponse>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(errKey, ct));
        }

        return ServiceResultFactory.Success(
            new SetManualRateResponse(rateId, request.FromCurrency.ToUpperInvariant(),
                request.ToCurrency.ToUpperInvariant(), request.Rate, effDate.ToString("yyyy-MM-dd")),
            InternalResponseCodes.Created,
            await messageProvider.GetMessagesAsync(MessageKeys.Currency.RateSet, ct));
    }

    // ── Conversion ────────────────────────────────────────────────────────────

    public async Task<ServiceResult<ConvertAmountResponse>> ConvertAmountAsync(
        ConvertAmountRequest request, CancellationToken ct = default)
    {
        var from = request.FromCurrency.ToUpperInvariant();
        var to   = request.ToCurrency.ToUpperInvariant();

        ConversionResult result;
        if (request.AsOfDate is not null && DateOnly.TryParse(request.AsOfDate, out var asOf))
            result = await conversionService.ConvertHistoricalAsync(request.Amount, from, to, asOf, ct);
        else
            result = await conversionService.ConvertAsync(request.Amount, from, to, ct);

        if (!result.Succeeded)
            return ServiceResultFactory.Failure<ConvertAmountResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Currency.ExchangeRateNotFound, ct));

        return ServiceResultFactory.Success(
            new ConvertAmountResponse(
                result.OriginalAmount, result.FromCurrency,
                result.ConvertedAmount, result.ToCurrency,
                result.ExchangeRate,
                result.RateEffectiveDate.ToString("yyyy-MM-dd"),
                result.SourceType.ToString(),
                result.IsIdentityConversion),
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Currency.ConversionSuccess, ct));
    }

    // ── Statistics & Dashboard ────────────────────────────────────────────────

    public async Task<ServiceResult<ExchangeRateStatisticsDto>> GetStatisticsAsync(
        CancellationToken ct = default)
    {
        var (summary, providers) = await currencyRepository.GetStatisticsAsync(ct);
        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Currency.StatisticsLoaded, ct);

        var dto = new ExchangeRateStatisticsDto(
            summary.TotalActivePairs,
            summary.TotalActiveRates,
            summary.OldestRate?.ToString("yyyy-MM-dd"),
            summary.NewestRate?.ToString("yyyy-MM-dd"),
            summary.DaysSinceLastSync,
            providers.Select(p => new ProviderStatDto(
                p.ProviderCode, p.NameEn, p.ActiveRateCount,
                p.LastSync?.ToString("yyyy-MM-dd"))).ToList());

        return ServiceResultFactory.Success(dto, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<CurrencyDashboardSummaryDto>> GetDashboardSummaryAsync(
        GetCurrencyDashboardRequest request, CancellationToken ct = default)
    {
        string displayCurrency;
        if (!string.IsNullOrWhiteSpace(request.DisplayCurrencyCode))
        {
            displayCurrency = request.DisplayCurrencyCode.ToUpperInvariant();
        }
        else
        {
            var prefs = await currencyRepository.GetUserPreferencesAsync(userContext.UserId, ct);
            displayCurrency = prefs.DisplayCurrencyCode;
        }

        DateOnly? dateFrom = DateOnly.TryParse(request.DateFrom, out var df2) ? df2 : null;
        DateOnly? dateTo   = DateOnly.TryParse(request.DateTo,   out var dt2) ? dt2 : null;

        var (summary, breakdown) = await currencyRepository.GetDashboardSummaryAsync(
            userContext.UserId, displayCurrency, dateFrom, dateTo, ct);

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Currency.DashboardLoaded, ct);
        var dto = new CurrencyDashboardSummaryDto(
            summary.TotalIncome, summary.TotalExpenses, summary.NetAmount,
            summary.DisplayCurrencyCode,
            breakdown.Select(b => new CurrencyBreakdownDto(
                b.CurrencyCode, b.TransactionCount, b.TotalIncome, b.TotalExpenses)).ToList());

        return ServiceResultFactory.Success(dto, InternalResponseCodes.OK, msg);
    }

    // ── Sync ──────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<SyncRatesResponse>> TriggerRateSyncAsync(
        CancellationToken ct = default)
    {
        var providers = await currencyRepository.GetActiveProvidersAsync(ct);
        var provider  = providers.FirstOrDefault(p => p.IsDefault) ?? providers.FirstOrDefault();

        if (provider is null)
            return ServiceResultFactory.Failure<SyncRatesResponse>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Currency.InvalidProvider, ct));

        var payload = new ExchangeRateSyncPayload(
            ProviderCode:     provider.Code,
            BaseCurrency:     "USD",
            TargetCurrencies: [],
            IsManualTrigger:  true);

        await backgroundJobService.EnqueueAsync(JobTypes.ExchangeRateSync, payload, priority: 1, ct: ct);

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Currency.SyncTriggered, ct);
        return ServiceResultFactory.Success(new SyncRatesResponse(msg, msg), InternalResponseCodes.Accepted, msg);
    }

    // ── Private mappers ───────────────────────────────────────────────────────

    private static CurrencyDto Map(CurrencyDbModel db) => new(
        db.CurrencyId, db.Code, db.NameEn, db.NameAr,
        db.Symbol, db.NativeSymbol, db.DecimalDigits,
        db.CountryCode, db.IsActive, db.IsSystemBase, db.IsCrypto, db.DisplayOrder);

    private static UserCurrencyPreferencesDto MapPrefs(UserCurrencyPreferencesDbModel db) => new(
        db.BaseCurrencyCode, db.DisplayCurrencyCode,
        db.NumberFormatId, db.SymbolStyleId, db.NegativeFormatId, db.CurrencyPositionId,
        db.UpdatedAt?.ToString("yyyy-MM-ddTHH:mm:ssZ"));

    private static ExchangeRateDto MapRate(ExchangeRateDbModel db) => new(
        db.RateId, db.FromCurrency, db.ToCurrency, db.Rate, db.InverseRate,
        db.EffectiveDate.ToString("yyyy-MM-dd"),
        db.ExpiryDate?.ToString("yyyy-MM-dd"),
        db.SourceTypeId,
        db.ProviderCode ?? "MANUAL",
        db.ProviderNameEn ?? "Manual");
}
