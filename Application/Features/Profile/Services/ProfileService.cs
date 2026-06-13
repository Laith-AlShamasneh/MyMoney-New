using Application.Common.Constants;
using Application.Common.Options;
using Application.Features.Email.Jobs;
using Application.Features.Profile.DbModels;
using Application.Features.Profile.DTOs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Microsoft.Extensions.Options;
using Shared.Constants;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.Profile.Services;

internal sealed class ProfileService(
    IProfileRepository       profileRepository,
    IPasswordHasher          passwordHasher,
    ITokenHasher             tokenHasher,
    IFileService             fileService,
    IStorageUtility          storageUtility,
    IUserContext             userContext,
    IMessageProvider         messageProvider,
    IBackgroundJobService    backgroundJobService,
    IOptions<AuthenticationOptions> authOptions) : IProfileService
{
    private bool IsArabic => userContext.Language == SystemLanguages.Arabic;

    private string LocalizedDisplayName(string en, string? ar) =>
        IsArabic && !string.IsNullOrWhiteSpace(ar) ? ar : en;

    // ─────────────────────────────────────────────────────────────────────────
    // GET PROFILE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<GetProfileResponse>> GetProfileAsync(CancellationToken ct = default)
    {
        var profile = await profileRepository.GetProfileAsync(userContext.UserId, ct);

        if (profile is null)
            return ServiceResultFactory.Failure<GetProfileResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Profile.NotFound, ct));

        string? profileImageUrl = null;
        if (!string.IsNullOrEmpty(profile.ProfilePicture))
        {
            var (url, _) = storageUtility.BuildFilePathWithExpiration(
                FolderPaths.ProfilePictures,
                profile.ProfilePicture,
                isInternalStorage: true,
                baseUrl: userContext.RequestBaseUrl);
            profileImageUrl = url;
        }

        var response = new GetProfileResponse(
            FirstNameEn:           profile.FirstNameEn,
            LastNameEn:            profile.LastNameEn,
            FirstNameAr:           profile.FirstNameAr,
            LastNameAr:            profile.LastNameAr,
            DisplayNameEn:         profile.DisplayNameEn,
            DisplayNameAr:         profile.DisplayNameAr,
            Email:                 profile.Email,
            DateOfBirth:           profile.DateOfBirth?.ToString("yyyy-MM-dd"),
            GenderId:              profile.GenderId,
            ProfileImageUrl:       profileImageUrl,
            IsEmailConfirmed:      profile.IsEmailConfirmed,
            HasPendingEmailChange: profile.PendingEmail is not null,
            PendingEmail:          profile.PendingEmail);

        return ServiceResultFactory.Success(
            response,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Profile.GetProfileSuccess, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UPDATE PROFILE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<UpdateProfileResponse>> UpdateProfileAsync(
        UpdateProfileRequest request, CancellationToken ct = default)
    {
        var input = new UpdateProfileDbInput
        {
            UserId        = userContext.UserId,
            FirstNameEn   = request.FirstNameEn.Trim(),
            LastNameEn    = request.LastNameEn.Trim(),
            FirstNameAr   = NullIfEmpty(request.FirstNameAr),
            LastNameAr    = NullIfEmpty(request.LastNameAr),
            DisplayNameEn = request.DisplayNameEn.Trim(),
            DisplayNameAr = NullIfEmpty(request.DisplayNameAr),
            DateOfBirth   = request.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
            GenderId      = request.GenderId.HasValue ? (byte)request.GenderId.Value : (byte?)null
        };

        var dbResult = await profileRepository.UpdateProfileAsync(input, ct);

        if (dbResult.ResultCode != 0)
            return ServiceResultFactory.Failure<UpdateProfileResponse>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Profile.NotFound, ct));

        var profile = await profileRepository.GetProfileAsync(userContext.UserId, ct);

        string? profileImageUrl = null;
        if (!string.IsNullOrEmpty(profile?.ProfilePicture))
        {
            var (url, _) = storageUtility.BuildFilePathWithExpiration(
                FolderPaths.ProfilePictures,
                profile.ProfilePicture,
                isInternalStorage: true,
                baseUrl: userContext.RequestBaseUrl);
            profileImageUrl = url;
        }

        return ServiceResultFactory.Success(
            new UpdateProfileResponse(
                DisplayNameEn: input.DisplayNameEn,
                DisplayNameAr: input.DisplayNameAr,
                ProfileImageUrl: profileImageUrl),
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Profile.Updated, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UPDATE PROFILE PICTURE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<string?>> UpdateProfilePictureAsync(
        UpdateProfilePictureRequest request, CancellationToken ct = default)
    {
        var ext      = Path.GetExtension(request.ProfileImage.FileName);
        var fileName = $"{Guid.NewGuid()}{ext}";
        var fileKey  = storageUtility.BuildFileKey(FolderPaths.ProfilePictures, fileName);

        await using var stream = request.ProfileImage.OpenReadStream();
        await fileService.UploadAsync(stream, fileKey, request.ProfileImage.ContentType, ct);

        var dbResult = await profileRepository.UpdateProfilePictureAsync(new UpdateProfilePictureDbInput
        {
            UserId         = userContext.UserId,
            ProfilePicture = fileName
        }, ct);

        if (dbResult.ResultCode != 0)
        {
            await fileService.DeleteAsync(fileKey, ct);
            return ServiceResultFactory.Failure<string?>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Profile.NotFound, ct));
        }

        if (!string.IsNullOrEmpty(dbResult.OldProfilePicture))
        {
            var oldKey = storageUtility.BuildFileKey(FolderPaths.ProfilePictures, dbResult.OldProfilePicture);
            await fileService.DeleteAsync(oldKey, ct);
        }

        var (profileUrl, _) = storageUtility.BuildFilePathWithExpiration(
            FolderPaths.ProfilePictures,
            fileName,
            isInternalStorage: true,
            baseUrl: userContext.RequestBaseUrl);

        return ServiceResultFactory.Success(
            (string?)profileUrl,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Profile.ProfilePictureUpdated, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // REMOVE PROFILE PICTURE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<bool>> RemoveProfilePictureAsync(CancellationToken ct = default)
    {
        var dbResult = await profileRepository.RemoveProfilePictureAsync(userContext.UserId, ct);

        if (dbResult.ResultCode != 0)
            return ServiceResultFactory.Failure<bool>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Profile.NotFound, ct));

        if (!string.IsNullOrEmpty(dbResult.OldProfilePicture))
        {
            var oldKey = storageUtility.BuildFileKey(FolderPaths.ProfilePictures, dbResult.OldProfilePicture);
            await fileService.DeleteAsync(oldKey, ct);
        }

        return ServiceResultFactory.Success(
            true,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Profile.ProfilePictureDeleted, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET SESSIONS
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<IReadOnlyList<SessionItem>>> GetSessionsAsync(
        string? currentRefreshToken, CancellationToken ct = default)
    {
        var currentHash = currentRefreshToken is not null
            ? tokenHasher.Hash(currentRefreshToken)
            : (string?)null;

        var sessions = await profileRepository.GetSessionsAsync(userContext.UserId, currentHash, ct);

        var items = sessions
            .Select(s => new SessionItem(
                Id:               s.TokenId,
                IpAddress:        s.CreatedByIp,
                CreatedOnUtc:     s.CreatedOnUtc,
                ExpiresOnUtc:     s.ExpiresOnUtc,
                IsCurrentSession: s.IsCurrentSession))
            .ToList();

        return ServiceResultFactory.Success(
            (IReadOnlyList<SessionItem>)items,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Profile.GetSessionsSuccess, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // REVOKE SESSION
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<bool>> RevokeSessionAsync(long sessionId, CancellationToken ct = default)
    {
        var dbResult = await profileRepository.RevokeSessionAsync(new RevokeSessionDbInput
        {
            UserId      = userContext.UserId,
            TokenId     = sessionId,
            RevokedByIp = userContext.IpAddress ?? string.Empty
        }, ct);

        if (dbResult.ResultCode != 0)
            return ServiceResultFactory.Failure<bool>(
                InternalResponseCodes.NotFound,
                await messageProvider.GetMessagesAsync(MessageKeys.Profile.SessionNotFound, ct));

        return ServiceResultFactory.Success(
            true,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Profile.SessionRevoked, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // REVOKE ALL OTHER SESSIONS
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<bool>> RevokeAllOtherSessionsAsync(
        string currentRefreshToken, CancellationToken ct = default)
    {
        var currentHash = tokenHasher.Hash(currentRefreshToken);

        await profileRepository.RevokeAllOtherSessionsAsync(new RevokeAllOtherSessionsDbInput
        {
            UserId           = userContext.UserId,
            CurrentTokenHash = currentHash,
            RevokedByIp      = userContext.IpAddress ?? string.Empty
        }, ct);

        return ServiceResultFactory.Success(
            true,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Profile.AllOtherSessionsRevoked, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // REQUEST EMAIL CHANGE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<bool>> RequestEmailChangeAsync(
        RequestEmailChangeRequest request, CancellationToken ct = default)
    {
        var profile = await profileRepository.GetProfileForEmailChangeAsync(userContext.UserId, ct);

        if (profile is null || !profile.IsActive)
            return ServiceResultFactory.Failure<bool>(
                InternalResponseCodes.Unauthorized,
                await messageProvider.GetMessagesAsync(MessageKeys.Common.Unauthorized, ct));

        if (!passwordHasher.Verify(request.CurrentPassword, profile.PasswordHash))
            return ServiceResultFactory.Failure<bool>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Profile.CurrentPasswordIncorrect, ct));

        var newEmail = request.NewEmail.Trim().ToLowerInvariant();

        if (newEmail.Equals(profile.Email, StringComparison.OrdinalIgnoreCase))
            return ServiceResultFactory.Failure<bool>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Profile.EmailSameAsCurrent, ct));

        var emailTaken = await profileRepository.CheckEmailExistsAsync(newEmail, ct);
        if (emailTaken)
            return ServiceResultFactory.Failure<bool>(
                InternalResponseCodes.Conflict,
                await messageProvider.GetMessagesAsync(MessageKeys.Profile.EmailAlreadyInUse, ct));

        var rawToken    = tokenHasher.GenerateRawToken();
        var hashedToken = tokenHasher.Hash(rawToken);
        var expiresAt   = DateTime.UtcNow.AddHours(authOptions.Value.EmailConfirmationExpiryHours);

        await profileRepository.RequestEmailChangeAsync(new RequestEmailChangeDbInput
        {
            UserId       = userContext.UserId,
            NewEmail     = newEmail,
            TokenHash    = hashedToken,
            ExpiresAtUtc = expiresAt,
            CreatedByIp  = userContext.IpAddress
        }, ct);

        var displayName      = LocalizedDisplayName(profile.DisplayNameEn, profile.DisplayNameAr);
        var confirmationLink = BuildEmailChangeLink(authOptions.Value, rawToken);

        await backgroundJobService.EnqueueAsync(
            jobType:  JobTypes.EmailChangeRequested,
            payload:  new EmailChangeRequestedPayload(newEmail, displayName, confirmationLink, profile.Email, userContext.Language),
            priority: 1,
            ct:       ct);

        return ServiceResultFactory.Success(
            true,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Profile.EmailChangeRequested, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CONFIRM EMAIL CHANGE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<bool>> ConfirmEmailChangeAsync(
        ConfirmEmailChangeRequest request, CancellationToken ct = default)
    {
        var tokenHash = tokenHasher.Hash(request.Token);

        var dbResult = await profileRepository.ConfirmEmailChangeAsync(new ConfirmEmailChangeDbInput
        {
            TokenHash = tokenHash,
            UsedByIp  = userContext.IpAddress
        }, ct);

        if (dbResult.ResultCode == 0 && dbResult.OldEmail is not null && dbResult.NewEmail is not null)
        {
            var displayName = LocalizedDisplayName(dbResult.DisplayNameEn!, dbResult.DisplayNameAr);
            var changeTime  = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm 'UTC'");

            await backgroundJobService.EnqueueAsync(
                jobType:  JobTypes.EmailChanged,
                payload:  new EmailChangedPayload(dbResult.OldEmail, displayName, dbResult.NewEmail, changeTime, userContext.Language),
                priority: 1,
                ct:       ct);

            return ServiceResultFactory.Success(
                true,
                InternalResponseCodes.OK,
                await messageProvider.GetMessagesAsync(MessageKeys.Profile.EmailChangeConfirmed, ct));
        }

        return dbResult.ResultCode == 2
            ? ServiceResultFactory.Failure<bool>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Profile.EmailChangeTokenExpired, ct))
            : ServiceResultFactory.Failure<bool>(
                InternalResponseCodes.BadRequest,
                await messageProvider.GetMessagesAsync(MessageKeys.Profile.EmailChangeInvalidToken, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CANCEL EMAIL CHANGE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<bool>> CancelEmailChangeAsync(CancellationToken ct = default)
    {
        await profileRepository.CancelEmailChangeAsync(userContext.UserId, ct);

        return ServiceResultFactory.Success(
            true,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Profile.EmailChangeCancelled, ct));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

    private static string BuildEmailChangeLink(AuthenticationOptions opts, string rawToken)
    {
        var baseUrl = opts.ConfirmEmailBaseUrl;
        var emailChangeUrl = baseUrl.Replace("confirm-email", "confirm-email-change", StringComparison.OrdinalIgnoreCase);
        return $"{emailChangeUrl}?token={Uri.EscapeDataString(rawToken)}";
    }
}
