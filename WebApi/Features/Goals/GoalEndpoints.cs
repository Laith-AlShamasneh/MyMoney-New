using Application.Features.Goals;
using Application.Features.Goals.DTOs;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Goals;

public static class GoalEndpoints
{
    public static void MapGoalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/goals")
                       .WithTags("Goals")
                       .RequireAuthorization();

        group.MapPost("dashboard", async (
            IGoalService      service,
            CancellationToken ct) =>
        {
            var result = await service.GetDashboardAsync(ct);
            return result.ToHttpResponse();
        });

        group.MapPost("list", async (
            GetGoalListRequest request,
            IGoalService       service,
            CancellationToken  ct) =>
        {
            var result = await service.GetListAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetGoalListRequest>>();

        group.MapPost("get", async (
            GetGoalRequest    request,
            IGoalService      service,
            CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(request.Id, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetGoalRequest>>();

        group.MapPost("create", async (
            CreateGoalRequest request,
            IGoalService      service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<CreateGoalRequest>>();

        group.MapPost("update", async (
            UpdateGoalRequest request,
            IGoalService      service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<UpdateGoalRequest>>();

        group.MapPost("delete", async (
            DeleteGoalRequest request,
            IGoalService      service,
            CancellationToken ct) =>
        {
            var result = await service.DeleteAsync(request.Id, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<DeleteGoalRequest>>();

        group.MapPost("pause", async (
            PauseGoalRequest  request,
            IGoalService      service,
            CancellationToken ct) =>
        {
            var result = await service.PauseAsync(request.Id, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<PauseGoalRequest>>();

        group.MapPost("resume", async (
            ResumeGoalRequest request,
            IGoalService      service,
            CancellationToken ct) =>
        {
            var result = await service.ResumeAsync(request.Id, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<ResumeGoalRequest>>();

        group.MapPost("contribute", async (
            AddContributionRequest request,
            IGoalService           service,
            CancellationToken      ct) =>
        {
            var result = await service.AddContributionAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<AddContributionRequest>>();

        group.MapPost("withdraw", async (
            WithdrawRequest   request,
            IGoalService      service,
            CancellationToken ct) =>
        {
            var result = await service.WithdrawAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<WithdrawRequest>>();

        group.MapPost("adjust", async (
            AdjustGoalRequest request,
            IGoalService      service,
            CancellationToken ct) =>
        {
            var result = await service.AdjustAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<AdjustGoalRequest>>();

        group.MapPost("contributions", async (
            GetContributionsRequest request,
            IGoalService            service,
            CancellationToken       ct) =>
        {
            var result = await service.GetContributionsAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetContributionsRequest>>();

        group.MapPost("link-recurring", async (
            LinkRecurringRequest request,
            IGoalService         service,
            CancellationToken    ct) =>
        {
            var result = await service.LinkRecurringAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<LinkRecurringRequest>>();

        group.MapPost("unlink-recurring", async (
            UnlinkRecurringRequest request,
            IGoalService           service,
            CancellationToken      ct) =>
        {
            var result = await service.UnlinkRecurringAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<UnlinkRecurringRequest>>();
    }
}
