using Application.Features.Receipt.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Receipt.Validators;

public sealed class UpdateReceiptValidator : AbstractValidator<UpdateReceiptRequest>
{
    public UpdateReceiptValidator()
    {
        RuleFor(x => x.ReceiptId)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Receipt.InvalidReceiptId);

        RuleFor(x => x.Title)
            .MaximumLength(255)
            .WithMessage(MessageKeys.Receipt.TitleTooLong)
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage(MessageKeys.Receipt.DescriptionTooLong)
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.MerchantName)
            .MaximumLength(255)
            .WithMessage(MessageKeys.Receipt.MerchantNameTooLong)
            .When(x => !string.IsNullOrEmpty(x.MerchantName));

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Receipt.AmountMustBePositive)
            .When(x => x.Amount.HasValue);

        RuleFor(x => x.CurrencyCode)
            .MaximumLength(10)
            .WithMessage(MessageKeys.Receipt.CurrencyCodeTooLong)
            .When(x => !string.IsNullOrEmpty(x.CurrencyCode));

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .WithMessage(MessageKeys.Receipt.NotesTooLong)
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
