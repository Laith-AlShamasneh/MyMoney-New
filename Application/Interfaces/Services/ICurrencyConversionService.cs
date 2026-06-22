using Shared.Enums.Finance;

namespace Application.Interfaces.Services;

/// <summary>
/// Centralized conversion engine. The ONLY place conversion math is performed.
/// All other services call this interface — never implement conversion inline.
/// Precision: uses decimal throughout. Never uses double or float.
/// Rounding: MidpointRounding.ToEven (banker's rounding) for financial accuracy.
/// </summary>
public interface ICurrencyConversionService
{
    /// <summary>
    /// Converts an amount using the most recent available rate.
    /// Returns the original amount if currencies are the same.
    /// </summary>
    Task<ConversionResult> ConvertAsync(
        decimal amount,
        string  fromCurrency,
        string  toCurrency,
        CancellationToken ct = default);

    /// <summary>
    /// Converts an amount using the rate effective on the given date.
    /// Used for historical reports and audit — never updates stored rates.
    /// </summary>
    Task<ConversionResult> ConvertHistoricalAsync(
        decimal  amount,
        string   fromCurrency,
        string   toCurrency,
        DateOnly asOfDate,
        CancellationToken ct = default);

    /// <summary>
    /// Batch-converts a list of amounts to a common target currency.
    /// More efficient than N individual calls — resolves rates once.
    /// </summary>
    Task<IReadOnlyList<ConversionResult>> ConvertBatchAsync(
        IReadOnlyList<ConversionRequest> requests,
        string targetCurrency,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the current rate without performing a conversion.
    /// Returns null when no rate is available.
    /// </summary>
    Task<RateSnapshot?> GetCurrentRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the rate effective on the given date.
    /// Returns null when no historical rate is available.
    /// </summary>
    Task<RateSnapshot?> GetHistoricalRateAsync(
        string   fromCurrency,
        string   toCurrency,
        DateOnly asOfDate,
        CancellationToken ct = default);
}

public sealed record ConversionRequest(
    decimal  Amount,
    string   FromCurrency,
    string?  EntityType = null,
    long?    EntityId   = null);

public sealed record ConversionResult(
    decimal         OriginalAmount,
    string          FromCurrency,
    decimal         ConvertedAmount,
    string          ToCurrency,
    decimal         ExchangeRate,
    DateOnly        RateEffectiveDate,
    long?           RateId,
    ExchangeRateSourceType SourceType,
    bool            IsIdentityConversion  // true when From == To
);

public sealed record RateSnapshot(
    string   FromCurrency,
    string   ToCurrency,
    decimal  Rate,
    decimal  InverseRate,
    DateOnly EffectiveDate,
    long?    RateId,
    ExchangeRateSourceType SourceType);
