using Application.Features.Workspace.DTOs;
using Application.Interfaces.Services;
using WebApi.Common.Extensions;
using WebApi.Common.Filters;

namespace WebApi.Features.Workspace;

public static class WorkspaceEndpoints
{
    public static void MapWorkspaceEndpoints(this WebApplication app)
    {
        // ── Workspace management ──────────────────────────────────────────────

        var ws = app.MapGroup("api/workspaces")
                    .WithTags("Workspaces")
                    .RequireAuthorization();

        ws.MapPost("create", async (
            CreateWorkspaceRequest request,
            IWorkspaceService      service,
            CancellationToken      ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<CreateWorkspaceRequest>>();

        ws.MapPost("update", async (
            UpdateWorkspaceRequest request,
            IWorkspaceService      service,
            CancellationToken      ct) =>
        {
            var result = await service.UpdateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<UpdateWorkspaceRequest>>();

        ws.MapPost("get", async (
            GetWorkspaceRequest request,
            IWorkspaceService   service,
            CancellationToken   ct) =>
        {
            var result = await service.GetByIdAsync(request.WorkspaceId, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetWorkspaceRequest>>();

        ws.MapPost("list", async (
            IWorkspaceService service,
            CancellationToken ct) =>
        {
            var result = await service.GetListAsync(ct);
            return result.ToHttpResponse();
        });

        ws.MapPost("delete", async (
            DeleteWorkspaceRequest request,
            IWorkspaceService      service,
            CancellationToken      ct) =>
        {
            var result = await service.DeleteAsync(request.WorkspaceId, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<DeleteWorkspaceRequest>>();

        ws.MapPost("switch", async (
            SwitchWorkspaceRequest request,
            IWorkspaceService      service,
            CancellationToken      ct) =>
        {
            var result = await service.SwitchCurrentAsync(request.WorkspaceId, ct);
            return result.ToHttpResponse();
        });

        ws.MapPost("context", async (
            IWorkspaceService service,
            CancellationToken ct) =>
        {
            var result = await service.GetCurrentContextAsync(ct);
            return result.ToHttpResponse();
        });

        // ── Members ───────────────────────────────────────────────────────────

        var members = app.MapGroup("api/workspaces/members")
                         .WithTags("Workspace Members")
                         .RequireAuthorization();

        members.MapPost("list", async (
            GetMembersRequest request,
            IWorkspaceService service,
            CancellationToken ct) =>
        {
            var result = await service.GetMembersAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetMembersRequest>>();

        members.MapPost("update-role", async (
            UpdateMemberRoleRequest request,
            IWorkspaceService       service,
            CancellationToken       ct) =>
        {
            var result = await service.UpdateMemberRoleAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<UpdateMemberRoleRequest>>();

        members.MapPost("suspend", async (
            SuspendMemberRequest request,
            IWorkspaceService    service,
            CancellationToken    ct) =>
        {
            var result = await service.SuspendMemberAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<SuspendMemberRequest>>();

        members.MapPost("reinstate", async (
            ReinstateMemberRequest request,
            IWorkspaceService      service,
            CancellationToken      ct) =>
        {
            var result = await service.ReinstateMemberAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<ReinstateMemberRequest>>();

        members.MapPost("remove", async (
            RemoveMemberRequest request,
            IWorkspaceService   service,
            CancellationToken   ct) =>
        {
            var result = await service.RemoveMemberAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<RemoveMemberRequest>>();

        members.MapPost("leave", async (
            LeaveWorkspaceRequest request,
            IWorkspaceService     service,
            CancellationToken     ct) =>
        {
            var result = await service.LeaveAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<LeaveWorkspaceRequest>>();

        // ── Invitations ───────────────────────────────────────────────────────

        var invitations = app.MapGroup("api/workspaces/invitations")
                             .WithTags("Workspace Invitations")
                             .RequireAuthorization();

        invitations.MapPost("send", async (
            SendInvitationRequest request,
            IWorkspaceService     service,
            CancellationToken     ct) =>
        {
            var result = await service.SendInvitationAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<SendInvitationRequest>>();

        invitations.MapPost("cancel", async (
            CancelInvitationRequest request,
            IWorkspaceService       service,
            CancellationToken       ct) =>
        {
            var result = await service.CancelInvitationAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<CancelInvitationRequest>>();

        invitations.MapPost("preview", async (
            GetInvitationsByTokenRequest request,
            IWorkspaceService            service,
            CancellationToken            ct) =>
        {
            var result = await service.GetInvitationByTokenAsync(request.Token, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetInvitationsByTokenRequest>>();

        invitations.MapPost("accept", async (
            AcceptInvitationRequest request,
            IWorkspaceService       service,
            CancellationToken       ct) =>
        {
            var result = await service.AcceptInvitationAsync(request.Token, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<AcceptInvitationRequest>>();

        invitations.MapPost("reject", async (
            RejectInvitationRequest request,
            IWorkspaceService       service,
            CancellationToken       ct) =>
        {
            var result = await service.RejectInvitationAsync(request.Token, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<RejectInvitationRequest>>();

        invitations.MapPost("list", async (
            GetInvitationsRequest request,
            IWorkspaceService     service,
            CancellationToken     ct) =>
        {
            var result = await service.GetInvitationsAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetInvitationsRequest>>();

        // ── Permissions ───────────────────────────────────────────────────────

        var permissions = app.MapGroup("api/workspaces/permissions")
                             .WithTags("Workspace Permissions")
                             .RequireAuthorization();

        permissions.MapPost("my", async (
            GetPermissionsRequest request,
            IWorkspaceService     service,
            CancellationToken     ct) =>
        {
            var result = await service.GetMyPermissionsAsync(request.WorkspaceId, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetPermissionsRequest>>();

        // ── Activity feed ─────────────────────────────────────────────────────

        var activity = app.MapGroup("api/workspaces/activity")
                          .WithTags("Workspace Activity")
                          .RequireAuthorization();

        activity.MapPost("list", async (
            GetActivityRequest request,
            IWorkspaceService  service,
            CancellationToken  ct) =>
        {
            var result = await service.GetActivityAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetActivityRequest>>();
    }
}
