namespace Application.Features.Workspace.DbModels;

// ── Workspace CRUD ────────────────────────────────────────────

public sealed class CreateWorkspaceDbModel
{
    public long    OwnerUserId  { get; init; }
    public string  Name         { get; init; } = default!;
    public string? Description  { get; init; }
    public byte    TypeId       { get; init; }
    public string? CurrencyCode { get; init; }
    public string? Timezone     { get; init; }
    public string? Color        { get; init; }
}

public sealed class UpdateWorkspaceDbModel
{
    public long    WorkspaceId  { get; init; }
    public long    CallerUserId { get; init; }
    public string  Name         { get; init; } = default!;
    public string? Description  { get; init; }
    public string? CurrencyCode { get; init; }
    public string? Timezone     { get; init; }
    public string? Color        { get; init; }
}

public sealed class WorkspaceDbResult
{
    public long    WorkspaceId       { get; init; }
    public long    OwnerUserId       { get; init; }
    public string  Name              { get; init; } = default!;
    public string? Description       { get; init; }
    public byte    TypeId            { get; init; }
    public string? CurrencyCode      { get; init; }
    public string? Timezone          { get; init; }
    public string? LogoFileName      { get; init; }
    public string? Color             { get; init; }
    public bool    IsActive          { get; init; }
    public DateTime CreatedAtUtc     { get; init; }
    public DateTime? UpdatedAtUtc    { get; init; }
    public byte?   CallerRoleId      { get; init; }
    public byte?   CallerStatusId    { get; init; }
    public string? CallerRoleCode    { get; init; }
    public int     ActiveMemberCount { get; init; }
}

public sealed class WorkspaceListItemDbResult
{
    public long    WorkspaceId       { get; init; }
    public long    OwnerUserId       { get; init; }
    public string  Name              { get; init; } = default!;
    public byte    TypeId            { get; init; }
    public string? CurrencyCode      { get; init; }
    public string? LogoFileName      { get; init; }
    public string? Color             { get; init; }
    public DateTime CreatedAtUtc     { get; init; }
    public byte    RoleId            { get; init; }
    public string  RoleCode          { get; init; } = default!;
    public int     ActiveMemberCount { get; init; }
    public bool    IsCurrent         { get; init; }
    public bool    IsDefault         { get; init; }
}

public sealed class AffectedRowsDbResult
{
    public int AffectedRows { get; init; }
}

// ── Members ───────────────────────────────────────────────────

public sealed class WorkspaceMemberDbResult
{
    public long    MemberId          { get; init; }
    public long    WorkspaceId       { get; init; }
    public long    UserId            { get; init; }
    public byte    RoleId            { get; init; }
    public byte    StatusId          { get; init; }
    public long?   InvitedByUserId   { get; init; }
    public DateTime? JoinedAtUtc     { get; init; }
    public DateTime  CreatedAtUtc    { get; init; }
    public string  RoleCode          { get; init; } = default!;
    public string  RoleNameEn        { get; init; } = default!;
    public string  RoleNameAr        { get; init; } = default!;
    public string  DisplayNameEn     { get; init; } = default!;
    public string? DisplayNameAr     { get; init; }
    public string? ProfilePicture    { get; init; }
    public string  Email             { get; init; } = default!;
}

public sealed class MyRoleDbResult
{
    public long   MemberId   { get; init; }
    public byte   RoleId     { get; init; }
    public byte   StatusId   { get; init; }
    public string RoleCode   { get; init; } = default!;
    public byte   RoleLevel  { get; init; }
}

// ── Invitations ───────────────────────────────────────────────

public sealed class SendInvitationDbModel
{
    public long    WorkspaceId      { get; init; }
    public long    CallerUserId     { get; init; }
    public string  Email            { get; init; } = default!;
    public byte    RoleId           { get; init; }
    public string  TokenHash        { get; init; } = default!;
    public DateTime ExpiresAtUtc    { get; init; }
}

public sealed class InvitationByTokenDbResult
{
    public long    InvitationId     { get; init; }
    public long    WorkspaceId      { get; init; }
    public long    InvitedByUserId  { get; init; }
    public string  Email            { get; init; } = default!;
    public byte    RoleId           { get; init; }
    public byte    StatusId         { get; init; }
    public DateTime ExpiresAtUtc    { get; init; }
    public DateTime CreatedAtUtc    { get; init; }
    public string  RoleCode         { get; init; } = default!;
    public string  WorkspaceName    { get; init; } = default!;
    public byte    WorkspaceTypeId  { get; init; }
    public string  InviterNameEn    { get; init; } = default!;
    public string? InviterNameAr    { get; init; }
}

public sealed class InvitationListItemDbResult
{
    public long    InvitationId     { get; init; }
    public long    WorkspaceId      { get; init; }
    public string  Email            { get; init; } = default!;
    public byte    RoleId           { get; init; }
    public byte    StatusId         { get; init; }
    public DateTime ExpiresAtUtc    { get; init; }
    public DateTime CreatedAtUtc    { get; init; }
    public DateTime? AcceptedAtUtc  { get; init; }
    public DateTime? RejectedAtUtc  { get; init; }
    public string  RoleCode         { get; init; } = default!;
    public string  InviterNameEn    { get; init; } = default!;
    public string? InviterNameAr    { get; init; }
}

// ── Permissions ───────────────────────────────────────────────

public sealed class WorkspacePermissionDbResult
{
    public string Code     { get; init; } = default!;
    public string Resource { get; init; } = default!;
    public string Action   { get; init; } = default!;
}

// ── Activity ──────────────────────────────────────────────────

public sealed class WorkspaceActivityDbResult
{
    public long    ActivityId        { get; init; }
    public long    WorkspaceId       { get; init; }
    public long    ActorUserId       { get; init; }
    public string  Action            { get; init; } = default!;
    public string? EntityType        { get; init; }
    public long?   EntityId          { get; init; }
    public string? MetadataJson      { get; init; }
    public DateTime CreatedAtUtc     { get; init; }
    public string  ActorNameEn       { get; init; } = default!;
    public string? ActorNameAr       { get; init; }
    public string? ActorProfilePicture { get; init; }
}

public sealed class ActivityTotalCountDbResult
{
    public int TotalCount { get; init; }
}

// ── Context ───────────────────────────────────────────────────

public sealed class WorkspaceContextDbResult
{
    public long?   CurrentWorkspaceId  { get; init; }
    public long?   DefaultWorkspaceId  { get; init; }
    public string? WorkspaceName       { get; init; }
    public byte?   WorkspaceTypeId     { get; init; }
    public string? CurrencyCode        { get; init; }
    public string? LogoFileName        { get; init; }
    public string? Color               { get; init; }
    public byte?   RoleId              { get; init; }
    public string? RoleCode            { get; init; }
    public byte?   RoleLevel           { get; init; }
}
