using Application.Features.Currency.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Currency.Validators;

public sealed class UpdateUserCurrencyPreferencesValidator
    : AbstractValidator<UpdateUserCurrencyPreferencesRequest>
{
    public UpdateUserCurrencyPreferencesValidator()
    {
        RuleFor(x => x.BaseCurrencyCode)
            .NotEmpty().WithMessage(MessageKeys.Currency.CurrencyCodeRequired)
            .MaximumLength(10).WithMessage(MessageKeys.Currency.InvalidCurrencyCode);

        RuleFor(x => x.DisplayCurrencyCode)
            .NotEmpty().WithMessage(MessageKeys.Currency.CurrencyCodeRequired)
            .MaximumLength(10).WithMessage(MessageKeys.Currency.InvalidCurrencyCode);

        RuleFor(x => x.NumberFormatId)
            .InclusiveBetween((byte)1, (byte)4).WithMessage(MessageKeys.Currency.InvalidPreference);

        RuleFor(x => x.SymbolStyleId)
            .InclusiveBetween((byte)1, (byte)3).WithMessage(MessageKeys.Currency.InvalidPreference);

        RuleFor(x => x.NegativeFormatId)
            .InclusiveBetween((byte)1, (byte)3).WithMessage(MessageKeys.Currency.InvalidPreference);

        RuleFor(x => x.CurrencyPositionId)
            .InclusiveBetween((byte)1, (byte)2).WithMessage(MessageKeys.Currency.InvalidPreference);
    }
}

public sealed class GetExchangeRateValidator : AbstractValidator<GetExchangeRateRequest>
{
    public GetExchangeRateValidator()
    {
        RuleFor(x => x.FromCurrency)
            .NotEmpty().WithMessage(MessageKeys.Currency.CurrencyCodeRequired)
            .MaximumLength(10).WithMessage(MessageKeys.Currency.InvalidCurrencyCode);

        RuleFor(x => x.ToCurrency)
            .NotEmpty().WithMessage(MessageKeys.Currency.CurrencyCodeRequired)
            .MaximumLength(10).WithMessage(MessageKeys.Currency.InvalidCurrencyCode);
    }
}

public sealed class GetHistoricalRateValidator : AbstractValidator<GetHistoricalRateRequest>
{
    public GetHistoricalRateValidator()
    {
        RuleFor(x => x.FromCurrency)
            .NotEmpty().WithMessage(MessageKeys.Currency.CurrencyCodeRequired)
            .MaximumLength(10).WithMessage(MessageKeys.Currency.InvalidCurrencyCode);

        RuleFor(x => x.ToCurrency)
            .NotEmpty().WithMessage(MessageKeys.Currency.CurrencyCodeRequired)
            .MaximumLength(10).WithMessage(MessageKeys.Currency.InvalidCurrencyCode);

        RuleFor(x => x.AsOfDate)
            .NotEmpty().WithMessage(MessageKeys.Currency.DateRequired)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage(MessageKeys.Currency.InvalidDate);
    }
}

public sealed class SetManualRateValidator : AbstractValidator<SetManualRateRequest>
{
    public SetManualRateValidator()
    {
        RuleFor(x => x.FromCurrency)
            .NotEmpty().WithMessage(MessageKeys.Currency.CurrencyCodeRequired)
            .MaximumLength(10).WithMessage(MessageKeys.Currency.InvalidCurrencyCode);

        RuleFor(x => x.ToCurrency)
            .NotEmpty().WithMessage(MessageKeys.Currency.CurrencyCodeRequired)
            .MaximumLength(10).WithMessage(MessageKeys.Currency.InvalidCurrencyCode);

        RuleFor(x => x)
            .Must(x => x.FromCurrency != x.ToCurrency)
            .WithMessage(MessageKeys.Currency.SameCurrencyConversion);

        RuleFor(x => x.Rate)
            .GreaterThan(0).WithMessage(MessageKeys.Currency.RateMustBePositive);

        RuleFor(x => x.EffectiveDate)
            .NotEmpty().WithMessage(MessageKeys.Currency.DateRequired)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage(MessageKeys.Currency.InvalidDate);
    }
}

public sealed class ConvertAmountValidator : AbstractValidator<ConvertAmountRequest>
{
    public ConvertAmountValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage(MessageKeys.Currency.AmountMustBeNonNegative);

        RuleFor(x => x.FromCurrency)
            .NotEmpty().WithMessage(MessageKeys.Currency.CurrencyCodeRequired)
            .MaximumLength(10).WithMessage(MessageKeys.Currency.InvalidCurrencyCode);

        RuleFor(x => x.ToCurrency)
            .NotEmpty().WithMessage(MessageKeys.Currency.CurrencyCodeRequired)
            .MaximumLength(10).WithMessage(MessageKeys.Currency.InvalidCurrencyCode);

        When(x => x.AsOfDate is not null, () =>
        {
            RuleFor(x => x.AsOfDate!)
                .Must(d => DateOnly.TryParse(d, out _)).WithMessage(MessageKeys.Currency.InvalidDate);
        });
    }
}
