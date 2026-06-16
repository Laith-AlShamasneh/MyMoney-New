using Application.Features.Transaction.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Transaction.Validators;

public sealed class DeleteTransactionValidator : AbstractValidator<DeleteTransactionRequest>
{
    public DeleteTransactionValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Transaction.InvalidTransactionId);
    }
}
