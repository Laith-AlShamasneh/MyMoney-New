using Application.Features.Calendar.DbModels;
using Application.Features.Calendar.DTOs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.Calendar.Services;

internal sealed class CalendarService(
    ICalendarRepository calendarRepository,
    IUserContext        userContext,
    IMessageProvider    messageProvider) : ICalendarService
{
    // ── Create ─────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CreateCalendarEventResponse>> CreateEventAsync(
        CreateCalendarEventRequest request, CancellationToken ct = default)
    {
        DateOnly.TryParse(request.EventDate, out var eventDate);
        TimeOnly.TryParse(request.StartTime, out var startTime);
        TimeOnly.TryParse(request.EndTime,   out var endTime);

        var model = new CreateCalendarEventDbModel
        {
            UserId             = userContext.UserId,
            Title              = request.Title,
            Description        = request.Description,
            EventDate          = eventDate,
            StartTime          = string.IsNullOrEmpty(request.StartTime) ? null : startTime,
            EndTime            = string.IsNullOrEmpty(request.EndTime)   ? null : endTime,
            AllDay             = request.AllDay,
            EventTypeId        = (byte)request.EventTypeId,
            Priority           = (byte)request.Priority,
            LinkedEntityTypeId = request.LinkedEntityTypeId.HasValue ? (byte?)request.LinkedEntityTypeId : null,
            LinkedEntityId     = request.LinkedEntityId,
            ColorHex           = request.ColorHex,
            Icon               = request.Icon,
            NotifyBefore       = request.NotifyBefore,
            MetadataJson       = request.MetadataJson,
        };

        var eventId = await calendarRepository.CreateEventAsync(model, ct);
        var msg     = await messageProvider.GetMessagesAsync(MessageKeys.Calendar.EventCreated, ct);
        return ServiceResultFactory.Success(new CreateCalendarEventResponse(eventId), InternalResponseCodes.Created, msg);
    }

    // ── Update ─────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> UpdateEventAsync(
        UpdateCalendarEventRequest request, CancellationToken ct = default)
    {
        DateOnly.TryParse(request.EventDate, out var eventDate);
        TimeOnly.TryParse(request.StartTime, out var startTime);
        TimeOnly.TryParse(request.EndTime,   out var endTime);

        var model = new UpdateCalendarEventDbModel
        {
            EventId     = request.EventId,
            UserId      = userContext.UserId,
            Title       = request.Title,
            Description = request.Description,
            EventDate   = eventDate,
            StartTime   = string.IsNullOrEmpty(request.StartTime) ? null : startTime,
            EndTime     = string.IsNullOrEmpty(request.EndTime)   ? null : endTime,
            AllDay      = request.AllDay,
            EventTypeId = (byte)request.EventTypeId,
            Priority    = (byte)request.Priority,
            ColorHex    = request.ColorHex,
            Icon        = request.Icon,
            NotifyBefore = request.NotifyBefore,
        };

        var affected = await calendarRepository.UpdateEventAsync(model, ct);
        if (affected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Calendar.EventNotFound, ct));
        }

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Calendar.EventUpdated, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, msg);
    }

    // ── Delete ─────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> DeleteEventAsync(long eventId, CancellationToken ct = default)
    {
        var affected = await calendarRepository.DeleteEventAsync(eventId, userContext.UserId, ct);
        if (affected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Calendar.EventNotFound, ct));
        }

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Calendar.EventDeleted, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, msg);
    }

    // ── Get ────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CalendarEventDetailResponse>> GetEventAsync(
        long eventId, CancellationToken ct = default)
    {
        var row = await calendarRepository.GetEventByIdAsync(eventId, userContext.UserId, ct);
        if (row is null)
        {
            return ServiceResultFactory.Failure<CalendarEventDetailResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Calendar.EventNotFound, ct));
        }

        var reminder = row.ReminderId.HasValue
            ? new CalendarReminderResponse(row.ReminderId.Value, row.ReminderAtUtc!.Value, row.ReminderStatusId!.Value)
            : null;

        var response = new CalendarEventDetailResponse(
            EventId:           row.EventId,
            Title:             row.Title,
            Description:       row.Description,
            EventDate:         row.EventDate.ToString("yyyy-MM-dd"),
            StartTime:         row.StartTime,
            EndTime:           row.EndTime,
            AllDay:            row.AllDay,
            EventTypeId:       row.EventTypeId,
            Priority:          row.Priority,
            StatusId:          row.StatusId,
            LinkedEntityTypeId: row.LinkedEntityTypeId,
            LinkedEntityId:    row.LinkedEntityId,
            ColorHex:          row.ColorHex,
            Icon:              row.Icon,
            NotifyBefore:      row.NotifyBefore,
            MetadataJson:      row.MetadataJson,
            CreatedAtUtc:      row.CreatedAtUtc,
            UpdatedAtUtc:      row.UpdatedAtUtc,
            CompletedAtUtc:    row.CompletedAtUtc,
            Reminder:          reminder);

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Calendar.GetSuccess, ct);
        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, msg);
    }

    // ── Complete ───────────────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> CompleteEventAsync(long eventId, CancellationToken ct = default)
    {
        var affected = await calendarRepository.CompleteEventAsync(eventId, userContext.UserId, ct);
        if (affected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Calendar.EventNotFound, ct));
        }

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Calendar.EventCompleted, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, msg);
    }

    // ── Views ──────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<CalendarDayResponse>> GetByDayAsync(
        GetCalendarByDayRequest request, CancellationToken ct = default)
    {
        DateOnly.TryParse(request.Date, out var date);
        var rows = await calendarRepository.GetByDayAsync(userContext.UserId, date, ct);
        var msg  = await messageProvider.GetMessagesAsync(MessageKeys.Calendar.ListLoaded, ct);
        return ServiceResultFactory.Success(
            new CalendarDayResponse(rows.Select(MapRow).ToList()),
            InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<CalendarWeekResponse>> GetByWeekAsync(
        GetCalendarByWeekRequest request, CancellationToken ct = default)
    {
        DateOnly.TryParse(request.WeekStart, out var weekStart);
        var weekEnd = weekStart.AddDays(6);
        var rows    = await calendarRepository.GetByWeekAsync(userContext.UserId, weekStart, weekEnd, ct);
        var msg     = await messageProvider.GetMessagesAsync(MessageKeys.Calendar.ListLoaded, ct);
        return ServiceResultFactory.Success(
            new CalendarWeekResponse(rows.Select(MapRow).ToList()),
            InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<CalendarMonthResponse>> GetByMonthAsync(
        GetCalendarByMonthRequest request, CancellationToken ct = default)
    {
        var rows = await calendarRepository.GetByMonthAsync(
            userContext.UserId,
            (short)request.Year,
            (byte)request.Month,
            request.EventTypeId.HasValue ? (byte?)request.EventTypeId.Value : null,
            ct);

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Calendar.ListLoaded, ct);
        return ServiceResultFactory.Success(
            new CalendarMonthResponse(rows.Select(MapRow).ToList()),
            InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<CalendarAgendaResponse>> GetAgendaAsync(
        GetCalendarAgendaRequest request, CancellationToken ct = default)
    {
        DateOnly? startDate = null;
        if (!string.IsNullOrEmpty(request.StartDate) && DateOnly.TryParse(request.StartDate, out var sd))
            startDate = sd;

        var model = new GetAgendaDbModel
        {
            UserId     = userContext.UserId,
            StartDate  = startDate,
            DaysAhead  = request.DaysAhead,
            PageNumber = request.PageNumber,
            PageSize   = request.PageSize,
        };

        var result = await calendarRepository.GetAgendaAsync(model, ct);
        var msg    = await messageProvider.GetMessagesAsync(MessageKeys.Calendar.AgendaLoaded, ct);
        return ServiceResultFactory.Success(
            new CalendarAgendaResponse(
                result.TotalCount,
                request.PageNumber,
                request.PageSize,
                result.Items.Select(MapRow).ToList()),
            InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<CalendarDashboardResponse>> GetDashboardAsync(CancellationToken ct = default)
    {
        var data = await calendarRepository.GetDashboardAsync(userContext.UserId, ct);
        var msg  = await messageProvider.GetMessagesAsync(MessageKeys.Calendar.DashboardLoaded, ct);

        var response = new CalendarDashboardResponse(
            TodayEvents:       data.TodayEvents.Select(MapRow).ToList(),
            WeekSummary:       data.WeekSummary.Select(d =>
                new WeekDaySummary(d.EventDate.ToString("yyyy-MM-dd"), d.EventCount, d.IsToday)).ToList(),
            UpcomingBills:     data.UpcomingBills.Select(MapRow).ToList(),
            UpcomingGoals:     data.UpcomingGoals.Select(MapRow).ToList(),
            UpcomingRecurring: data.UpcomingRecurring.Select(MapRow).ToList());

        return ServiceResultFactory.Success(response, InternalResponseCodes.OK, msg);
    }

    public async Task<ServiceResult<CalendarSearchResponse>> SearchAsync(
        SearchCalendarRequest request, CancellationToken ct = default)
    {
        DateOnly? dateFrom = null, dateTo = null;
        if (!string.IsNullOrEmpty(request.DateFrom) && DateOnly.TryParse(request.DateFrom, out var df)) dateFrom = df;
        if (!string.IsNullOrEmpty(request.DateTo)   && DateOnly.TryParse(request.DateTo,   out var dt)) dateTo   = dt;

        var model = new SearchCalendarDbModel
        {
            UserId      = userContext.UserId,
            Keyword     = request.Keyword,
            EventTypeId = request.EventTypeId.HasValue ? (byte?)request.EventTypeId : null,
            SourceId    = request.SourceId.HasValue    ? (byte?)request.SourceId    : null,
            DateFrom    = dateFrom,
            DateTo      = dateTo,
            StatusId    = request.StatusId.HasValue  ? (byte?)request.StatusId  : null,
            Priority    = request.Priority.HasValue  ? (byte?)request.Priority  : null,
            PageNumber  = request.PageNumber,
            PageSize    = request.PageSize,
        };

        var result = await calendarRepository.SearchAsync(model, ct);
        var msg    = await messageProvider.GetMessagesAsync(MessageKeys.Calendar.SearchResultsLoaded, ct);
        return ServiceResultFactory.Success(
            new CalendarSearchResponse(
                result.TotalCount,
                request.PageNumber,
                request.PageSize,
                result.Items.Select(MapRow).ToList()),
            InternalResponseCodes.OK, msg);
    }

    // ── Reminder Dismiss ───────────────────────────────────────────────────────

    public async Task<ServiceResult<object?>> DismissReminderAsync(long reminderId, CancellationToken ct = default)
    {
        var affected = await calendarRepository.DismissReminderAsync(reminderId, userContext.UserId, ct);
        if (affected == 0)
        {
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Calendar.ReminderNotFound, ct));
        }

        var msg = await messageProvider.GetMessagesAsync(MessageKeys.Calendar.ReminderDismissed, ct);
        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, msg);
    }

    // ── Mapping ────────────────────────────────────────────────────────────────

    private static CalendarEventRow MapRow(CalendarEventRowDbResult r) =>
        new(
            EventId:           r.EventId,
            SourceId:          r.SourceId,
            EventTypeId:       r.EventTypeId,
            EventDate:         r.EventDate.ToString("yyyy-MM-dd"),
            TitleEn:           r.TitleEn,
            TitleAr:           r.TitleAr,
            Description:       r.Description,
            Priority:          r.Priority,
            StatusId:          r.StatusId,
            LinkedEntityTypeId: r.LinkedEntityTypeId,
            LinkedEntityId:    r.LinkedEntityId,
            ColorHex:          r.ColorHex,
            Icon:              r.Icon,
            AllDay:            r.AllDay,
            StartTime:         r.StartTime,
            EndTime:           r.EndTime,
            IsCompleted:       r.IsCompleted,
            NotifyBefore:      r.NotifyBefore,
            Amount:            r.Amount);
}
