using Application.Features.Workspace.DbModels;
using Application.Interfaces.Database;
using Application.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Infrastructure.Services.Workspace;

internal sealed class WorkspaceRepository(IDbExecutor db) : IWorkspaceRepository
{
    // ── Workspaces ────────────────────────────────────────────

    public async Task<long> CreateAsync(CreateWorkspaceDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@OwnerUserId",  model.OwnerUserId,  DbType.Int64);
        p.Add("@Name",         model.Name,          DbType.String);
        p.Add("@Description",  model.Description,   DbType.String);
        p.Add("@TypeId",       model.TypeId,         DbType.Byte);
        p.Add("@CurrencyCode", model.CurrencyCode,   DbType.String);
        p.Add("@Timezone",     model.Timezone,        DbType.String);
        p.Add("@Color",        model.Color,           DbType.String);

        var result = await db.QuerySingleAsync<WorkspaceIdRow>("MyMoney.usp_Workspace_Create", p, ct);
        return result?.WorkspaceId ?? 0;
    }

    public async Task<int> UpdateAsync(UpdateWorkspaceDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  model.WorkspaceId,  DbType.Int64);
        p.Add("@CallerUserId", model.CallerUserId,  DbType.Int64);
        p.Add("@Name",         model.Name,          DbType.String);
        p.Add("@Description",  model.Description,   DbType.String);
        p.Add("@CurrencyCode", model.CurrencyCode,   DbType.String);
        p.Add("@Timezone",     model.Timezone,        DbType.String);
        p.Add("@Color",        model.Color,           DbType.String);

        var result = await db.QuerySingleAsync<AffectedRowsDbResult>("MyMoney.usp_Workspace_Update", p, ct);
        return result?.AffectedRows ?? 0;
    }

    public async Task<WorkspaceDbResult?> GetByIdAsync(
        long workspaceId, long callerUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);

        return await db.QuerySingleAsync<WorkspaceDbResult>("MyMoney.usp_Workspace_GetById", p, ct);
    }

    public async Task<IReadOnlyList<WorkspaceListItemDbResult>> GetListAsync(
        long callerUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@CallerUserId", callerUserId, DbType.Int64);

        return await db.QueryListAsync<WorkspaceListItemDbResult>("MyMoney.usp_Workspace_GetList", p, ct);
    }

    public async Task<int> DeleteAsync(
        long workspaceId, long callerUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);

        var result = await db.QuerySingleAsync<AffectedRowsDbResult>("MyMoney.usp_Workspace_Delete", p, ct);
        return result?.AffectedRows ?? 0;
    }

    public async Task<bool> SwitchCurrentAsync(
        long callerUserId, long? workspaceId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@CallerUserId", callerUserId, DbType.Int64);
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);

        var result = await db.QuerySingleAsync<SuccessRow>("MyMoney.usp_Workspace_SwitchCurrent", p, ct);
        return result?.Success == 1;
    }

    public async Task<WorkspaceContextDbResult?> GetCurrentContextAsync(
        long callerUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@CallerUserId", callerUserId, DbType.Int64);

        return await db.QuerySingleAsync<WorkspaceContextDbResult>("MyMoney.usp_Workspace_GetCurrentContext", p, ct);
    }

    // ── Members ───────────────────────────────────────────────

    public async Task<IReadOnlyList<WorkspaceMemberDbResult>> GetMembersAsync(
        long workspaceId, long callerUserId, byte? statusId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);
        p.Add("@StatusId",     statusId,     DbType.Byte);

        return await db.QueryListAsync<WorkspaceMemberDbResult>("MyMoney.usp_WorkspaceMember_GetList", p, ct);
    }

    public async Task<int> UpdateMemberRoleAsync(
        long workspaceId, long callerUserId, long targetUserId, byte newRoleId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);
        p.Add("@TargetUserId", targetUserId, DbType.Int64);
        p.Add("@NewRoleId",    newRoleId,    DbType.Byte);

        var result = await db.QuerySingleAsync<AffectedRowsDbResult>("MyMoney.usp_WorkspaceMember_UpdateRole", p, ct);
        return result?.AffectedRows ?? 0;
    }

    public async Task<int> SuspendMemberAsync(
        long workspaceId, long callerUserId, long targetUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);
        p.Add("@TargetUserId", targetUserId, DbType.Int64);

        var result = await db.QuerySingleAsync<AffectedRowsDbResult>("MyMoney.usp_WorkspaceMember_Suspend", p, ct);
        return result?.AffectedRows ?? 0;
    }

    public async Task<int> ReinstateMemberAsync(
        long workspaceId, long callerUserId, long targetUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);
        p.Add("@TargetUserId", targetUserId, DbType.Int64);

        var result = await db.QuerySingleAsync<AffectedRowsDbResult>("MyMoney.usp_WorkspaceMember_Reinstate", p, ct);
        return result?.AffectedRows ?? 0;
    }

    public async Task<int> RemoveMemberAsync(
        long workspaceId, long callerUserId, long targetUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);
        p.Add("@TargetUserId", targetUserId, DbType.Int64);

        var result = await db.QuerySingleAsync<AffectedRowsDbResult>("MyMoney.usp_WorkspaceMember_Remove", p, ct);
        return result?.AffectedRows ?? 0;
    }

    public async Task<int> LeaveAsync(
        long workspaceId, long callerUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);

        var result = await db.QuerySingleAsync<AffectedRowsDbResult>("MyMoney.usp_WorkspaceMember_Leave", p, ct);
        return result?.AffectedRows ?? 0;
    }

    public async Task<MyRoleDbResult?> GetMyRoleAsync(
        long workspaceId, long callerUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);

        return await db.QuerySingleAsync<MyRoleDbResult>("MyMoney.usp_WorkspaceMember_GetMyRole", p, ct);
    }

    // ── Invitations ───────────────────────────────────────────

    public async Task<long> SendInvitationAsync(SendInvitationDbModel model, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",     model.WorkspaceId,   DbType.Int64);
        p.Add("@CallerUserId",    model.CallerUserId,   DbType.Int64);
        p.Add("@Email",           model.Email,           DbType.String);
        p.Add("@RoleId",          model.RoleId,          DbType.Byte);
        p.Add("@TokenHash",       model.TokenHash,        DbType.String);
        p.Add("@ExpiresAtUtc",    model.ExpiresAtUtc,    DbType.DateTime2);

        var result = await db.QuerySingleAsync<InvitationIdRow>("MyMoney.usp_WorkspaceInvitation_Send", p, ct);
        return result?.InvitationId ?? 0;
    }

    public async Task<int> CancelInvitationAsync(
        long invitationId, long workspaceId, long callerUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@InvitationId", invitationId, DbType.Int64);
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);

        var result = await db.QuerySingleAsync<AffectedRowsDbResult>("MyMoney.usp_WorkspaceInvitation_Cancel", p, ct);
        return result?.AffectedRows ?? 0;
    }

    public async Task<InvitationByTokenDbResult?> GetInvitationByTokenAsync(
        string tokenHash, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@TokenHash", tokenHash, DbType.String);

        return await db.QuerySingleAsync<InvitationByTokenDbResult>("MyMoney.usp_WorkspaceInvitation_GetByToken", p, ct);
    }

    public async Task<int> AcceptInvitationAsync(
        string tokenHash, long callerUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@TokenHash",    tokenHash,    DbType.String);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);

        var result = await db.QuerySingleAsync<ResultRow>("MyMoney.usp_WorkspaceInvitation_Accept", p, ct);
        return result?.Result ?? 0;
    }

    public async Task<int> RejectInvitationAsync(
        string tokenHash, long callerUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@TokenHash",    tokenHash,    DbType.String);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);

        var result = await db.QuerySingleAsync<ResultRow>("MyMoney.usp_WorkspaceInvitation_Reject", p, ct);
        return result?.Result ?? 0;
    }

    public async Task<IReadOnlyList<InvitationListItemDbResult>> GetInvitationsAsync(
        long workspaceId, long callerUserId, byte? statusId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);
        p.Add("@StatusId",     statusId,     DbType.Byte);

        return await db.QueryListAsync<InvitationListItemDbResult>("MyMoney.usp_WorkspaceInvitation_GetList", p, ct);
    }

    // ── Permissions ───────────────────────────────────────────

    public async Task<IReadOnlyList<WorkspacePermissionDbResult>> GetMyPermissionsAsync(
        long workspaceId, long callerUserId, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);

        return await db.QueryListAsync<WorkspacePermissionDbResult>("MyMoney.usp_WorkspacePermission_GetMyPermissions", p, ct);
    }

    // ── Activity ──────────────────────────────────────────────

    public async Task<(int TotalCount, IReadOnlyList<WorkspaceActivityDbResult> Items)> GetActivityAsync(
        long workspaceId, long callerUserId, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("@WorkspaceId",  workspaceId,  DbType.Int64);
        p.Add("@CallerUserId", callerUserId, DbType.Int64);
        p.Add("@PageNumber",   pageNumber,   DbType.Int32);
        p.Add("@PageSize",     pageSize,     DbType.Int32);

        return await db.QueryMultipleAsync<(int, IReadOnlyList<WorkspaceActivityDbResult>)>(
            "MyMoney.usp_WorkspaceActivity_GetList",
            async multi =>
            {
                var countRow = await multi.ReadFirstOrDefaultAsync<ActivityTotalCountDbResult>();
                var items    = (await multi.ReadAsync<WorkspaceActivityDbResult>()).ToList();
                return (countRow?.TotalCount ?? 0, items);
            },
            p, ct);
    }

    // ── Private row-mapping types ─────────────────────────────

    private sealed record WorkspaceIdRow(long WorkspaceId);
    private sealed record InvitationIdRow(long InvitationId);
    private sealed record SuccessRow(int Success);
    private sealed record ResultRow(int Result);
}
