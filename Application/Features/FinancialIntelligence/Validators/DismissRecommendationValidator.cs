using Application.Features.FinancialIntelligence.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.FinancialIntelligence.Validators;

public sealed class DismissRecommendationValidator : AbstractValidator<DismissRecommendationRequest>
{
    public DismissRecommendationValidator()
    {
        RuleFor(x => x.RecommendationId)
            .GreaterThan(0)
            .WithMessage(MessageKeys.FinancialIntelligence.InvalidRecommendationId);
    }
}
