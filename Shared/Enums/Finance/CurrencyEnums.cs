namespace Shared.Enums.Finance;

public enum ExchangeRateSourceType : byte
{
    Manual    = 1,  // Entered by admin/user
    Automatic = 2,  // Fetched from external provider
    Estimated = 3   // Calculated via cross-rate pivot
}

public enum ExchangeRateStatus : byte
{
    Active   = 1,
    Draft    = 2,
    Archived = 3
}

public enum ConversionMode : byte
{
    Historical = 1,  // Use rate that was effective at transaction date
    Current    = 2,  // Use today's rate
    Manual     = 3   // Rate was manually specified by user
}

public enum NumberFormat : byte
{
    CommaThousandPeriodDecimal  = 1,  // 1,234.56  (US/UK)
    PeriodThousandCommaDecimal  = 2,  // 1.234,56  (DE/AR)
    SpaceThousandCommaDecimal   = 3,  // 1 234,56  (FR)
    SpaceThousandPeriodDecimal  = 4   // 1 234.56  (CH)
}

public enum CurrencySymbolStyle : byte
{
    Symbol  = 1,  // $
    Code    = 2,  // USD
    Both    = 3   // $ USD
}

public enum NegativeNumberFormat : byte
{
    PrefixMinus   = 1,  // -1,000.00
    Parentheses   = 2,  // (1,000.00)
    SuffixMinus   = 3   // 1,000.00-
}

public enum CurrencyPosition : byte
{
    Before = 1,  // $ 1,000.00
    After  = 2   // 1,000.00 $
}
