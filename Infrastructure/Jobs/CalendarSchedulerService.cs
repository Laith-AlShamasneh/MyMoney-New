using Application.Common.Constants;
using Application.Features.Calendar.Jobs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

internal sealed class CalendarSchedulerService(
    IServiceScopeFactory              scopeFactory,
    ILogger<CalendarSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval  = TimeSpan.FromMinutes(1);
    private static readonly int      WindowMinutes = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 3 minutes after startup before first tick
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Calendar scheduler tick failed.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope      = scopeFactory.CreateScope();
        var calendarRepo     = scope.ServiceProvider.GetRequiredService<ICalendarRepository>();
        var backgroundJobSvc = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();

        // Expire stale reminders (event long past / deleted / completed) — idempotent.
        try
        {
            var expired = await calendarRepo.ExpireRemindersAsync(ct);
            if (expired > 0)
                logger.LogInformation("Calendar: Expired {Count} stale reminder(s).", expired);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Calendar: Reminder expiry sweep failed.");
        }

        var pendingReminders = await calendarRepo.GetPendingRemindersAsync(WindowMinutes, ct);

        if (pendingReminders.Count == 0) return;

        logger.LogInformation("Calendar: Processing {Count} due reminder(s).", pendingReminders.Count);

        foreach (var reminder in pendingReminders)
        {
            try
            {
                var payload = new CalendarReminderPayload(
                    ReminderId:  reminder.ReminderId,
                    EventId:     reminder.EventId,
                    UserId:      reminder.UserId,
                    EventTitle:  reminder.Title,
                    EventDate:   reminder.EventDate.ToString("yyyy-MM-dd"),
                    EventTypeId: reminder.EventTypeId);

                await backgroundJobSvc.EnqueueAsync(
                    JobTypes.CalendarReminder,
                    payload,
                    priority: 2,
                    ct: ct);

                // Mark as sent so we don't re-process on the next tick
                await calendarRepo.MarkReminderSentAsync(reminder.ReminderId, jobId: null, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Calendar: Failed to enqueue reminder {ReminderId} for event {EventId}.",
                    reminder.ReminderId, reminder.EventId);
            }
        }
    }
}
