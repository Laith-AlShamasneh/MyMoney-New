using Application.Common.Constants;
using Application.Common.Options;
using Application.Features.Authentication.DbModels;
using Application.Features.Authentication.DTOs;
using Application.Features.Email.Jobs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Microsoft.Extensions.Options;
using Shared.Constants;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.Authentication.Services;

internal sealed class AuthService(
    IAuthRepository              authRepository,
    IPasswordHasher              passwordHasher,
    IJwtService                  jwtService,
    ITokenHasher                 tokenHasher,
    IFileService                 fileService,
    IStorageUtility              storageUtility,
    IUserContext                 userContext,
    IMessageProvider             messageProvider,
    IBackgroundJobService        backgroundJobService,
    IOptions<AuthenticationOptions> authOptions) : IAuthService
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
            var fileKey = storageUtility.BuildFileKey(FolderPaths.ProfilePictures, profilePictureFileName);
            await using var stream = request.ProfileImage.OpenReadStream();
            await fileService.UploadAsync(stream, fileKey, request.ProfileImage.ContentType, ct);
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
                await fileService.DeleteAsync(storageUtility.BuildFileKey(FolderPaths.ProfilePictures, profilePictureFileName), ct);

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

        // 5. Localize display name and role name
        var isArabic    = userContext.Language == SystemLanguages.Arabic;
        var displayName = isArabic && !string.IsNullOrWhiteSpace(dbResult.DisplayNameAr)
            ? dbResult.DisplayNameAr
            : dbResult.DisplayNameEn;
        var roleName = isArabic ? dbResult.RoleNameAr : dbResult.RoleNameEn;

        // 6. Build profile image URL
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

        // 7. Enqueue welcome email — fire-and-forget via durable background job (Rule 16)
        await backgroundJobService.EnqueueAsync(
            jobType: JobTypes.WelcomeEmail,
            payload: new WelcomeEmailPayload(dbResult.Email, displayName, userContext.Language),
            ct: ct);

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

    public async Task<ServiceResult<LoginResponse>> LoginAsync(
        LoginRequest      request,
        CancellationToken ct = default)
    {
        // 1. Load user record — NULL means email does not exist
        var user = await authRepository.GetByEmailForLoginAsync(request.Email, ct);

        // Always return generic failure — never reveal whether the email exists (Rule: no enumeration)
        if (user is null)
        {
            return ServiceResultFactory.Failure<LoginResponse>(
                InternalResponseCodes.Unauthorized,
                await messageProvider.GetMessagesAsync(MessageKeys.Authentication.InvalidCredentials, ct));
        }

        // 2. Inactive account — block before any further processing
        if (!user.IsActive)
        {
            return ServiceResultFactory.Failure<LoginResponse>(
                InternalResponseCodes.Unauthorized,
                await messageProvider.GetMessagesAsync(MessageKeys.Authentication.AccountNotActive, ct));
        }

        // 3. Lockout check — respect temporal locks; expired locks are cleared on the next successful login
        var isCurrentlyLocked = user.IsLocked &&
            (user.LockoutEndDateUtc is null || user.LockoutEndDateUtc > DateTime.UtcNow);

        if (isCurrentlyLocked)
        {
            return ServiceResultFactory.Failure<LoginResponse>(
                InternalResponseCodes.Unauthorized,
                await messageProvider.GetMessagesAsync(MessageKeys.Authentication.AccountLocked, ct));
        }

        // 4. Password verification — wrong password increments the failed-attempt counter atomically
        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            var options = authOptions.Value;
            await authRepository.UpdateLoginAsync(new LoginUpdateDbModel
            {
                UserId                 = user.UserId,
                LoginSucceeded         = false,
                MaxFailedAttempts      = options.MaxFailedLoginAttempts,
                LockoutDurationMinutes = options.LockoutDurationMinutes
            }, ct);

            return ServiceResultFactory.Failure<LoginResponse>(
                InternalResponseCodes.Unauthorized,
                await messageProvider.GetMessagesAsync(MessageKeys.Authentication.InvalidCredentials, ct));
        }

        // 5. Email confirmation — password was correct so do not penalize the attempt counter
        if (!user.IsEmailConfirmed)
        {
            return ServiceResultFactory.Failure<LoginResponse>(
                InternalResponseCodes.Unauthorized,
                await messageProvider.GetMessagesAsync(MessageKeys.Authentication.EmailNotConfirmed, ct));
        }

        // 6. Mark login success — resets failed-attempt counter and updates LastLoginDateUtc atomically
        await authRepository.UpdateLoginAsync(new LoginUpdateDbModel
        {
            UserId                 = user.UserId,
            LoginSucceeded         = true,
            MaxFailedAttempts      = authOptions.Value.MaxFailedLoginAttempts,
            LockoutDurationMinutes = authOptions.Value.LockoutDurationMinutes
        }, ct);

        // 7. Generate access token
        var jwtModel = new JwtTokenResponse(
            user.UserId, user.Email, user.DisplayNameEn, [user.RoleId]);

        var (accessToken, accessTokenExpiresAt) = jwtService.GenerateAccessToken(jwtModel);

        // 8. Generate and persist refresh token (store only the SHA-256 hash — Rule 17)
        var rawRefreshToken       = tokenHasher.GenerateRawToken();
        var hashedRefreshToken    = tokenHasher.Hash(rawRefreshToken);
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays);

        await authRepository.SaveRefreshTokenAsync(new SaveRefreshTokenDbInput
        {
            UserId       = user.UserId,
            Token        = hashedRefreshToken,
            ExpiresOnUtc = refreshTokenExpiresAt,
            CreatedByIp  = userContext.IpAddress
        }, ct);

        // 9. Resolve localized display name and role name
        var isArabic    = userContext.Language == SystemLanguages.Arabic;
        var displayName = isArabic && !string.IsNullOrWhiteSpace(user.DisplayNameAr)
            ? user.DisplayNameAr
            : user.DisplayNameEn;
        var roleName = isArabic ? user.RoleNameAr : user.RoleNameEn;

        // 10. Build profile image URL if present
        string? profileImageUrl = null;
        if (!string.IsNullOrEmpty(user.ProfilePicture))
        {
            var (url, _) = storageUtility.BuildFilePathWithExpiration(
                FolderPaths.ProfilePictures,
                user.ProfilePicture,
                isInternalStorage: true,
                baseUrl: userContext.RequestBaseUrl);
            profileImageUrl = url;
        }

        var loginResponse = new LoginResponse(
            Email:                 user.Email,
            DisplayName:           displayName,
            ProfileImageUrl:       profileImageUrl,
            Roles:                 [roleName],
            AccessToken:           accessToken,
            RefreshToken:          rawRefreshToken,
            AccessTokenExpiresAt:  accessTokenExpiresAt,
            RefreshTokenExpiresAt: refreshTokenExpiresAt);

        return ServiceResultFactory.Success(
            loginResponse,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Authentication.UserLoginSuccess, ct));
    }
}
