using Application.Features.Notifications;
using Application.Features.Notifications.DTOs;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Notifications;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/notifications")
                       .WithTags("Notifications")
                       .RequireAuthorization();

        group.MapPost("list", async (
            GetNotificationsRequest request,
            INotificationService    service,
            CancellationToken       ct) =>
        {
            var result = await service.GetListAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetNotificationsRequest>>();

        group.MapPost("unread-count", async (
            INotificationService service,
            CancellationToken    ct) =>
        {
            var result = await service.GetUnreadCountAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapPost("mark-read", async (
            MarkReadRequest      request,
            INotificationService service,
            CancellationToken    ct) =>
        {
            var result = await service.MarkReadAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<MarkReadRequest>>();

        group.MapPost("mark-all-read", async (
            INotificationService service,
            CancellationToken    ct) =>
        {
            var result = await service.MarkAllReadAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapPost("archive", async (
            ArchiveNotificationRequest request,
            INotificationService       service,
            CancellationToken          ct) =>
        {
            var result = await service.ArchiveAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<ArchiveNotificationRequest>>();

        group.MapPost("dismiss", async (
            DismissNotificationRequest request,
            INotificationService       service,
            CancellationToken          ct) =>
        {
            var result = await service.DismissAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<DismissNotificationRequest>>();

        group.MapPost("delete", async (
            DeleteNotificationRequest request,
            INotificationService      service,
            CancellationToken         ct) =>
        {
            var result = await service.DeleteAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<DeleteNotificationRequest>>();

        group.MapPost("preferences", async (
            INotificationService service,
            CancellationToken    ct) =>
        {
            var result = await service.GetPreferencesAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapPost("preferences/update", async (
            UpdatePreferencesRequest request,
            INotificationService     service,
            CancellationToken        ct) =>
        {
            var result = await service.UpdatePreferencesAsync(request, ct);
            return result.ToHttpResponse();
        });
    }
}
