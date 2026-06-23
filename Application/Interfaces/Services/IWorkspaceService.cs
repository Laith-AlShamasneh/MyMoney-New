using Application.Features.Workspace.DTOs;
using Shared.Results;

namespace Application.Interfaces.Services;

public interface IWorkspaceService
{
    // ── Workspaces ────────────────────────────────────────────
    Task<ServiceResult<CreateWorkspaceResponse>>              CreateAsync(CreateWorkspaceRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                              UpdateAsync(UpdateWorkspaceRequest request, CancellationToken ct = default);
    Task<ServiceResult<WorkspaceDto>>                         GetByIdAsync(long workspaceId, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<WorkspaceListItemDto>>>  GetListAsync(CancellationToken ct = default);
    Task<ServiceResult<object?>>                              DeleteAsync(long workspaceId, CancellationToken ct = default);
    Task<ServiceResult<object?>>                              SwitchCurrentAsync(long? workspaceId, CancellationToken ct = default);
    Task<ServiceResult<WorkspaceContextDto>>                  GetCurrentContextAsync(CancellationToken ct = default);

    // ── Members ───────────────────────────────────────────────
    Task<ServiceResult<IReadOnlyList<WorkspaceMemberDto>>>    GetMembersAsync(GetMembersRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                              UpdateMemberRoleAsync(UpdateMemberRoleRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                              SuspendMemberAsync(SuspendMemberRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                              ReinstateMemberAsync(ReinstateMemberRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                              RemoveMemberAsync(RemoveMemberRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                              LeaveAsync(LeaveWorkspaceRequest request, CancellationToken ct = default);

    // ── Invitations ───────────────────────────────────────────
    Task<ServiceResult<object?>>                              SendInvitationAsync(SendInvitationRequest request, CancellationToken ct = default);
    Task<ServiceResult<object?>>                              CancelInvitationAsync(CancelInvitationRequest request, CancellationToken ct = default);
    Task<ServiceResult<InvitationPreviewDto>>                 GetInvitationByTokenAsync(string token, CancellationToken ct = default);
    Task<ServiceResult<object?>>                              AcceptInvitationAsync(string token, CancellationToken ct = default);
    Task<ServiceResult<object?>>                              RejectInvitationAsync(string token, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<InvitationListItemDto>>> GetInvitationsAsync(GetInvitationsRequest request, CancellationToken ct = default);

    // ── Permissions ───────────────────────────────────────────
    Task<ServiceResult<IReadOnlyList<WorkspacePermissionDto>>> GetMyPermissionsAsync(long workspaceId, CancellationToken ct = default);

    // ── Activity ──────────────────────────────────────────────
    Task<ServiceResult<WorkspaceActivityResponse>>            GetActivityAsync(GetActivityRequest request, CancellationToken ct = default);
}
