namespace Application.Features.Currency.DbModels;

// ── Currencies ─────────────────────────────────────────────────────────────────

public sealed class CurrencyDbModel
{
    public int     CurrencyId    { get; init; }
    public string  Code          { get; init; } = default!;
    public string  NameEn        { get; init; } = default!;
    public string  NameAr        { get; init; } = default!;
    public string  Symbol        { get; init; } = default!;
    public string? NativeSymbol  { get; init; }
    public byte    DecimalDigits { get; init; }
    public string? CountryCode   { get; init; }
    public bool    IsActive      { get; init; }
    public bool    IsSystemBase  { get; init; }
    public bool    IsCrypto      { get; init; }
    public short   DisplayOrder  { get; init; }
}

// ── Exchange Rates ─────────────────────────────────────────────────────────────

public sealed class ExchangeRateDbModel
{
    public long?    RateId        { get; init; }
    public string   FromCurrency  { get; init; } = default!;
    public string   ToCurrency    { get; init; } = default!;
    public decimal  Rate          { get; init; }
    public decimal  InverseRate   { get; init; }
    public DateOnly EffectiveDate { get; init; }
    public DateOnly? ExpiryDate   { get; init; }
    public byte     SourceTypeId  { get; init; }
    public byte     StatusId      { get; init; }
    public DateTime CreatedAt     { get; init; }
    public string?  ProviderCode  { get; init; }
    public string?  ProviderNameEn { get; init; }
    public int?     TotalCount    { get; init; }
}

public sealed class GetRateListDbModel
{
    public string? FromCurrency { get; init; }
    public string? ToCurrency   { get; init; }
    public byte?   StatusId     { get; init; } = 1;
    public DateOnly? DateFrom   { get; init; }
    public DateOnly? DateTo     { get; init; }
    public int     PageNumber   { get; init; } = 1;
    public int     PageSize     { get; init; } = 50;
}

public sealed class UpsertExchangeRateDbModel
{
    public string   FromCurrency  { get; init; } = default!;
    public string   ToCurrency    { get; init; } = default!;
    public decimal  Rate          { get; init; }
    public int      ProviderId    { get; init; }
    public DateOnly EffectiveDate { get; init; }
    public byte     SourceTypeId  { get; init; } = 1;
    public long?    CreatedBy     { get; init; }
}

public sealed class StaleRatePairDbModel
{
    public string   FromCurrency      { get; init; } = default!;
    public string   ToCurrency        { get; init; } = default!;
    public DateOnly LastRateDate      { get; init; }
    public int      DaysSinceUpdate   { get; init; }
}

public sealed class ExchangeRateStatsSummaryDbModel
{
    public int     TotalActivePairs   { get; init; }
    public int     TotalActiveRates   { get; init; }
    public DateOnly? OldestRate       { get; init; }
    public DateOnly? NewestRate       { get; init; }
    public int     DaysSinceLastSync  { get; init; }
}

public sealed class ProviderStatDbModel
{
    public string  ProviderCode      { get; init; } = default!;
    public string  NameEn            { get; init; } = default!;
    public int     ActiveRateCount   { get; init; }
    public DateOnly? LastSync        { get; init; }
}

// ── Providers ─────────────────────────────────────────────────────────────────

public sealed class ExchangeRateProviderDbModel
{
    public int     ProviderId  { get; init; }
    public string  Code        { get; init; } = default!;
    public string  NameEn      { get; init; } = default!;
    public string  NameAr      { get; init; } = default!;
    public bool    IsDefault   { get; init; }
    public string? ApiBaseUrl  { get; init; }
    public byte    Priority    { get; init; }
}

// ── User Currency Preferences ─────────────────────────────────────────────────

public sealed class UserCurrencyPreferencesDbModel
{
    public long   UserId              { get; init; }
    public string BaseCurrencyCode    { get; init; } = "USD";
    public string DisplayCurrencyCode { get; init; } = "USD";
    public byte   NumberFormatId      { get; init; } = 1;
    public byte   SymbolStyleId       { get; init; } = 1;
    public byte   NegativeFormatId    { get; init; } = 1;
    public byte   CurrencyPositionId  { get; init; } = 1;
    public DateTime? UpdatedAt        { get; init; }
}

public sealed class UpsertUserPreferencesDbModel
{
    public long   UserId              { get; init; }
    public string BaseCurrencyCode    { get; init; } = default!;
    public string DisplayCurrencyCode { get; init; } = default!;
    public byte   NumberFormatId      { get; init; }
    public byte   SymbolStyleId       { get; init; }
    public byte   NegativeFormatId    { get; init; }
    public byte   CurrencyPositionId  { get; init; }
}

// ── Conversion Log ────────────────────────────────────────────────────────────

public sealed class LogConversionDbModel
{
    public long    UserId             { get; init; }
    public string  EntityType         { get; init; } = default!;
    public long    EntityId           { get; init; }
    public string  FromCurrency       { get; init; } = default!;
    public string  ToCurrency         { get; init; } = default!;
    public decimal OriginalAmount     { get; init; }
    public decimal ConvertedAmount    { get; init; }
    public decimal ExchangeRate       { get; init; }
    public long?   RateId             { get; init; }
    public DateOnly RateEffectiveDate { get; init; }
    public byte    ConversionModeId   { get; init; }
}

// ── Dashboard ─────────────────────────────────────────────────────────────────

public sealed class CurrencyDashboardSummaryDbModel
{
    public decimal TotalIncome         { get; init; }
    public decimal TotalExpenses       { get; init; }
    public decimal NetAmount           { get; init; }
    public string  DisplayCurrencyCode { get; init; } = default!;
}

public sealed class CurrencyBreakdownDbModel
{
    public string  CurrencyCode      { get; init; } = default!;
    public int     TransactionCount  { get; init; }
    public decimal TotalIncome       { get; init; }
    public decimal TotalExpenses     { get; init; }
}
