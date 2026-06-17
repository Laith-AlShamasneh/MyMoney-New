using Application.Features.FinancialIntelligence.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.FinancialIntelligence.Validators;

public sealed class MarkInsightReadValidator : AbstractValidator<MarkInsightReadRequest>
{
    public MarkInsightReadValidator()
    {
        RuleFor(x => x.InsightId)
            .GreaterThan(0)
            .WithMessage(MessageKeys.FinancialIntelligence.InvalidInsightId);
    }
}
