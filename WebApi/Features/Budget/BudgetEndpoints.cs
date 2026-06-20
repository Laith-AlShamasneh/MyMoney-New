using Application.Features.Budget;
using Application.Features.Budget.DTOs;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Budget;

public static class BudgetEndpoints
{
    public static void MapBudgetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/budgets")
                       .WithTags("Budgets")
                       .RequireAuthorization();

        group.MapPost("dashboard", async (
            IBudgetService    service,
            CancellationToken ct) =>
        {
            var result = await service.GetDashboardAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapPost("list", async (
            GetBudgetListRequest request,
            IBudgetService       service,
            CancellationToken    ct) =>
        {
            var result = await service.GetListAsync(request.StatusId, ct);
            return result.ToHttpResponse();
        });

        group.MapPost("get", async (
            GetBudgetRequest  request,
            IBudgetService    service,
            CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(request.Id, ct);
            return result.ToHttpResponse();
        });

        group.MapPost("create", async (
            CreateBudgetRequest request,
            IBudgetService      service,
            CancellationToken   ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<CreateBudgetRequest>>();

        group.MapPost("update", async (
            UpdateBudgetRequest request,
            IBudgetService      service,
            CancellationToken   ct) =>
        {
            var result = await service.UpdateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<UpdateBudgetRequest>>();

        group.MapPost("delete", async (
            DeleteBudgetRequest request,
            IBudgetService      service,
            CancellationToken   ct) =>
        {
            var result = await service.DeleteAsync(request.Id, ct);
            return result.ToHttpResponse();
        });

        group.MapPost("pause", async (
            PauseBudgetRequest request,
            IBudgetService     service,
            CancellationToken  ct) =>
        {
            var result = await service.PauseAsync(request.Id, ct);
            return result.ToHttpResponse();
        });

        group.MapPost("resume", async (
            ResumeBudgetRequest request,
            IBudgetService      service,
            CancellationToken   ct) =>
        {
            var result = await service.ResumeAsync(request.Id, ct);
            return result.ToHttpResponse();
        });

        group.MapPost("periods", async (
            GetBudgetPeriodsRequest request,
            IBudgetService          service,
            CancellationToken       ct) =>
        {
            var result = await service.GetPeriodsAsync(request.Id, request.PageNumber, request.PageSize, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetBudgetPeriodsRequest>>();

        group.MapPost("analytics", async (
            GetBudgetAnalyticsRequest request,
            IBudgetService            service,
            CancellationToken         ct) =>
        {
            var result = await service.GetAnalyticsAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetBudgetAnalyticsRequest>>();
    }
}
