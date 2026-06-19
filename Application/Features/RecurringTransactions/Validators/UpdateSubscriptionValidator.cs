using Application.Features.RecurringTransactions.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.RecurringTransactions.Validators;

public sealed class UpdateSubscriptionValidator : AbstractValidator<UpdateSubscriptionRequest>
{
    public UpdateSubscriptionValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Subscription.InvalidId);

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(MessageKeys.RecurringTransaction.NameRequired)
            .MaximumLength(200)
            .WithMessage(MessageKeys.RecurringTransaction.NameTooLong);

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage(MessageKeys.RecurringTransaction.AmountMustBePositive);

        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
            .WithMessage(MessageKeys.RecurringTransaction.CategoryRequired);

        RuleFor(x => x.ProviderName)
            .NotEmpty()
            .WithMessage(MessageKeys.Subscription.ProviderNameRequired)
            .MaximumLength(200)
            .WithMessage(MessageKeys.Subscription.ProviderNameTooLong);

        RuleFor(x => x.WebsiteUrl)
            .MaximumLength(500)
            .WithMessage(MessageKeys.Subscription.WebsiteUrlTooLong)
            .When(x => x.WebsiteUrl is not null);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage(MessageKeys.RecurringTransaction.DescriptionTooLong)
            .When(x => x.Description is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage(MessageKeys.RecurringTransaction.NotesTooLong)
            .When(x => x.Notes is not null);
    }
}
