using Application.Features.Receipt.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Receipt.Validators;

public sealed class SearchReceiptsValidator : AbstractValidator<SearchReceiptsRequest>
{
    public SearchReceiptsValidator()
    {
        RuleFor(x => x.Keyword)
            .MaximumLength(500)
            .WithMessage(MessageKeys.Receipt.KeywordTooLong)
            .When(x => !string.IsNullOrEmpty(x.Keyword));

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage(MessageKeys.Receipt.InvalidPageNumber);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage(MessageKeys.Receipt.InvalidPageSize);

        RuleFor(x => x.AmountMin)
            .GreaterThanOrEqualTo(0)
            .WithMessage(MessageKeys.Receipt.AmountMustBePositive)
            .When(x => x.AmountMin.HasValue);

        RuleFor(x => x.AmountMax)
            .GreaterThanOrEqualTo(0)
            .WithMessage(MessageKeys.Receipt.AmountMustBePositive)
            .When(x => x.AmountMax.HasValue);
    }
}
