using Application.Features.Transaction.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Transaction.Validators;

public sealed class UpdateTransactionValidator : AbstractValidator<UpdateTransactionRequest>
{
    public UpdateTransactionValidator()
    {
        RuleFor(x => x.TransactionTypeId)
            .InclusiveBetween(1, 2)
            .WithMessage(MessageKeys.Transaction.InvalidTransactionType);

        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Transaction.CategoryRequired);

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Transaction.AmountMustBePositive);

        RuleFor(x => x.TransactionDate)
            .NotEmpty()
            .WithMessage(MessageKeys.Transaction.DateRequired)
            .Must(d => DateOnly.TryParse(d, out var date) &&
                       date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage(MessageKeys.Transaction.DateCannotBeFuture)
            .When(x => !string.IsNullOrEmpty(x.TransactionDate));

        When(x => !string.IsNullOrEmpty(x.Description), () =>
        {
            RuleFor(x => x.Description!)
                .MaximumLength(500)
                .WithMessage(MessageKeys.Transaction.DescriptionTooLong);
        });

        When(x => !string.IsNullOrEmpty(x.Notes), () =>
        {
            RuleFor(x => x.Notes!)
                .MaximumLength(1000)
                .WithMessage(MessageKeys.Transaction.NotesTooLong);
        });
    }
}
