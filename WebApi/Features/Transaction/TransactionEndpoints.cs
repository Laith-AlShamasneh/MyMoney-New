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

        group.MapGet("get/{id:long}", async (
            long                id,
            ITransactionService service,
            CancellationToken   ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.ToHttpResponse();
        });

        group.MapPost("create", async (
            CreateTransactionRequest request,
            ITransactionService      service,
            CancellationToken        ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<CreateTransactionRequest>>();

        group.MapPut("update/{id:long}", async (
            long                     id,
            UpdateTransactionRequest request,
            ITransactionService      service,
            CancellationToken        ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<UpdateTransactionRequest>>();

        group.MapDelete("delete/{id:long}", async (
            long                id,
            ITransactionService service,
            CancellationToken   ct) =>
        {
            var result = await service.DeleteAsync(id, ct);
            return result.ToHttpResponse();
        });
    }
}
