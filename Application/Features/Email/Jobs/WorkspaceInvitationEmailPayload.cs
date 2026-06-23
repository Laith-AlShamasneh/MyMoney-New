namespace Application.Features.Email.Jobs;

public sealed record WorkspaceInvitationEmailPayload(
    string  ToEmail,
    string  InviterNameEn,
    string  InviterNameAr,
    string  WorkspaceName,
    string  RoleCode,
    string  AcceptToken,
    string  RejectToken,        // same token — front-end path determines action
    DateTime ExpiresAtUtc,
    string  Language            // "en" | "ar"
);
