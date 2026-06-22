namespace Application.Interfaces.Services;

/// <summary>
/// Abstraction for external exchange-rate data sources.
/// Implementations are provider-specific (ECB, Open Exchange Rates, Fixer, etc.).
/// The system never couples business logic to a specific provider.
/// </summary>
public interface IExchangeRateProvider
{
    /// <summary>Unique provider code matching ExchangeRateProviders.Code in the database.</summary>
    string ProviderCode { get; }

    /// <summary>
    /// Fetches the latest rates for all supported pairs relative to the base currency.
    /// Returns an empty list on failure rather than throwing — callers log and fall back.
    /// </summary>
    Task<IReadOnlyList<ExchangeRateData>> GetLatestRatesAsync(
        string baseCurrency, CancellationToken ct = default);

    /// <summary>Returns true when the provider can serve rates for this pair.</summary>
    bool SupportsPair(string fromCurrency, string toCurrency);
}

/// <summary>
/// Lightweight DTO exchanged between provider implementations and the sync engine.
/// Intentionally free of database concerns.
/// </summary>
public sealed record ExchangeRateData(
    string   FromCurrency,
    string   ToCurrency,
    decimal  Rate,
    DateOnly EffectiveDate);
