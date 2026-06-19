using Application.Features.RecurringTransactions;
using Application.Features.RecurringTransactions.DTOs;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.RecurringTransactions;

public static class RecurringTransactionEndpoints
{
    public static void MapRecurringTransactionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/recurring-transactions")
                       .WithTags("RecurringTransactions")
                       .RequireAuthorization();

        group.MapPost("dashboard", async (
            IRecurringTransactionService service,
            CancellationToken            ct) =>
        {
            var result = await service.GetDashboardAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapPost("list", async (
            GetRecurringTransactionsRequest request,
            IRecurringTransactionService    service,
            CancellationToken               ct) =>
        {
            var result = await service.GetListAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetRecurringTransactionsRequest>>();

        group.MapPost("create", async (
            CreateRecurringTransactionRequest request,
            IRecurringTransactionService      service,
            CancellationToken                 ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<CreateRecurringTransactionRequest>>();

        group.MapPost("get", async (
            RecurringTransactionIdRequest request,
            IRecurringTransactionService  service,
            CancellationToken             ct) =>
        {
            var result = await service.GetByIdAsync(request.Id, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<RecurringTransactionIdRequest>>();

        group.MapPost("update", async (
            UpdateRecurringTransactionRequest request,
            IRecurringTransactionService      service,
            CancellationToken                 ct) =>
        {
            var result = await service.UpdateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<UpdateRecurringTransactionRequest>>();

        group.MapPost("delete", async (
            RecurringTransactionIdRequest request,
            IRecurringTransactionService  service,
            CancellationToken             ct) =>
        {
            var result = await service.DeleteAsync(request.Id, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<RecurringTransactionIdRequest>>();

        group.MapPost("pause", async (
            RecurringTransactionIdRequest request,
            IRecurringTransactionService  service,
            CancellationToken             ct) =>
        {
            var result = await service.PauseAsync(request.Id, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<RecurringTransactionIdRequest>>();

        group.MapPost("resume", async (
            RecurringTransactionIdRequest request,
            IRecurringTransactionService  service,
            CancellationToken             ct) =>
        {
            var result = await service.ResumeAsync(request.Id, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<RecurringTransactionIdRequest>>();
    }
}
