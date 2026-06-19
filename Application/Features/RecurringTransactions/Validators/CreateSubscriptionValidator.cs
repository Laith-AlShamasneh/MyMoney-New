using Application.Features.RecurringTransactions.DTOs;
using FluentValidation;
using Shared.Constants;
using Shared.Enums.RecurringTransactions;

namespace Application.Features.RecurringTransactions.Validators;

public sealed class CreateSubscriptionValidator : AbstractValidator<CreateSubscriptionRequest>
{
    public CreateSubscriptionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(MessageKeys.RecurringTransaction.NameRequired)
            .MaximumLength(200)
            .WithMessage(MessageKeys.RecurringTransaction.NameTooLong);

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage(MessageKeys.RecurringTransaction.AmountMustBePositive);

        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
            .WithMessage(MessageKeys.RecurringTransaction.CategoryRequired);

        RuleFor(x => x.FrequencyId)
            .InclusiveBetween(1, 6)
            .WithMessage(MessageKeys.RecurringTransaction.InvalidFrequency);

        RuleFor(x => x.StartDate)
            .NotEmpty()
            .WithMessage(MessageKeys.RecurringTransaction.StartDateRequired)
            .Must(s => DateOnly.TryParse(s, out _))
            .WithMessage(MessageKeys.RecurringTransaction.InvalidStartDate);

        RuleFor(x => x.FrequencyInterval)
            .NotNull()
            .WithMessage(MessageKeys.RecurringTransaction.CustomIntervalRequired)
            .GreaterThan(0)
            .WithMessage(MessageKeys.RecurringTransaction.CustomIntervalMustBePositive)
            .When(x => x.FrequencyId == (int)RecurrenceFrequency.Custom);

        RuleFor(x => x.FrequencyUnit)
            .NotNull()
            .WithMessage(MessageKeys.RecurringTransaction.CustomUnitRequired)
            .InclusiveBetween(1, 4)
            .WithMessage(MessageKeys.RecurringTransaction.CustomUnitRequired)
            .When(x => x.FrequencyId == (int)RecurrenceFrequency.Custom);

        RuleFor(x => x.ProviderName)
            .NotEmpty()
            .WithMessage(MessageKeys.Subscription.ProviderNameRequired)
            .MaximumLength(200)
            .WithMessage(MessageKeys.Subscription.ProviderNameTooLong);

        RuleFor(x => x.WebsiteUrl)
            .MaximumLength(500)
            .WithMessage(MessageKeys.Subscription.WebsiteUrlTooLong)
            .When(x => x.WebsiteUrl is not null);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage(MessageKeys.RecurringTransaction.DescriptionTooLong)
            .When(x => x.Description is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage(MessageKeys.RecurringTransaction.NotesTooLong)
            .When(x => x.Notes is not null);
    }
}
