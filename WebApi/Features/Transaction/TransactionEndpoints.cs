using Application.Features.Transaction.DTOs;
using Application.Interfaces.Services;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Transaction;

public static class TransactionEndpoints
{
    public static void MapTransactionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/transactions")
                       .WithTags("Transactions")
                       .RequireAuthorization();

        group.MapPost("search", async (
            SearchTransactionsRequest request,
            ITransactionService       service,
            CancellationToken         ct) =>
        {
            var result = await service.SearchAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<SearchTransactionsRequest>>();

        group.MapPost("analytics", async (
            GetAnalyticsRequest request,
            ITransactionService service,
            CancellationToken   ct) =>
        {
            var result = await service.GetAnalyticsAsync(request, ct);
            return result.ToHttpResponse();
        });

        group.MapPost("get", async (
            GetTransactionRequest request,
            ITransactionService   service,
            CancellationToken     ct) =>
        {
            var result = await service.GetByIdAsync(request.Id, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetTransactionRequest>>();

        group.MapPost("create", async (
            CreateTransactionRequest request,
            ITransactionService      service,
            CancellationToken        ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<CreateTransactionRequest>>();

        group.MapPost("update", async (
            UpdateTransactionRequest request,
            ITransactionService      service,
            CancellationToken        ct) =>
        {
            var result = await service.UpdateAsync(request.Id, request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<UpdateTransactionRequest>>();

        group.MapPost("delete", async (
            DeleteTransactionRequest request,
            ITransactionService      service,
            CancellationToken        ct) =>
        {
            var result = await service.DeleteAsync(request.Id, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<DeleteTransactionRequest>>();
    }
}
