using Application.Features.Profile.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Profile.Validators;

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.FirstNameEn)
            .NotEmpty().WithMessage(MessageKeys.Profile.FirstNameRequired)
            .MaximumLength(100).WithMessage(MessageKeys.Profile.FirstNameTooLong);

        RuleFor(x => x.LastNameEn)
            .NotEmpty().WithMessage(MessageKeys.Profile.LastNameRequired)
            .MaximumLength(100).WithMessage(MessageKeys.Profile.LastNameTooLong);

        RuleFor(x => x.DisplayNameEn)
            .NotEmpty().WithMessage(MessageKeys.Profile.FirstNameRequired)
            .MaximumLength(200).WithMessage(MessageKeys.Profile.DisplayNameTooLong);

        RuleFor(x => x.FirstNameAr)
            .MaximumLength(100).WithMessage(MessageKeys.Profile.FirstNameTooLong)
            .When(x => x.FirstNameAr is not null);

        RuleFor(x => x.LastNameAr)
            .MaximumLength(100).WithMessage(MessageKeys.Profile.LastNameTooLong)
            .When(x => x.LastNameAr is not null);

        RuleFor(x => x.DisplayNameAr)
            .MaximumLength(200).WithMessage(MessageKeys.Profile.DisplayNameTooLong)
            .When(x => x.DisplayNameAr is not null);

        RuleFor(x => x.DateOfBirth)
            .Must(dob => dob is null || dob <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage(MessageKeys.Authentication.InvalidDateOfBirth);

        RuleFor(x => x.GenderId)
            .Must(g => g is null || g is 1 or 2 or 3)
            .WithMessage(MessageKeys.Profile.InvalidGender);
    }
}
