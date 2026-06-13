namespace Application.Features.Profile.DTOs;

public sealed record SessionItem(
    long     Id,
    string   IpAddress,
    DateTime CreatedOnUtc,
    DateTime ExpiresOnUtc,
    bool     IsCurrentSession
);

public sealed record RevokeSessionRequest(long SessionId);

public sealed record RevokeAllOtherSessionsRequest(string CurrentRefreshToken);
