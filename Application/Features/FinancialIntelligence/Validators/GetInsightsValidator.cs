using Application.Features.FinancialIntelligence.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.FinancialIntelligence.Validators;

public sealed class GetInsightsValidator : AbstractValidator<GetInsightsRequest>
{
    public GetInsightsValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage(MessageKeys.FinancialIntelligence.InvalidPageNumber);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage(MessageKeys.FinancialIntelligence.InvalidPageSize);
    }
}
