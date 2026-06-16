using Application.Features.Reports.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Reports.Validations;

public sealed class DeleteReportValidator : AbstractValidator<DeleteReportRequest>
{
    public DeleteReportValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Common.BadRequest);
    }
}
