using Application.Features.Receipt.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Receipt.Validators;

public sealed class CreateReceiptTagValidator : AbstractValidator<CreateReceiptTagRequest>
{
    public CreateReceiptTagValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(MessageKeys.Receipt.TagNameRequired)
            .MaximumLength(100)
            .WithMessage(MessageKeys.Receipt.TagNameTooLong);

        RuleFor(x => x.ColorHex)
            .MaximumLength(10)
            .WithMessage(MessageKeys.Receipt.ColorHexTooLong)
            .When(x => !string.IsNullOrEmpty(x.ColorHex));
    }
}

public sealed class DeleteReceiptTagValidator : AbstractValidator<DeleteReceiptTagRequest>
{
    public DeleteReceiptTagValidator()
    {
        RuleFor(x => x.TagId)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Receipt.InvalidTagId);
    }
}
