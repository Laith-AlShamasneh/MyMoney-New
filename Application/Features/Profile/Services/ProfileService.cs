using Application.Common.Constants;
using Application.Features.Notifications.Services;
using Application.Features.Profile.DbModels;
using Application.Features.Profile.DTOs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Shared.Constants;
using Shared.Enums.System;
using Shared.Results;

namespace Application.Features.Profile.Services;

internal sealed class ProfileService(
    IProfileRepository profileRepository,
    ITokenHasher       tokenHasher,
    IFileService       fileService,
    IStorageUtility    storageUtility,
    IUserContext       userContext,
    IMessageProvider   messageProvider,
    INotificationPublisher notificationPublisher) : IProfileService
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

        await notificationPublisher.PublishAsync(NotificationCodes.ProfilePictureChanged, userContext.UserId, ct: ct);

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
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
