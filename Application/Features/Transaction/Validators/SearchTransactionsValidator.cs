using Application.Features.Transaction.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Transaction.Validators;

public sealed class SearchTransactionsValidator : AbstractValidator<SearchTransactionsRequest>
{
    public SearchTransactionsValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Transaction.PageNumberInvalid);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage(MessageKeys.Transaction.PageSizeInvalid);

        When(x => x.SortDir is not null, () =>
        {
            RuleFor(x => x.SortDir!)
                .Must(d => string.Equals(d, "ASC", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(d, "DESC", StringComparison.OrdinalIgnoreCase))
                .WithMessage(MessageKeys.Transaction.InvalidSortDirection);
        });

        When(x => x.TypeId.HasValue, () =>
        {
            RuleFor(x => x.TypeId!.Value)
                .InclusiveBetween(1, 2)
                .WithMessage(MessageKeys.Transaction.InvalidTransactionType);
        });

        When(x => x.AmountMin.HasValue && x.AmountMax.HasValue, () =>
        {
            RuleFor(x => x.AmountMax!.Value)
                .GreaterThanOrEqualTo(x => x.AmountMin!.Value)
                .WithMessage(MessageKeys.Transaction.AmountRangeInvalid);
        });

        When(x => !string.IsNullOrEmpty(x.DateFrom) && !string.IsNullOrEmpty(x.DateTo), () =>
        {
            RuleFor(x => x)
                .Must(x => DateOnly.TryParse(x.DateFrom, out var from) &&
                            DateOnly.TryParse(x.DateTo,   out var to)  &&
                            from <= to)
                .WithMessage(MessageKeys.Transaction.DateRangeInvalid);
        });
    }
}
