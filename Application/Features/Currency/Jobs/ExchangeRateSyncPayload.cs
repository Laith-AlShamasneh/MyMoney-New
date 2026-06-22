namespace Application.Features.Currency.Jobs;

/// <summary>Payload for the ExchangeRateSync background job.</summary>
public sealed record ExchangeRateSyncPayload(
    string   ProviderCode,
    string   BaseCurrency,
    string[] TargetCurrencies,
    bool     IsManualTrigger = false);

/// <summary>Payload for the ExchangeRateValidation background job.</summary>
public sealed record ExchangeRateValidationPayload(
    int StaleDaysThreshold = 2);
