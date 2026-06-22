using Application.Common.Options;
using Application.Features.Receipt.DTOs;
using FluentValidation;
using Microsoft.Extensions.Options;
using Shared.Constants;

namespace Application.Features.Receipt.Validators;

public sealed class UploadReceiptValidator : AbstractValidator<UploadReceiptRequest>
{
    public UploadReceiptValidator(IOptions<ReceiptOptions> options)
    {
        var opts = options.Value;

        RuleFor(x => x.File)
            .NotNull()
            .WithMessage(MessageKeys.Receipt.FileRequired);

        RuleFor(x => x.File.Length)
            .LessThanOrEqualTo(opts.MaxFileSizeBytes)
            .WithMessage(MessageKeys.Receipt.FileTooLarge)
            .When(x => x.File is not null);

        RuleFor(x => x.File.FileName)
            .Must(name =>
            {
                var ext = System.IO.Path.GetExtension(name)?.ToLowerInvariant();
                return !string.IsNullOrEmpty(ext) && opts.AllowedExtensions.Contains(ext);
            })
            .WithMessage(MessageKeys.Receipt.InvalidFileType)
            .When(x => x.File is not null);

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
