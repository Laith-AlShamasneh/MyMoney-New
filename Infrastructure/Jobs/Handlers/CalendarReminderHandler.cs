using Application.Common.Constants;
using Application.Features.Calendar.Jobs;
using Application.Features.Notifications.Jobs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;

namespace Infrastructure.Jobs.Handlers;

internal sealed class CalendarReminderHandler(
    ICalendarRepository   calendarRepository,
    IBackgroundJobService backgroundJobService) : JobHandlerBase<CalendarReminderPayload>
{
    public override string JobType => JobTypes.CalendarReminder;

    protected override async Task HandleAsync(CalendarReminderPayload payload, CancellationToken ct)
    {
        var notificationPayload = new CreateNotificationPayload(
            TemplateCode: "CALENDAR_REMINDER_DUE",
            UserId:       payload.UserId,
            Parameters: new Dictionary<string, string>
            {
                { "EventTitle", payload.EventTitle },
                { "EventDate",  payload.EventDate  },
            },
            PayloadJson: null);

        await backgroundJobService.EnqueueAsync(
            JobTypes.CreateNotification,
            notificationPayload,
            priority: 2,
            ct: ct);

        await calendarRepository.MarkReminderSentAsync(payload.ReminderId, jobId: null, ct);
    }
}
