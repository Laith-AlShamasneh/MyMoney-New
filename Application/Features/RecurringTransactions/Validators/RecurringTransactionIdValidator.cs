using Application.Features.RecurringTransactions.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.RecurringTransactions.Validators;

public sealed class RecurringTransactionIdValidator : AbstractValidator<RecurringTransactionIdRequest>
{
    public RecurringTransactionIdValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(MessageKeys.RecurringTransaction.InvalidId);
    }
}
