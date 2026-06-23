namespace Application.Features.Workspace.DTOs;

// ── Requests ──────────────────────────────────────────────────

public sealed record CreateWorkspaceRequest(
    string  Name,
    string? Description,
    byte    TypeId,
    string? CurrencyCode,
    string? Timezone,
    string? Color);

public sealed record UpdateWorkspaceRequest(
    long    WorkspaceId,
    string  Name,
    string? Description,
    string? CurrencyCode,
    string? Timezone,
    string? Color);

public sealed record GetWorkspaceRequest(long WorkspaceId);

public sealed record DeleteWorkspaceRequest(long WorkspaceId);

public sealed record SwitchWorkspaceRequest(long? WorkspaceId); // null = personal mode

public sealed record GetMembersRequest(
    long   WorkspaceId,
    byte?  StatusId);

public sealed record UpdateMemberRoleRequest(
    long  WorkspaceId,
    long  TargetUserId,
    byte  NewRoleId);

public sealed record SuspendMemberRequest(
    long WorkspaceId,
    long TargetUserId);

public sealed record ReinstateMemberRequest(
    long WorkspaceId,
    long TargetUserId);

public sealed record RemoveMemberRequest(
    long WorkspaceId,
    long TargetUserId);

public sealed record LeaveWorkspaceRequest(long WorkspaceId);

public sealed record SendInvitationRequest(
    long   WorkspaceId,
    string Email,
    byte   RoleId);

public sealed record CancelInvitationRequest(
    long InvitationId,
    long WorkspaceId);

public sealed record AcceptInvitationRequest(string Token);

public sealed record RejectInvitationRequest(string Token);

public sealed record GetInvitationsByTokenRequest(string Token);

public sealed record GetInvitationsRequest(
    long  WorkspaceId,
    byte? StatusId);

public sealed record GetPermissionsRequest(long WorkspaceId);

public sealed record GetActivityRequest(
    long WorkspaceId,
    int  PageNumber,
    int  PageSize);

// ── Responses ─────────────────────────────────────────────────

public sealed record CreateWorkspaceResponse(long WorkspaceId);

public sealed record WorkspaceDto(
    long     WorkspaceId,
    long     OwnerUserId,
    string   Name,
    string?  Description,
    byte     TypeId,
    string?  CurrencyCode,
    string?  Timezone,
    string?  LogoFileName,
    string?  Color,
    bool     IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte?    CallerRoleId,
    string?  CallerRoleCode,
    int      ActiveMemberCount);

public sealed record WorkspaceListItemDto(
    long     WorkspaceId,
    long     OwnerUserId,
    string   Name,
    byte     TypeId,
    string?  CurrencyCode,
    string?  LogoFileName,
    string?  Color,
    DateTime CreatedAtUtc,
    byte     RoleId,
    string   RoleCode,
    int      ActiveMemberCount,
    bool     IsCurrent,
    bool     IsDefault);

public sealed record WorkspaceMemberDto(
    long     MemberId,
    long     WorkspaceId,
    long     UserId,
    byte     RoleId,
    byte     StatusId,
    long?    InvitedByUserId,
    DateTime? JoinedAtUtc,
    DateTime  CreatedAtUtc,
    string   RoleCode,
    string   RoleNameEn,
    string   RoleNameAr,
    string   DisplayNameEn,
    string?  DisplayNameAr,
    string?  ProfilePicture,
    string   Email);

public sealed record InvitationPreviewDto(
    long     InvitationId,
    long     WorkspaceId,
    string   WorkspaceName,
    byte     WorkspaceTypeId,
    string   Email,
    byte     RoleId,
    string   RoleCode,
    byte     StatusId,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc,
    string   InviterNameEn,
    string?  InviterNameAr);

public sealed record InvitationListItemDto(
    long     InvitationId,
    long     WorkspaceId,
    string   Email,
    byte     RoleId,
    string   RoleCode,
    byte     StatusId,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? RejectedAtUtc,
    string   InviterNameEn,
    string?  InviterNameAr);

public sealed record WorkspacePermissionDto(
    string Code,
    string Resource,
    string Action);

public sealed record WorkspaceActivityDto(
    long     ActivityId,
    long     WorkspaceId,
    long     ActorUserId,
    string   Action,
    string?  EntityType,
    long?    EntityId,
    string?  MetadataJson,
    DateTime CreatedAtUtc,
    string   ActorNameEn,
    string?  ActorNameAr,
    string?  ActorProfilePicture);

public sealed record WorkspaceActivityResponse(
    int                           TotalCount,
    int                           PageNumber,
    int                           PageSize,
    IReadOnlyList<WorkspaceActivityDto> Items);

public sealed record WorkspaceContextDto(
    long?   CurrentWorkspaceId,
    long?   DefaultWorkspaceId,
    string? WorkspaceName,
    byte?   WorkspaceTypeId,
    string? CurrencyCode,
    string? LogoFileName,
    string? Color,
    byte?   RoleId,
    string? RoleCode,
    byte?   RoleLevel);
