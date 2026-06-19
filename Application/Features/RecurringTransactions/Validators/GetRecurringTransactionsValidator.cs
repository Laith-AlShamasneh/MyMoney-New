using Application.Features.RecurringTransactions.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.RecurringTransactions.Validators;

public sealed class GetRecurringTransactionsValidator : AbstractValidator<GetRecurringTransactionsRequest>
{
    public GetRecurringTransactionsValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage(MessageKeys.RecurringTransaction.PageNumberInvalid);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage(MessageKeys.RecurringTransaction.PageSizeInvalid);
    }
}

public sealed class GetSubscriptionsValidator : AbstractValidator<GetSubscriptionsRequest>
{
    public GetSubscriptionsValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage(MessageKeys.Subscription.PageNumberInvalid);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage(MessageKeys.Subscription.PageSizeInvalid);
    }
}
