using Application.Features.Receipt.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Receipt.Validators;

public sealed class ReceiptActionValidator : AbstractValidator<ReceiptActionRequest>
{
    public ReceiptActionValidator()
    {
        RuleFor(x => x.ReceiptId)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Receipt.InvalidReceiptId);
    }
}

public sealed class AssignTransactionValidator : AbstractValidator<AssignTransactionRequest>
{
    public AssignTransactionValidator()
    {
        RuleFor(x => x.ReceiptId)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Receipt.InvalidReceiptId);

        RuleFor(x => x.TransactionId)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Receipt.InvalidTransactionId)
            .When(x => x.TransactionId.HasValue);
    }
}

public sealed class SetReceiptTagsValidator : AbstractValidator<SetReceiptTagsRequest>
{
    public SetReceiptTagsValidator()
    {
        RuleFor(x => x.ReceiptId)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Receipt.InvalidReceiptId);
    }
}
