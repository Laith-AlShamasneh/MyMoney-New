using Application.Features.Profile.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Profile.Validators;

public sealed class RequestEmailChangeValidator : AbstractValidator<RequestEmailChangeRequest>
{
    public RequestEmailChangeValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty().WithMessage(MessageKeys.Profile.NewEmailRequired)
            .MaximumLength(254).WithMessage(MessageKeys.Profile.NewEmailTooLong)
            .EmailAddress().WithMessage(MessageKeys.Profile.NewEmailInvalid);

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage(MessageKeys.Profile.CurrentPasswordRequired);
    }
}
