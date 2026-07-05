using Application.Common.Constants;
using Application.Common.Options;
using Application.Features.Email.Jobs;
using Application.Features.Workspace.DbModels;
using Application.Features.Workspace.DTOs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Microsoft.Extensions.Options;
using Shared.Constants;
using Shared.Enums.System;
using Shared.Enums.Workspace;
using Shared.Results;

namespace Application.Features.Workspace.Services;

internal sealed class WorkspaceService(
    IWorkspaceRepository            workspaceRepository,
    IUserContext                   userContext,
    IMessageProvider               messageProvider,
    IBackgroundJobService          backgroundJobService,
    ITokenHasher                   tokenHasher,
    IStorageUtility                storageUtility,
    IOptions<AuthenticationOptions> authOptions) : IWorkspaceService
{
    private const int InvitationExpiryDays = 7;

    // ── Workspaces ────────────────────────────────────────────

    public async Task<ServiceResult<CreateWorkspaceResponse>> CreateAsync(
        CreateWorkspaceRequest request,
        CancellationToken      ct = default)
    {
        var model = new CreateWorkspaceDbModel
        {
            OwnerUserId  = userContext.UserId,
            Name         = request.Name.Trim(),
            Description  = request.Description?.Trim(),
            TypeId       = request.TypeId,
            CurrencyCode = request.CurrencyCode?.Trim().ToUpperInvariant(),
            Timezone     = request.Timezone?.Trim(),
            Color        = request.Color?.Trim(),
        };

        var workspaceId = await workspaceRepository.CreateAsync(model, ct);

        var message = await messageProvider.GetMessagesAsync(MessageKeys.Workspace.Created, ct);
        return ServiceResultFactory.Success(new CreateWorkspaceResponse(workspaceId), InternalResponseCodes.Created, message);
    }

    public async Task<ServiceResult<object?>> UpdateAsync(
        UpdateWorkspaceRequest request,
        CancellationToken      ct = default)
    {
        var model = new UpdateWorkspaceDbModel
        {
            WorkspaceId  = request.WorkspaceId,
            CallerUserId = userContext.UserId,
            Name         = request.Name.Trim(),
            Description  = request.Description?.Trim(),
            CurrencyCode = request.CurrencyCode?.Trim().ToUpperInvariant(),
            Timezone     = request.Timezone?.Trim(),
            Color        = request.Color?.Trim(),
        };

        var rows = await workspaceRepository.UpdateAsync(model, ct);

        if (rows == -1)
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.Forbidden,
                await messageProvider.GetMessagesAsync(MessageKeys.Workspace.Forbidden, ct));

        if (rows == 0)
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Workspace.NotFound, ct));

        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Workspace.Updated, ct));
    }

    public async Task<ServiceResult<WorkspaceDto>> GetByIdAsync(
        long workspaceId, CancellationToken ct = default)
    {
        var db = await workspaceRepository.GetByIdAsync(workspaceId, userContext.UserId, ct);

        if (db is null)
            return ServiceResultFactory.Failure<WorkspaceDto>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Workspace.NotFound, ct));

        var dto = MapWorkspaceDto(db);
        return ServiceResultFactory.Success(dto, InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Workspace.GetSuccess, ct));
    }

    public async Task<ServiceResult<IReadOnlyList<WorkspaceListItemDto>>> GetListAsync(
        CancellationToken ct = default)
    {
        var items = await workspaceRepository.GetListAsync(userContext.UserId, ct);

        var dtos = items
            .Select(w => new WorkspaceListItemDto(
                w.WorkspaceId, w.OwnerUserId, w.Name, w.TypeId,
                w.CurrencyCode, w.LogoFileName, w.Color, w.CreatedAtUtc,
                w.RoleId, w.RoleCode, w.ActiveMemberCount, w.IsCurrent, w.IsDefault))
            .ToList();

        return ServiceResultFactory.Success<IReadOnlyList<WorkspaceListItemDto>>(dtos, InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Workspace.ListLoaded, ct));
    }

    public async Task<ServiceResult<object?>> DeleteAsync(
        long workspaceId, CancellationToken ct = default)
    {
        var rows = await workspaceRepository.DeleteAsync(workspaceId, userContext.UserId, ct);

        if (rows == -1)
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.Forbidden,
                await messageProvider.GetMessagesAsync(MessageKeys.Workspace.Forbidden, ct));

        if (rows == 0)
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Workspace.NotFound, ct));

        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Workspace.Deleted, ct));
    }

    public async Task<ServiceResult<object?>> SwitchCurrentAsync(
        long? workspaceId, CancellationToken ct = default)
    {
        var success = await workspaceRepository.SwitchCurrentAsync(userContext.UserId, workspaceId, ct);

        if (!success)
            return ServiceResultFactory.Failure<object?>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Workspace.SwitchFailed, ct));

        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Workspace.Switched, ct));
    }

    public async Task<ServiceResult<WorkspaceContextDto>> GetCurrentContextAsync(
        CancellationToken ct = default)
    {
        var db = await workspaceRepository.GetCurrentContextAsync(userContext.UserId, ct);

        var dto = db is null
            ? new WorkspaceContextDto(null, null, null, null, null, null, null, null, null, null)
            : new WorkspaceContextDto(
                db.CurrentWorkspaceId, db.DefaultWorkspaceId, db.WorkspaceName,
                db.WorkspaceTypeId, db.CurrencyCode, db.LogoFileName, db.Color,
                db.RoleId, db.RoleCode, db.RoleLevel);

        return ServiceResultFactory.Success(dto, InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Workspace.ContextLoaded, ct));
    }

    // ── Members ───────────────────────────────────────────────

    public async Task<ServiceResult<IReadOnlyList<WorkspaceMemberDto>>> GetMembersAsync(
        GetMembersRequest request, CancellationToken ct = default)
    {
        var items = await workspaceRepository.GetMembersAsync(
            request.WorkspaceId, userContext.UserId, request.StatusId, ct);

        var dtos = items.Select(MapMemberDto).ToList();

        return ServiceResultFactory.Success<IReadOnlyList<WorkspaceMemberDto>>(dtos, InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Workspace.MemberListLoaded, ct));
    }

    public async Task<ServiceResult<object?>> UpdateMemberRoleAsync(
        UpdateMemberRoleRequest request, CancellationToken ct = default)
    {
        var rows = await workspaceRepository.UpdateMemberRoleAsync(
            request.WorkspaceId, userContext.UserId, request.TargetUserId, request.NewRoleId, ct);

        return rows switch
        {
            -1 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.Forbidden,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.Forbidden, ct)),
            0  => ServiceResultFactory.Failure<object?>(InternalResponseCodes.NotFound,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.MemberNotFound, ct)),
            _  => ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.MemberRoleUpdated, ct)),
        };
    }

    public async Task<ServiceResult<object?>> SuspendMemberAsync(
        SuspendMemberRequest request, CancellationToken ct = default)
    {
        var rows = await workspaceRepository.SuspendMemberAsync(
            request.WorkspaceId, userContext.UserId, request.TargetUserId, ct);

        return rows switch
        {
            -1 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.Forbidden,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.Forbidden, ct)),
            0  => ServiceResultFactory.Failure<object?>(InternalResponseCodes.NotFound,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.MemberNotFound, ct)),
            _  => ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.MemberSuspended, ct)),
        };
    }

    public async Task<ServiceResult<object?>> ReinstateMemberAsync(
        ReinstateMemberRequest request, CancellationToken ct = default)
    {
        var rows = await workspaceRepository.ReinstateMemberAsync(
            request.WorkspaceId, userContext.UserId, request.TargetUserId, ct);

        return rows switch
        {
            -1 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.Forbidden,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.Forbidden, ct)),
            0  => ServiceResultFactory.Failure<object?>(InternalResponseCodes.NotFound,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.MemberNotFound, ct)),
            _  => ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.MemberReinstated, ct)),
        };
    }

    public async Task<ServiceResult<object?>> RemoveMemberAsync(
        RemoveMemberRequest request, CancellationToken ct = default)
    {
        var rows = await workspaceRepository.RemoveMemberAsync(
            request.WorkspaceId, userContext.UserId, request.TargetUserId, ct);

        return rows switch
        {
            -1 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.Forbidden,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.Forbidden, ct)),
            0  => ServiceResultFactory.Failure<object?>(InternalResponseCodes.NotFound,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.MemberNotFound, ct)),
            _  => ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.MemberRemoved, ct)),
        };
    }

    public async Task<ServiceResult<object?>> LeaveAsync(
        LeaveWorkspaceRequest request, CancellationToken ct = default)
    {
        var rows = await workspaceRepository.LeaveAsync(request.WorkspaceId, userContext.UserId, ct);

        return rows switch
        {
            -1 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.BadRequest,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.OwnerCannotLeave, ct)),
            0  => ServiceResultFactory.Failure<object?>(InternalResponseCodes.NotFound,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.NotFound, ct)),
            _  => ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.MemberLeft, ct)),
        };
    }

    // ── Invitations ───────────────────────────────────────────

    public async Task<ServiceResult<object?>> SendInvitationAsync(
        SendInvitationRequest request, CancellationToken ct = default)
    {
        // Generate a secure opaque token; tokenHasher.Hash stores the SHA-256 hash
        var rawToken  = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var tokenHash = tokenHasher.Hash(rawToken);
        var expiry    = DateTime.UtcNow.AddDays(InvitationExpiryDays);

        var model = new SendInvitationDbModel
        {
            WorkspaceId   = request.WorkspaceId,
            CallerUserId  = userContext.UserId,
            Email         = request.Email.Trim().ToLowerInvariant(),
            RoleId        = request.RoleId,
            TokenHash     = tokenHash,
            ExpiresAtUtc  = expiry,
        };

        var invitationId = await workspaceRepository.SendInvitationAsync(model, ct);

        return invitationId switch
        {
            -1 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.Forbidden,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.Forbidden, ct)),
            -2 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.BadRequest,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.CannotInviteOwnerRole, ct)),
            -3 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.Conflict,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.AlreadyMember, ct)),
            _  => await EnqueueInvitationEmailAndSucceedAsync(request, rawToken, expiry, ct),
        };
    }

    public async Task<ServiceResult<object?>> CancelInvitationAsync(
        CancelInvitationRequest request, CancellationToken ct = default)
    {
        var rows = await workspaceRepository.CancelInvitationAsync(
            request.InvitationId, request.WorkspaceId, userContext.UserId, ct);

        return rows switch
        {
            -1 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.Forbidden,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.Forbidden, ct)),
            0  => ServiceResultFactory.Failure<object?>(InternalResponseCodes.NotFound,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.InvitationNotFound, ct)),
            _  => ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.InvitationCancelled, ct)),
        };
    }

    public async Task<ServiceResult<InvitationPreviewDto>> GetInvitationByTokenAsync(
        string token, CancellationToken ct = default)
    {
        var tokenHash = tokenHasher.Hash(token);
        var db        = await workspaceRepository.GetInvitationByTokenAsync(tokenHash, ct);

        if (db is null)
            return ServiceResultFactory.Failure<InvitationPreviewDto>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Workspace.InvitationNotFound, ct));

        var dto = new InvitationPreviewDto(
            db.InvitationId, db.WorkspaceId, db.WorkspaceName, db.WorkspaceTypeId,
            db.Email, db.RoleId, db.RoleCode, db.StatusId,
            db.ExpiresAtUtc, db.CreatedAtUtc, db.InviterNameEn, db.InviterNameAr);

        return ServiceResultFactory.Success(dto, InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Common.Success, ct));
    }

    public async Task<ServiceResult<object?>> AcceptInvitationAsync(
        string token, CancellationToken ct = default)
    {
        var tokenHash = tokenHasher.Hash(token);
        var result    = await workspaceRepository.AcceptInvitationAsync(tokenHash, userContext.UserId, ct);

        return result switch
        {
            -1 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.NotFound,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.InvitationNotFound, ct)),
            -2 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.BadRequest,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.InvitationAlreadyUsed, ct)),
            -3 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.BadRequest,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.InvitationExpired, ct)),
            -4 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.Forbidden,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.EmailMismatch, ct)),
            _  => ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.InvitationAccepted, ct)),
        };
    }

    public async Task<ServiceResult<object?>> RejectInvitationAsync(
        string token, CancellationToken ct = default)
    {
        var tokenHash = tokenHasher.Hash(token);
        var result    = await workspaceRepository.RejectInvitationAsync(tokenHash, userContext.UserId, ct);

        return result switch
        {
            -1 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.NotFound,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.InvitationNotFound, ct)),
            -2 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.BadRequest,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.InvitationAlreadyUsed, ct)),
            -4 => ServiceResultFactory.Failure<object?>(InternalResponseCodes.Forbidden,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.EmailMismatch, ct)),
            _  => ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK,
                      await messageProvider.GetMessagesAsync(MessageKeys.Workspace.InvitationRejected, ct)),
        };
    }

    public async Task<ServiceResult<IReadOnlyList<InvitationListItemDto>>> GetInvitationsAsync(
        GetInvitationsRequest request, CancellationToken ct = default)
    {
        var items = await workspaceRepository.GetInvitationsAsync(
            request.WorkspaceId, userContext.UserId, request.StatusId, ct);

        var dtos = items.Select(i => new InvitationListItemDto(
            i.InvitationId, i.WorkspaceId, i.Email, i.RoleId, i.RoleCode,
            i.StatusId, i.ExpiresAtUtc, i.CreatedAtUtc,
            i.AcceptedAtUtc, i.RejectedAtUtc, i.InviterNameEn, i.InviterNameAr)).ToList();

        return ServiceResultFactory.Success<IReadOnlyList<InvitationListItemDto>>(dtos, InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Workspace.InvitationListLoaded, ct));
    }

    // ── Permissions ───────────────────────────────────────────

    public async Task<ServiceResult<IReadOnlyList<WorkspacePermissionDto>>> GetMyPermissionsAsync(
        long workspaceId, CancellationToken ct = default)
    {
        var items = await workspaceRepository.GetMyPermissionsAsync(workspaceId, userContext.UserId, ct);

        var dtos = items.Select(p => new WorkspacePermissionDto(p.Code, p.Resource, p.Action)).ToList();

        return ServiceResultFactory.Success<IReadOnlyList<WorkspacePermissionDto>>(dtos, InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Workspace.PermissionsLoaded, ct));
    }

    // ── Activity ──────────────────────────────────────────────

    public async Task<ServiceResult<WorkspaceActivityResponse>> GetActivityAsync(
        GetActivityRequest request, CancellationToken ct = default)
    {
        var (total, items) = await workspaceRepository.GetActivityAsync(
            request.WorkspaceId, userContext.UserId, request.PageNumber, request.PageSize, ct);

        var dtos = items.Select(a => new WorkspaceActivityDto(
            a.ActivityId, a.WorkspaceId, a.ActorUserId, a.Action,
            a.EntityType, a.EntityId, a.MetadataJson, a.CreatedAtUtc,
            a.ActorNameEn, a.ActorNameAr, _profilePictureUrl(a.ActorProfilePicture))).ToList();

        var response = new WorkspaceActivityResponse(total, request.PageNumber, request.PageSize, dtos);

        return ServiceResultFactory.Success(response, InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Workspace.ActivityLoaded, ct));
    }

    // ── Helpers ───────────────────────────────────────────────

    private async Task<ServiceResult<object?>> EnqueueInvitationEmailAndSucceedAsync(
        SendInvitationRequest request, string rawToken, DateTime expiry, CancellationToken ct)
    {
        // Populate the email payload with real data so the invitation renders completely
        // (workspace name, who invited you, the granted role) — matching the other emails.
        var workspace   = await workspaceRepository.GetByIdAsync(request.WorkspaceId, userContext.UserId, ct);
        var inviterName = userContext.DisplayName;
        var roleCode    = Enum.IsDefined(typeof(WorkspaceRoleId), request.RoleId)
            ? ((WorkspaceRoleId)request.RoleId).ToString()
            : request.RoleId.ToString();
        var language    = userContext.Language == SystemLanguages.Arabic ? "ar" : "en";

        // Build the accept link the same way AuthService builds ResetLink/confirmationLink.
        var acceptLink = $"{authOptions.Value.AcceptInvitationBaseUrl}?token={Uri.EscapeDataString(rawToken)}";

        var payload = new WorkspaceInvitationEmailPayload(
            ToEmail:       request.Email,
            InviterNameEn: inviterName,
            InviterNameAr: inviterName,
            WorkspaceName: workspace?.Name ?? string.Empty,
            RoleCode:      roleCode,
            AcceptToken:   rawToken,
            RejectToken:   rawToken,
            AcceptLink:    acceptLink,
            ExpiresAtUtc:  expiry,
            Language:      language);

        await backgroundJobService.EnqueueAsync(
            JobTypes.WorkspaceInvitationEmail,
            payload,
            priority: (byte)2,
            ct: ct);

        return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Workspace.InvitationSent, ct));
    }

    private static WorkspaceDto MapWorkspaceDto(WorkspaceDbResult db) =>
        new(db.WorkspaceId, db.OwnerUserId, db.Name, db.Description, db.TypeId,
            db.CurrencyCode, db.Timezone, db.LogoFileName, db.Color,
            db.IsActive, db.CreatedAtUtc, db.UpdatedAtUtc,
            db.CallerRoleId, db.CallerRoleCode, db.ActiveMemberCount);

    private WorkspaceMemberDto MapMemberDto(WorkspaceMemberDbResult m) =>
        new(m.MemberId, m.WorkspaceId, m.UserId, m.RoleId, m.StatusId,
            m.InvitedByUserId, m.JoinedAtUtc, m.CreatedAtUtc,
            m.RoleCode, m.RoleNameEn, m.RoleNameAr,
            m.DisplayNameEn, m.DisplayNameAr, _profilePictureUrl(m.ProfilePicture), m.Email);

    /// <summary>
    /// Builds the full, publicly-servable URL for a stored profile-picture file
    /// name. The backend owns URL construction so the frontend only displays it.
    /// </summary>
    private string? _profilePictureUrl(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var (url, _) = storageUtility.BuildFilePathWithExpiration(
            FolderPaths.ProfilePictures, fileName, isInternalStorage: true, baseUrl: userContext.RequestBaseUrl);
        return url;
    }
}
