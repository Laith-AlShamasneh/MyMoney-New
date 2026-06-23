using Application.Features.Workspace.DbModels;

namespace Application.Interfaces.Repositories;

public interface IWorkspaceRepository
{
    // ── Workspaces ────────────────────────────────────────────
    Task<long>                                   CreateAsync(CreateWorkspaceDbModel model, CancellationToken ct = default);
    Task<int>                                    UpdateAsync(UpdateWorkspaceDbModel model, CancellationToken ct = default);
    Task<WorkspaceDbResult?>                     GetByIdAsync(long workspaceId, long callerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkspaceListItemDbResult>> GetListAsync(long callerUserId, CancellationToken ct = default);
    Task<int>                                    DeleteAsync(long workspaceId, long callerUserId, CancellationToken ct = default);
    Task<bool>                                   SwitchCurrentAsync(long callerUserId, long? workspaceId, CancellationToken ct = default);
    Task<WorkspaceContextDbResult?>              GetCurrentContextAsync(long callerUserId, CancellationToken ct = default);

    // ── Members ───────────────────────────────────────────────
    Task<IReadOnlyList<WorkspaceMemberDbResult>> GetMembersAsync(long workspaceId, long callerUserId, byte? statusId, CancellationToken ct = default);
    Task<int>                                    UpdateMemberRoleAsync(long workspaceId, long callerUserId, long targetUserId, byte newRoleId, CancellationToken ct = default);
    Task<int>                                    SuspendMemberAsync(long workspaceId, long callerUserId, long targetUserId, CancellationToken ct = default);
    Task<int>                                    ReinstateMemberAsync(long workspaceId, long callerUserId, long targetUserId, CancellationToken ct = default);
    Task<int>                                    RemoveMemberAsync(long workspaceId, long callerUserId, long targetUserId, CancellationToken ct = default);
    Task<int>                                    LeaveAsync(long workspaceId, long callerUserId, CancellationToken ct = default);
    Task<MyRoleDbResult?>                        GetMyRoleAsync(long workspaceId, long callerUserId, CancellationToken ct = default);

    // ── Invitations ───────────────────────────────────────────
    Task<long>                                   SendInvitationAsync(SendInvitationDbModel model, CancellationToken ct = default);
    Task<int>                                    CancelInvitationAsync(long invitationId, long workspaceId, long callerUserId, CancellationToken ct = default);
    Task<InvitationByTokenDbResult?>             GetInvitationByTokenAsync(string tokenHash, CancellationToken ct = default);
    Task<int>                                    AcceptInvitationAsync(string tokenHash, long callerUserId, CancellationToken ct = default);
    Task<int>                                    RejectInvitationAsync(string tokenHash, long callerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<InvitationListItemDbResult>> GetInvitationsAsync(long workspaceId, long callerUserId, byte? statusId, CancellationToken ct = default);

    // ── Permissions ───────────────────────────────────────────
    Task<IReadOnlyList<WorkspacePermissionDbResult>> GetMyPermissionsAsync(long workspaceId, long callerUserId, CancellationToken ct = default);

    // ── Activity ──────────────────────────────────────────────
    Task<(int TotalCount, IReadOnlyList<WorkspaceActivityDbResult> Items)> GetActivityAsync(long workspaceId, long callerUserId, int pageNumber, int pageSize, CancellationToken ct = default);
}
