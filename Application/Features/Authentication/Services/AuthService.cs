using Application.Features.Authentication.DbModels;
using Application.Features.Authentication.DTOs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.Authentication.Services;

internal sealed class AuthService(
    IAuthRepository  authRepository,
    IPasswordHasher  passwordHasher,
    IJwtService      jwtService,
    ITokenHasher     tokenHasher,
    IFileService     fileService,
    IStorageUtility  storageUtility,
    IUserContext     userContext,
    IMessageProvider messageProvider) : IAuthService
{
    private const int RefreshTokenExpiryDays = 7;

    public async Task<ServiceResult<RegisterResponse>> RegisterAsync(
        RegisterRequest request, CancellationToken ct = default)
    {
        // 1. Fast email duplicate check before any expensive work
        var emailExists = await authRepository.CheckEmailExistsAsync(request.Email, ct);
        if (emailExists)
        {
            var failMsg    = await messageProvider.GetMessagesAsync(MessageKeys.Authentication.RegistrationFailed, ct);
            var detailMsg  = await messageProvider.GetMessagesAsync(MessageKeys.Authentication.EmailAlreadyInUse, ct);
            return ServiceResultFactory.Failure<RegisterResponse>(InternalResponseCodes.Conflict, failMsg, [detailMsg]);
        }

        // 2. Upload profile image if provided
        string? profilePictureFileName = null;
        if (request.ProfileImage is not null)
        {
            var ext = Path.GetExtension(request.ProfileImage.FileName);
            profilePictureFileName = $"{Guid.NewGuid()}{ext}";
            await using var stream = request.ProfileImage.OpenReadStream();
            await fileService.UploadAsync(stream, $"profiles/{profilePictureFileName}", request.ProfileImage.ContentType, ct);
        }

        // 3. Persist Person + User + UserRole atomically
        var dbInput = new RegisterDbInput
        {
            FirstNameEn    = request.FirstNameEn,
            LastNameEn     = request.LastNameEn,
            FirstNameAr    = request.FirstNameAr,
            LastNameAr     = request.LastNameAr,
            DisplayNameEn  = request.DisplayNameEn,
            DisplayNameAr  = request.DisplayNameAr,
            DateOfBirth    = request.DateOfBirth,
            GenderId       = request.GenderId,
            ProfilePicture = profilePictureFileName,
            Email          = request.Email,
            PasswordHash   = passwordHasher.Hash(request.Password),
            DefaultRoleId  = (int)SystemRoles.User
        };

        var dbResult = await authRepository.RegisterAsync(dbInput, ct);

        if (dbResult is null)
        {
            // Race-condition duplicate — clean up uploaded file
            if (profilePictureFileName is not null)
                await fileService.DeleteAsync($"profiles/{profilePictureFileName}", ct);

            var failMsg   = await messageProvider.GetMessagesAsync(MessageKeys.Authentication.RegistrationFailed, ct);
            var detailMsg = await messageProvider.GetMessagesAsync(MessageKeys.Authentication.EmailAlreadyInUse, ct);
            return ServiceResultFactory.Failure<RegisterResponse>(InternalResponseCodes.Conflict, failMsg, [detailMsg]);
        }

        // 4. Generate tokens
        var jwtModel = new JwtTokenResponse(
            dbResult.UserId, dbResult.Email, dbResult.DisplayNameEn, [(int)SystemRoles.User]);

        var (accessToken, accessTokenExpiresAt) = jwtService.GenerateAccessToken(jwtModel);

        var rawRefreshToken       = tokenHasher.GenerateRawToken();
        var hashedRefreshToken    = tokenHasher.Hash(rawRefreshToken);
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays);

        await authRepository.SaveRefreshTokenAsync(new SaveRefreshTokenDbInput
        {
            UserId       = dbResult.UserId,
            Token        = hashedRefreshToken,
            ExpiresOnUtc = refreshTokenExpiresAt,
            CreatedByIp  = userContext.IpAddress
        }, ct);

        // 5. Build profile image URL
        string? profileImageUrl = null;
        if (profilePictureFileName is not null)
        {
            var (url, _) = storageUtility.BuildFilePathWithExpiration(
                FolderPaths.ProfilePictures,
                profilePictureFileName,
                isInternalStorage: true,
                baseUrl: userContext.RequestBaseUrl);
            profileImageUrl = url;
        }

        // 6. Localize display name and role name
        var isArabic    = userContext.Language == SystemLanguages.Arabic;
        var displayName = isArabic && !string.IsNullOrWhiteSpace(dbResult.DisplayNameAr)
            ? dbResult.DisplayNameAr
            : dbResult.DisplayNameEn;
        var roleName = isArabic ? dbResult.RoleNameAr : dbResult.RoleNameEn;

        var successMsg = await messageProvider.GetMessagesAsync(MessageKeys.Authentication.UserRegisteredSuccess, ct);
        var response   = new RegisterResponse(
            Email:                 dbResult.Email,
            DisplayName:           displayName,
            ProfileImageUrl:       profileImageUrl,
            Roles:                 [roleName],
            AccessToken:           accessToken,
            RefreshToken:          rawRefreshToken,
            AccessTokenExpiresAt:  accessTokenExpiresAt,
            RefreshTokenExpiresAt: refreshTokenExpiresAt);

        return ServiceResultFactory.Success(response, InternalResponseCodes.Created, successMsg);
    }
}
