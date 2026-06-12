using Application.Features.Authentication.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Authentication.Validators;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage(MessageKeys.Authentication.RefreshTokenRequired);
    }
}
