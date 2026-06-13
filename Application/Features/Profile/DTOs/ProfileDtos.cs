using Microsoft.AspNetCore.Http;

namespace Application.Features.Profile.DTOs;

public sealed record GetProfileResponse(
    string   FirstNameEn,
    string   LastNameEn,
    string?  FirstNameAr,
    string?  LastNameAr,
    string   DisplayNameEn,
    string?  DisplayNameAr,
    string   Email,
    string?  DateOfBirth,
    int?     GenderId,
    string?  ProfileImageUrl,
    bool     IsEmailConfirmed,
    bool     HasPendingEmailChange,
    string?  PendingEmail
);

public sealed record UpdateProfileRequest(
    string   FirstNameEn,
    string   LastNameEn,
    string?  FirstNameAr,
    string?  LastNameAr,
    string   DisplayNameEn,
    string?  DisplayNameAr,
    DateOnly? DateOfBirth,
    int?     GenderId
);

public sealed record UpdateProfileResponse(
    string   DisplayNameEn,
    string?  DisplayNameAr,
    string?  ProfileImageUrl
);

public sealed record UpdateProfilePictureRequest(IFormFile ProfileImage);
