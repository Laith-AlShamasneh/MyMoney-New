using Application.Features.RecurringTransactions.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.RecurringTransactions.Validators;

public sealed class UpdateRecurringTransactionValidator : AbstractValidator<UpdateRecurringTransactionRequest>
{
    public UpdateRecurringTransactionValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(MessageKeys.RecurringTransaction.InvalidId);

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

        RuleFor(x => x.FrequencyInterval)
            .GreaterThan(0)
            .WithMessage(MessageKeys.RecurringTransaction.CustomIntervalMustBePositive)
            .When(x => x.FrequencyInterval.HasValue);

        RuleFor(x => x.FrequencyUnit)
            .InclusiveBetween(1, 4)
            .WithMessage(MessageKeys.RecurringTransaction.CustomUnitRequired)
            .When(x => x.FrequencyUnit.HasValue);

        RuleFor(x => x.DayOfMonth)
            .InclusiveBetween(0, 28)
            .WithMessage(MessageKeys.RecurringTransaction.InvalidDayOfMonth)
            .When(x => x.DayOfMonth.HasValue);

        RuleFor(x => x.DayOfWeek)
            .InclusiveBetween(0, 6)
            .WithMessage(MessageKeys.RecurringTransaction.InvalidDayOfWeek)
            .When(x => x.DayOfWeek.HasValue);

        RuleFor(x => x.EndDate)
            .Must(e => e is null || DateOnly.TryParse(e, out _))
            .WithMessage(MessageKeys.RecurringTransaction.EndDateBeforeStartDate)
            .When(x => x.EndDate is not null);

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
