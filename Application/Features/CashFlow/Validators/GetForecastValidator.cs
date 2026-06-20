using Application.Features.CashFlow.DTOs;
using FluentValidation;

namespace Application.Features.CashFlow.Validators;

public sealed class GetForecastValidator : AbstractValidator<GetForecastRequest>
{
    public GetForecastValidator()
    {
        RuleFor(x => x.HorizonMonths)
            .InclusiveBetween(1, 24)
            .WithMessage("HorizonMonths must be between 1 and 24.");
    }
}
