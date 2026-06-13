using Application.Features.Profile.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Profile.Validators;

public sealed class ConfirmEmailChangeValidator : AbstractValidator<ConfirmEmailChangeRequest>
{
    public ConfirmEmailChangeValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage(MessageKeys.Profile.EmailChangeTokenRequired);
    }
}
