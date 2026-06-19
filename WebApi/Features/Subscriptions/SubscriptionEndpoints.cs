using Application.Features.RecurringTransactions;
using Application.Features.RecurringTransactions.DTOs;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Subscriptions;

public static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/subscriptions")
                       .WithTags("Subscriptions")
                       .RequireAuthorization();

        group.MapPost("list", async (
            GetSubscriptionsRequest      request,
            IRecurringTransactionService service,
            CancellationToken            ct) =>
        {
            var result = await service.GetSubscriptionsAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetSubscriptionsRequest>>();

        group.MapPost("create", async (
            CreateSubscriptionRequest    request,
            IRecurringTransactionService service,
            CancellationToken            ct) =>
        {
            var result = await service.CreateSubscriptionAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<CreateSubscriptionRequest>>();

        group.MapPost("update", async (
            UpdateSubscriptionRequest    request,
            IRecurringTransactionService service,
            CancellationToken            ct) =>
        {
            var result = await service.UpdateSubscriptionAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<UpdateSubscriptionRequest>>();

        group.MapPost("pause", async (
            RecurringTransactionIdRequest request,
            IRecurringTransactionService  service,
            CancellationToken             ct) =>
        {
            var result = await service.PauseAsync(request.Id, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<RecurringTransactionIdRequest>>();

        group.MapPost("delete", async (
            RecurringTransactionIdRequest request,
            IRecurringTransactionService  service,
            CancellationToken             ct) =>
        {
            var result = await service.DeleteAsync(request.Id, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<RecurringTransactionIdRequest>>();
    }
}
