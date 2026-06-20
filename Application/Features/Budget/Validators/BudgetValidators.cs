using Application.Features.Budget.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Budget.Validators;

public sealed class CreateBudgetValidator : AbstractValidator<CreateBudgetRequest>
{
    public CreateBudgetValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(MessageKeys.Budget.NameRequired)
            .MaximumLength(100).WithMessage(MessageKeys.Budget.NameTooLong);

        RuleFor(x => x.BudgetTypeId)
            .InclusiveBetween(1, 4).WithMessage(MessageKeys.Budget.InvalidBudgetType);

        RuleFor(x => x.PeriodTypeId)
            .InclusiveBetween(1, 3).WithMessage(MessageKeys.Budget.InvalidPeriodType);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage(MessageKeys.Budget.AmountMustBePositive);

        // For percentage-based budgets, amount must be 1–100
        RuleFor(x => x.Amount)
            .InclusiveBetween(1, 100).WithMessage(MessageKeys.Budget.PercentageMustBe1To100)
            .When(x => x.BudgetTypeId == 2);

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage(MessageKeys.Budget.StartDateRequired)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage(MessageKeys.Budget.StartDateRequired);

        RuleFor(x => x.EndDate)
            .Must(d => d == null || DateOnly.TryParse(d, out _))
            .WithMessage(MessageKeys.Budget.EndDateBeforeStartDate)
            .When(x => x.EndDate is not null);

        RuleFor(x => x)
            .Must(x =>
            {
                if (x.EndDate == null) return true;
                return DateOnly.TryParse(x.StartDate, out var s)
                    && DateOnly.TryParse(x.EndDate, out var e)
                    && e > s;
            })
            .WithMessage(MessageKeys.Budget.EndDateBeforeStartDate)
            .When(x => x.EndDate is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage(MessageKeys.Budget.NotesTooLong)
            .When(x => x.Notes is not null);
    }
}

public sealed class UpdateBudgetValidator : AbstractValidator<UpdateBudgetRequest>
{
    public UpdateBudgetValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(MessageKeys.Budget.InvalidId);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(MessageKeys.Budget.NameRequired)
            .MaximumLength(100).WithMessage(MessageKeys.Budget.NameTooLong);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage(MessageKeys.Budget.AmountMustBePositive);

        RuleFor(x => x.EndDate)
            .Must(d => d == null || DateOnly.TryParse(d, out _))
            .WithMessage(MessageKeys.Budget.EndDateBeforeStartDate)
            .When(x => x.EndDate is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage(MessageKeys.Budget.NotesTooLong)
            .When(x => x.Notes is not null);
    }
}

public sealed class GetBudgetPeriodsValidator : AbstractValidator<GetBudgetPeriodsRequest>
{
    public GetBudgetPeriodsValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(MessageKeys.Budget.InvalidId);

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage(MessageKeys.Budget.PageNumberInvalid);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage(MessageKeys.Budget.PageSizeInvalid);
    }
}

public sealed class GetBudgetAnalyticsValidator : AbstractValidator<GetBudgetAnalyticsRequest>
{
    public GetBudgetAnalyticsValidator()
    {
        RuleFor(x => x.BudgetId)
            .GreaterThan(0).WithMessage(MessageKeys.Budget.InvalidId)
            .When(x => x.BudgetId.HasValue);

        RuleFor(x => x.DateFrom)
            .Must(d => d == null || DateOnly.TryParse(d, out _))
            .WithMessage(MessageKeys.Common.BadRequest)
            .When(x => x.DateFrom is not null);

        RuleFor(x => x.DateTo)
            .Must(d => d == null || DateOnly.TryParse(d, out _))
            .WithMessage(MessageKeys.Common.BadRequest)
            .When(x => x.DateTo is not null);
    }
}
