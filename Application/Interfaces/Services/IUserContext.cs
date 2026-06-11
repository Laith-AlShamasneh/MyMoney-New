using Shared.Enums.System;

namespace Application.Interfaces.Services;

public interface IUserContext
{
    long UserId { get; }
    string Email { get; }
    string DisplayName { get; }
    bool IsAuthenticated { get; }
    SystemRoles RoleId { get; }
    SystemLanguages Language { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    string? SessionId { get; }
    string? TraceId { get; }
}
