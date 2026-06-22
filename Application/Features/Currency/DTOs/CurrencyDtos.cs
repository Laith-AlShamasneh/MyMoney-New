using Shared.Enums.Finance;

namespace Application.Features.Currency.DTOs;

// ── Currency ───────────────────────────────────────────────────────────────────

public sealed record CurrencyDto(
    int     CurrencyId,
    string  Code,
    string  NameEn,
    string  NameAr,
    string  Symbol,
    string? NativeSymbol,
    int     DecimalDigits,
    string? CountryCode,
    bool    IsActive,
    bool    IsSystemBase,
    bool    IsCrypto,
    int     DisplayOrder);

// ── Exchange Rate ──────────────────────────────────────────────────────────────

public sealed record ExchangeRateDto(
    long?   RateId,
    string  FromCurrency,
    string  ToCurrency,
    decimal Rate,
    decimal InverseRate,
    string  EffectiveDate,       // yyyy-MM-dd
    string? ExpiryDate,
    byte    SourceTypeId,
    string  ProviderCode,
    string  ProviderNameEn);

public sealed record ExchangeRateListResponse(
    IReadOnlyList<ExchangeRateDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record GetExchangeRateRequest(
    string FromCurrency,
    string ToCurrency);

public sealed record GetHistoricalRateRequest(
    string FromCurrency,
    string ToCurrency,
    string AsOfDate);    // yyyy-MM-dd

public sealed record GetRateHistoryRequest(
    string? FromCurrency,
    string? ToCurrency,
    string? DateFrom,
    string? DateTo,
    int     PageNumber = 1,
    int     PageSize   = 50);

public sealed record SetManualRateRequest(
    string  FromCurrency,
    string  ToCurrency,
    decimal Rate,
    string  EffectiveDate);   // yyyy-MM-dd

public sealed record SetManualRateResponse(
    long    RateId,
    string  FromCurrency,
    string  ToCurrency,
    decimal Rate,
    string  EffectiveDate);

// ── User Currency Preferences ──────────────────────────────────────────────────

public sealed record UserCurrencyPreferencesDto(
    string BaseCurrencyCode,
    string DisplayCurrencyCode,
    byte   NumberFormatId,
    byte   SymbolStyleId,
    byte   NegativeFormatId,
    byte   CurrencyPositionId,
    string? UpdatedAt);

public sealed record UpdateUserCurrencyPreferencesRequest(
    string BaseCurrencyCode,
    string DisplayCurrencyCode,
    byte   NumberFormatId,
    byte   SymbolStyleId,
    byte   NegativeFormatId,
    byte   CurrencyPositionId);

// ── Conversion ─────────────────────────────────────────────────────────────────

public sealed record ConvertAmountRequest(
    decimal  Amount,
    string   FromCurrency,
    string   ToCurrency,
    string?  AsOfDate = null);   // yyyy-MM-dd; null = current rate

public sealed record ConvertAmountResponse(
    decimal  OriginalAmount,
    string   FromCurrency,
    decimal  ConvertedAmount,
    string   ToCurrency,
    decimal  ExchangeRate,
    string   RateEffectiveDate,
    string   SourceType,
    bool     IsIdentityConversion);

// ── Statistics ─────────────────────────────────────────────────────────────────

public sealed record ExchangeRateStatisticsDto(
    int     TotalActivePairs,
    int     TotalActiveRates,
    string? OldestRate,
    string? NewestRate,
    int     DaysSinceLastSync,
    IReadOnlyList<ProviderStatDto> Providers);

public sealed record ProviderStatDto(
    string  ProviderCode,
    string  NameEn,
    int     ActiveRateCount,
    string? LastSync);

// ── Dashboard ──────────────────────────────────────────────────────────────────

public sealed record GetCurrencyDashboardRequest(
    string? DisplayCurrencyCode = null,
    string? DateFrom            = null,
    string? DateTo              = null);

public sealed record CurrencyDashboardSummaryDto(
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetAmount,
    string  DisplayCurrencyCode,
    IReadOnlyList<CurrencyBreakdownDto> CurrencyBreakdown);

public sealed record CurrencyBreakdownDto(
    string  CurrencyCode,
    int     TransactionCount,
    decimal TotalIncome,
    decimal TotalExpenses);

// ── Sync ───────────────────────────────────────────────────────────────────────

public sealed record SyncRatesResponse(
    string  JobId,
    string  Message);
