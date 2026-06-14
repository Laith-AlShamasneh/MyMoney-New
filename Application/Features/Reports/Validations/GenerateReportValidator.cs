using Application.Features.Reports.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Reports.Validations;

public sealed class GenerateReportValidator : AbstractValidator<GenerateReportRequest>
{
    private static readonly HashSet<string> _validLanguages = ["en", "ar"];

    public GenerateReportValidator()
    {
        RuleFor(x => x.ReportTypeId)
            .GreaterThan((byte)0)
            .WithMessage(MessageKeys.Reports.ReportTypeRequired);

        RuleFor(x => x.Language)
            .NotEmpty()
            .WithMessage(MessageKeys.Reports.LanguageRequired)
            .Must(l => _validLanguages.Contains(l))
            .WithMessage(MessageKeys.Reports.InvalidLanguage)
            .When(x => !string.IsNullOrEmpty(x.Language));

        RuleFor(x => x.DateFrom)
            .NotEmpty()
            .WithMessage(MessageKeys.Reports.DateFromRequired)
            .Must(d => DateOnly.TryParse(d, out _))
            .WithMessage(MessageKeys.Reports.DateFromRequired)
            .When(x => !string.IsNullOrEmpty(x.DateFrom));

        RuleFor(x => x.DateTo)
            .NotEmpty()
            .WithMessage(MessageKeys.Reports.DateToRequired)
            .Must(d => DateOnly.TryParse(d, out _))
            .WithMessage(MessageKeys.Reports.DateToRequired)
            .When(x => !string.IsNullOrEmpty(x.DateTo));

        RuleFor(x => x)
            .Must(x =>
            {
                if (!DateOnly.TryParse(x.DateFrom, out var from)) return true;
                if (!DateOnly.TryParse(x.DateTo, out var to)) return true;
                return from <= to;
            })
            .WithMessage(MessageKeys.Reports.InvalidDateRange)
            .Must(x =>
            {
                if (!DateOnly.TryParse(x.DateFrom, out var from)) return true;
                if (!DateOnly.TryParse(x.DateTo, out var to)) return true;
                return (to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue)).Days <= 366;
            })
            .WithMessage(MessageKeys.Reports.DateRangeTooLarge)
            .When(x => !string.IsNullOrEmpty(x.DateFrom) && !string.IsNullOrEmpty(x.DateTo));
    }
}
