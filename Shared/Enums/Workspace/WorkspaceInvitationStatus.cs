namespace Shared.Enums.Workspace;

public enum WorkspaceInvitationStatus : byte
{
    Pending   = 1,
    Accepted  = 2,
    Rejected  = 3,
    Expired   = 4,
    Cancelled = 5,
}
