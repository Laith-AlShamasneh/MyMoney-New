using Application.Features.Goals.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Goals.Validators;

public sealed class CreateGoalValidator : AbstractValidator<CreateGoalRequest>
{
    public CreateGoalValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(MessageKeys.Goal.NameRequired)
            .MaximumLength(100).WithMessage(MessageKeys.Goal.NameTooLong);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(MessageKeys.Goal.DescriptionTooLong)
            .When(x => x.Description is not null);

        RuleFor(x => x.GoalTypeId)
            .InclusiveBetween(1, 8).WithMessage(MessageKeys.Goal.InvalidGoalType);

        RuleFor(x => x.TargetAmount)
            .GreaterThan(0).WithMessage(MessageKeys.Goal.TargetAmountMustBePositive);

        RuleFor(x => x.InitialAmount)
            .GreaterThanOrEqualTo(0).WithMessage(MessageKeys.Goal.InitialAmountCannotBeNegative)
            .When(x => x.InitialAmount.HasValue);

        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 4).WithMessage(MessageKeys.Goal.InvalidPriority)
            .When(x => x.Priority.HasValue);

        RuleFor(x => x.TargetDate)
            .Must(d => DateOnly.TryParse(d, out var date) && date > DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage(MessageKeys.Goal.TargetDateMustBeFuture)
            .When(x => !string.IsNullOrEmpty(x.TargetDate));
    }
}

public sealed class UpdateGoalValidator : AbstractValidator<UpdateGoalRequest>
{
    public UpdateGoalValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidId);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(MessageKeys.Goal.NameRequired)
            .MaximumLength(100).WithMessage(MessageKeys.Goal.NameTooLong);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(MessageKeys.Goal.DescriptionTooLong)
            .When(x => x.Description is not null);

        RuleFor(x => x.TargetAmount)
            .GreaterThan(0).WithMessage(MessageKeys.Goal.TargetAmountMustBePositive);

        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 4).WithMessage(MessageKeys.Goal.InvalidPriority);

        RuleFor(x => x.TargetDate)
            .Must(d => DateOnly.TryParse(d, out var date) && date > DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage(MessageKeys.Goal.TargetDateMustBeFuture)
            .When(x => !string.IsNullOrEmpty(x.TargetDate));
    }
}

public sealed class GetGoalValidator : AbstractValidator<GetGoalRequest>
{
    public GetGoalValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidId);
    }
}

public sealed class GetGoalListValidator : AbstractValidator<GetGoalListRequest>
{
    public GetGoalListValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage(MessageKeys.Goal.PageNumberInvalid);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage(MessageKeys.Goal.PageSizeInvalid);
    }
}

public sealed class DeleteGoalValidator : AbstractValidator<DeleteGoalRequest>
{
    public DeleteGoalValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidId);
    }
}

public sealed class PauseGoalValidator : AbstractValidator<PauseGoalRequest>
{
    public PauseGoalValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidId);
    }
}

public sealed class ResumeGoalValidator : AbstractValidator<ResumeGoalRequest>
{
    public ResumeGoalValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidId);
    }
}

public sealed class AddContributionValidator : AbstractValidator<AddContributionRequest>
{
    public AddContributionValidator()
    {
        RuleFor(x => x.GoalId).GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidId);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage(MessageKeys.Goal.ContributionAmountMustBePositive);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage(MessageKeys.Goal.NotesTooLong)
            .When(x => x.Notes is not null);

        RuleFor(x => x.ContributionDate)
            .NotEmpty().WithMessage(MessageKeys.Goal.ContributionDateRequired)
            .Must(d => DateOnly.TryParse(d, out _))
            .WithMessage(MessageKeys.Goal.ContributionDateRequired);
    }
}

public sealed class WithdrawValidator : AbstractValidator<WithdrawRequest>
{
    public WithdrawValidator()
    {
        RuleFor(x => x.GoalId).GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidId);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage(MessageKeys.Goal.ContributionAmountMustBePositive);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage(MessageKeys.Goal.NotesTooLong)
            .When(x => x.Notes is not null);

        RuleFor(x => x.ContributionDate)
            .NotEmpty().WithMessage(MessageKeys.Goal.ContributionDateRequired)
            .Must(d => DateOnly.TryParse(d, out _))
            .WithMessage(MessageKeys.Goal.ContributionDateRequired);
    }
}

public sealed class AdjustGoalValidator : AbstractValidator<AdjustGoalRequest>
{
    public AdjustGoalValidator()
    {
        RuleFor(x => x.GoalId).GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidId);

        RuleFor(x => x.NewAmount)
            .GreaterThan(0).WithMessage(MessageKeys.Goal.NewAmountMustBePositive);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage(MessageKeys.Goal.NotesTooLong)
            .When(x => x.Notes is not null);

        RuleFor(x => x.AdjustmentDate)
            .NotEmpty().WithMessage(MessageKeys.Goal.ContributionDateRequired)
            .Must(d => DateOnly.TryParse(d, out _))
            .WithMessage(MessageKeys.Goal.ContributionDateRequired);
    }
}

public sealed class GetContributionsValidator : AbstractValidator<GetContributionsRequest>
{
    public GetContributionsValidator()
    {
        RuleFor(x => x.GoalId).GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidId);
        RuleFor(x => x.PageNumber).GreaterThan(0).WithMessage(MessageKeys.Goal.PageNumberInvalid);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage(MessageKeys.Goal.PageSizeInvalid);
    }
}

public sealed class LinkRecurringValidator : AbstractValidator<LinkRecurringRequest>
{
    public LinkRecurringValidator()
    {
        RuleFor(x => x.GoalId).GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidId);
        RuleFor(x => x.RecurringDefinitionId).GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidRecurringId);
    }
}

public sealed class UnlinkRecurringValidator : AbstractValidator<UnlinkRecurringRequest>
{
    public UnlinkRecurringValidator()
    {
        RuleFor(x => x.GoalId).GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidId);
        RuleFor(x => x.RecurringDefinitionId).GreaterThan(0).WithMessage(MessageKeys.Goal.InvalidRecurringId);
    }
}
