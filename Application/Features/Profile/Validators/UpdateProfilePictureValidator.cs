using Application.Features.Profile.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Profile.Validators;

public sealed class UpdateProfilePictureValidator : AbstractValidator<UpdateProfilePictureRequest>
{
    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public UpdateProfilePictureValidator()
    {
        RuleFor(x => x.ProfileImage)
            .NotNull().WithMessage(MessageKeys.Profile.InvalidProfilePictureFormat);

        When(x => x.ProfileImage is not null, () =>
        {
            RuleFor(x => x.ProfileImage.ContentType)
                .Must(ct => AllowedTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
                .WithMessage(MessageKeys.Profile.InvalidProfilePictureFormat);

            RuleFor(x => x.ProfileImage.Length)
                .LessThanOrEqualTo(MaxFileSizeBytes)
                .WithMessage(MessageKeys.Profile.ProfilePictureTooLarge);
        });
    }
}
