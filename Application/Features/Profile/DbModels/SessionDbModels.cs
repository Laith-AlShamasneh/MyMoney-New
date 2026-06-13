namespace Application.Features.Profile.DbModels;

public sealed record GetSessionsDbResult(
    long     Id,
    string   CreatedByIp,
    DateTime CreatedOnUtc,
    DateTime ExpiresOnUtc,
    bool     IsCurrentSession
);

public sealed record RevokeSessionDbInput
{
    public long   UserId       { get; init; }
    public long   TokenId      { get; init; }
    public string RevokedByIp  { get; init; } = default!;
}

public sealed record RevokeSessionDbResult(byte ResultCode);

public sealed record RevokeAllOtherSessionsDbInput
{
    public long   UserId            { get; init; }
    public string CurrentTokenHash  { get; init; } = default!;
    public string RevokedByIp       { get; init; } = default!;
}
