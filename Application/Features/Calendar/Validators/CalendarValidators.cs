using Application.Features.Calendar.DTOs;
using FluentValidation;
using Shared.Constants;

namespace Application.Features.Calendar.Validators;

public sealed class CreateCalendarEventValidator : AbstractValidator<CreateCalendarEventRequest>
{
    public CreateCalendarEventValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(MessageKeys.Calendar.TitleRequired)
            .MaximumLength(200).WithMessage(MessageKeys.Calendar.TitleTooLong);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage(MessageKeys.Calendar.DescriptionTooLong)
            .When(x => x.Description is not null);

        RuleFor(x => x.EventDate)
            .NotEmpty().WithMessage(MessageKeys.Calendar.EventDateRequired)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage(MessageKeys.Calendar.InvalidEventDate);

        RuleFor(x => x.EventTypeId)
            .InclusiveBetween(1, 8).WithMessage(MessageKeys.Calendar.InvalidEventType);

        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 4).WithMessage(MessageKeys.Calendar.InvalidPriority);

        RuleFor(x => x.NotifyBefore)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.NotifyBeforeMustBePositive)
            .LessThanOrEqualTo(43200).WithMessage(MessageKeys.Calendar.NotifyBeforeTooLarge)
            .When(x => x.NotifyBefore.HasValue);

        RuleFor(x => x.ColorHex)
            .MaximumLength(10).WithMessage(MessageKeys.Calendar.ColorHexTooLong)
            .When(x => x.ColorHex is not null);

        RuleFor(x => x.Icon)
            .MaximumLength(50).WithMessage(MessageKeys.Calendar.IconTooLong)
            .When(x => x.Icon is not null);

        RuleFor(x => x.LinkedEntityId)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.InvalidLinkedEntityId)
            .When(x => x.LinkedEntityId.HasValue);

        RuleFor(x => x.LinkedEntityTypeId)
            .InclusiveBetween(1, 8).WithMessage(MessageKeys.Calendar.InvalidLinkedEntityType)
            .When(x => x.LinkedEntityTypeId.HasValue);
    }
}

public sealed class UpdateCalendarEventValidator : AbstractValidator<UpdateCalendarEventRequest>
{
    public UpdateCalendarEventValidator()
    {
        RuleFor(x => x.EventId)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.InvalidEventId);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(MessageKeys.Calendar.TitleRequired)
            .MaximumLength(200).WithMessage(MessageKeys.Calendar.TitleTooLong);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage(MessageKeys.Calendar.DescriptionTooLong)
            .When(x => x.Description is not null);

        RuleFor(x => x.EventDate)
            .NotEmpty().WithMessage(MessageKeys.Calendar.EventDateRequired)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage(MessageKeys.Calendar.InvalidEventDate);

        RuleFor(x => x.EventTypeId)
            .InclusiveBetween(1, 8).WithMessage(MessageKeys.Calendar.InvalidEventType);

        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 4).WithMessage(MessageKeys.Calendar.InvalidPriority);

        RuleFor(x => x.NotifyBefore)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.NotifyBeforeMustBePositive)
            .LessThanOrEqualTo(43200).WithMessage(MessageKeys.Calendar.NotifyBeforeTooLarge)
            .When(x => x.NotifyBefore.HasValue);

        RuleFor(x => x.ColorHex)
            .MaximumLength(10).WithMessage(MessageKeys.Calendar.ColorHexTooLong)
            .When(x => x.ColorHex is not null);

        RuleFor(x => x.Icon)
            .MaximumLength(50).WithMessage(MessageKeys.Calendar.IconTooLong)
            .When(x => x.Icon is not null);
    }
}

public sealed class GetCalendarByDayValidator : AbstractValidator<GetCalendarByDayRequest>
{
    public GetCalendarByDayValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty().WithMessage(MessageKeys.Calendar.EventDateRequired)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage(MessageKeys.Calendar.InvalidEventDate);
    }
}

public sealed class GetCalendarByWeekValidator : AbstractValidator<GetCalendarByWeekRequest>
{
    public GetCalendarByWeekValidator()
    {
        RuleFor(x => x.WeekStart)
            .NotEmpty().WithMessage(MessageKeys.Calendar.WeekStartRequired)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage(MessageKeys.Calendar.InvalidWeekStart);
    }
}

public sealed class GetCalendarByMonthValidator : AbstractValidator<GetCalendarByMonthRequest>
{
    public GetCalendarByMonthValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage(MessageKeys.Calendar.InvalidYear);

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage(MessageKeys.Calendar.InvalidMonth);

        RuleFor(x => x.EventTypeId)
            .InclusiveBetween(1, 8).WithMessage(MessageKeys.Calendar.InvalidEventType)
            .When(x => x.EventTypeId.HasValue);
    }
}

public sealed class GetCalendarAgendaValidator : AbstractValidator<GetCalendarAgendaRequest>
{
    public GetCalendarAgendaValidator()
    {
        RuleFor(x => x.StartDate)
            .Must(d => DateOnly.TryParse(d!, out _)).WithMessage(MessageKeys.Calendar.InvalidEventDate)
            .When(x => !string.IsNullOrEmpty(x.StartDate));

        RuleFor(x => x.DaysAhead)
            .InclusiveBetween(1, 365).WithMessage(MessageKeys.Calendar.DaysAheadInvalid);

        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.PageNumberInvalid);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage(MessageKeys.Calendar.PageSizeInvalid);
    }
}

public sealed class SearchCalendarValidator : AbstractValidator<SearchCalendarRequest>
{
    public SearchCalendarValidator()
    {
        RuleFor(x => x.Keyword)
            .MaximumLength(200).WithMessage(MessageKeys.Calendar.KeywordTooLong)
            .When(x => !string.IsNullOrEmpty(x.Keyword));

        RuleFor(x => x.EventTypeId)
            .InclusiveBetween(1, 8).WithMessage(MessageKeys.Calendar.InvalidEventType)
            .When(x => x.EventTypeId.HasValue);

        RuleFor(x => x.DateFrom)
            .Must(d => DateOnly.TryParse(d!, out _)).WithMessage(MessageKeys.Calendar.InvalidEventDate)
            .When(x => !string.IsNullOrEmpty(x.DateFrom));

        RuleFor(x => x.DateTo)
            .Must(d => DateOnly.TryParse(d!, out _)).WithMessage(MessageKeys.Calendar.InvalidEventDate)
            .When(x => !string.IsNullOrEmpty(x.DateTo));

        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.PageNumberInvalid);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage(MessageKeys.Calendar.PageSizeInvalid);
    }
}

public sealed class DismissReminderValidator : AbstractValidator<DismissReminderRequest>
{
    public DismissReminderValidator()
    {
        RuleFor(x => x.ReminderId)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.InvalidReminderId);
    }
}

public sealed class SnoozeReminderValidator : AbstractValidator<SnoozeReminderRequest>
{
    public SnoozeReminderValidator()
    {
        RuleFor(x => x.ReminderId)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.InvalidReminderId);
    }
}

public sealed class MarkReminderClickedValidator : AbstractValidator<MarkReminderClickedRequest>
{
    public MarkReminderClickedValidator()
    {
        RuleFor(x => x.ReminderId)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.InvalidReminderId);
    }
}

public sealed class ReminderHistoryValidator : AbstractValidator<ReminderHistoryRequest>
{
    public ReminderHistoryValidator()
    {
        RuleFor(x => x.ReminderId)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.InvalidReminderId);
    }
}

public sealed class GetCalendarEventValidator : AbstractValidator<GetCalendarEventRequest>
{
    public GetCalendarEventValidator()
    {
        RuleFor(x => x.EventId)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.InvalidEventId);
    }
}

public sealed class DeleteCalendarEventValidator : AbstractValidator<DeleteCalendarEventRequest>
{
    public DeleteCalendarEventValidator()
    {
        RuleFor(x => x.EventId)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.InvalidEventId);
    }
}

public sealed class CompleteCalendarEventValidator : AbstractValidator<CompleteCalendarEventRequest>
{
    public CompleteCalendarEventValidator()
    {
        RuleFor(x => x.EventId)
            .GreaterThan(0).WithMessage(MessageKeys.Calendar.InvalidEventId);
    }
}
