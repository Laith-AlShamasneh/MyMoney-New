using Application.Features.Calendar;
using Application.Features.Calendar.DTOs;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Calendar;

public static class CalendarEndpoints
{
    public static void MapCalendarEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/calendar")
                       .WithTags("Calendar")
                       .RequireAuthorization();

        group.MapPost("dashboard", async (
            ICalendarService  service,
            CancellationToken ct) =>
        {
            var result = await service.GetDashboardAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapPost("day", async (
            GetCalendarByDayRequest request,
            ICalendarService        service,
            CancellationToken       ct) =>
        {
            var result = await service.GetByDayAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetCalendarByDayRequest>>();

        group.MapPost("week", async (
            GetCalendarByWeekRequest request,
            ICalendarService         service,
            CancellationToken        ct) =>
        {
            var result = await service.GetByWeekAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetCalendarByWeekRequest>>();

        group.MapPost("month", async (
            GetCalendarByMonthRequest request,
            ICalendarService          service,
            CancellationToken         ct) =>
        {
            var result = await service.GetByMonthAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetCalendarByMonthRequest>>();

        group.MapPost("agenda", async (
            GetCalendarAgendaRequest request,
            ICalendarService         service,
            CancellationToken        ct) =>
        {
            var result = await service.GetAgendaAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetCalendarAgendaRequest>>();

        group.MapPost("search", async (
            SearchCalendarRequest request,
            ICalendarService      service,
            CancellationToken     ct) =>
        {
            var result = await service.SearchAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<SearchCalendarRequest>>();

        group.MapPost("events/get", async (
            GetCalendarEventRequest request,
            ICalendarService        service,
            CancellationToken       ct) =>
        {
            var result = await service.GetEventAsync(request.EventId, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetCalendarEventRequest>>();

        group.MapPost("events/create", async (
            CreateCalendarEventRequest request,
            ICalendarService           service,
            CancellationToken          ct) =>
        {
            var result = await service.CreateEventAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<CreateCalendarEventRequest>>();

        group.MapPost("events/update", async (
            UpdateCalendarEventRequest request,
            ICalendarService           service,
            CancellationToken          ct) =>
        {
            var result = await service.UpdateEventAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<UpdateCalendarEventRequest>>();

        group.MapPost("events/delete", async (
            DeleteCalendarEventRequest request,
            ICalendarService           service,
            CancellationToken          ct) =>
        {
            var result = await service.DeleteEventAsync(request.EventId, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<DeleteCalendarEventRequest>>();

        group.MapPost("events/complete", async (
            CompleteCalendarEventRequest request,
            ICalendarService             service,
            CancellationToken            ct) =>
        {
            var result = await service.CompleteEventAsync(request.EventId, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<CompleteCalendarEventRequest>>();

        group.MapPost("reminders/dismiss", async (
            DismissReminderRequest request,
            ICalendarService       service,
            CancellationToken      ct) =>
        {
            var result = await service.DismissReminderAsync(request.ReminderId, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<DismissReminderRequest>>();

        // ── Smart reminders (popup) ───────────────────────────────────────────
        group.MapPost("reminders/active", async (
            ICalendarService  service,
            CancellationToken ct) =>
        {
            var result = await service.GetActiveRemindersAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapPost("reminders/snooze", async (
            SnoozeReminderRequest request,
            ICalendarService      service,
            CancellationToken     ct) =>
        {
            var result = await service.SnoozeReminderAsync(request.ReminderId, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<SnoozeReminderRequest>>();

        group.MapPost("reminders/go-to", async (
            MarkReminderClickedRequest request,
            ICalendarService           service,
            CancellationToken          ct) =>
        {
            var result = await service.MarkReminderClickedAsync(request.ReminderId, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<MarkReminderClickedRequest>>();

        group.MapPost("reminders/history", async (
            ReminderHistoryRequest request,
            ICalendarService       service,
            CancellationToken      ct) =>
        {
            var result = await service.GetReminderHistoryAsync(request.ReminderId, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<ReminderHistoryRequest>>();
    }
}
