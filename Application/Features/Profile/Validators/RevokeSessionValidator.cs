using Application.Features.Profile.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Profile.Validators;

public sealed class RevokeSessionValidator : AbstractValidator<RevokeSessionRequest>
{
    public RevokeSessionValidator()
    {
        RuleFor(x => x.SessionId)
            .GreaterThan(0).WithMessage(MessageKeys.Profile.SessionNotFound);
    }
}
