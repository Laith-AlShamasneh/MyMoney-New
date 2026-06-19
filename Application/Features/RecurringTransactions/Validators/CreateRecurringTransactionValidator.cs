using Application.Features.RecurringTransactions.DTOs;
using FluentValidation;
using Shared.Constants;
using Shared.Enums.RecurringTransactions;

namespace Application.Features.RecurringTransactions.Validators;

public sealed class CreateRecurringTransactionValidator : AbstractValidator<CreateRecurringTransactionRequest>
{
    public CreateRecurringTransactionValidator()
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

        RuleFor(x => x.TransactionTypeId)
            .InclusiveBetween(1, 2)
            .WithMessage(MessageKeys.RecurringTransaction.InvalidTransactionType);

        RuleFor(x => x.FrequencyId)
            .InclusiveBetween(1, 6)
            .WithMessage(MessageKeys.RecurringTransaction.InvalidFrequency);

        RuleFor(x => x.StartDate)
            .NotEmpty()
            .WithMessage(MessageKeys.RecurringTransaction.StartDateRequired)
            .Must(s => DateOnly.TryParse(s, out _))
            .WithMessage(MessageKeys.RecurringTransaction.InvalidStartDate);

        RuleFor(x => x.EndDate)
            .Must(e => e is null || DateOnly.TryParse(e, out _))
            .WithMessage(MessageKeys.RecurringTransaction.EndDateBeforeStartDate)
            .When(x => x.EndDate is not null);

        // Custom frequency requires interval + unit
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

        // Weekly requires DayOfWeek
        RuleFor(x => x.DayOfWeek)
            .NotNull()
            .WithMessage(MessageKeys.RecurringTransaction.DayOfWeekRequired)
            .InclusiveBetween(0, 6)
            .WithMessage(MessageKeys.RecurringTransaction.InvalidDayOfWeek)
            .When(x => x.FrequencyId == (int)RecurrenceFrequency.Weekly);

        // DayOfMonth: 0 = last day, 1–28 = literal day
        RuleFor(x => x.DayOfMonth)
            .InclusiveBetween(0, 28)
            .WithMessage(MessageKeys.RecurringTransaction.InvalidDayOfMonth)
            .When(x => x.DayOfMonth.HasValue);

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
