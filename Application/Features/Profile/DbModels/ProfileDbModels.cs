namespace Application.Features.Profile.DbModels;

public sealed record GetProfileDbResult(
    long     PersonId,
    string   FirstNameEn,
    string   LastNameEn,
    string?  FirstNameAr,
    string?  LastNameAr,
    string   DisplayNameEn,
    string?  DisplayNameAr,
    string   Email,
    DateTime? DateOfBirth,
    byte?    GenderId,
    string?  ProfilePicture,
    bool     IsEmailConfirmed,
    string?  PendingEmail
);

public sealed record UpdateProfileDbInput
{
    public long    UserId        { get; init; }
    public string  FirstNameEn   { get; init; } = default!;
    public string  LastNameEn    { get; init; } = default!;
    public string? FirstNameAr   { get; init; }
    public string? LastNameAr    { get; init; }
    public string  DisplayNameEn { get; init; } = default!;
    public string? DisplayNameAr { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public byte?   GenderId      { get; init; }
}

public sealed record UpdateProfileDbResult(byte ResultCode);

public sealed record UpdateProfilePictureDbInput
{
    public long   UserId         { get; init; }
    public string ProfilePicture { get; init; } = default!;
}

public sealed record UpdateProfilePictureDbResult(
    byte    ResultCode,
    string? OldProfilePicture
);

public sealed record RemoveProfilePictureDbResult(
    byte    ResultCode,
    string? OldProfilePicture
);
