using Application.Features.Profile.DTOs;
using Shared.Results;

namespace Application.Interfaces.Services;

public interface IProfileService
{
    Task<ServiceResult<GetProfileResponse>>     GetProfileAsync(CancellationToken ct = default);

    Task<ServiceResult<UpdateProfileResponse>>  UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default);

    Task<ServiceResult<string?>>                UpdateProfilePictureAsync(UpdateProfilePictureRequest request, CancellationToken ct = default);

    Task<ServiceResult<bool>>                   RemoveProfilePictureAsync(CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<SessionItem>>> GetSessionsAsync(string? currentRefreshToken, CancellationToken ct = default);

    Task<ServiceResult<bool>> RevokeSessionAsync(long sessionId, CancellationToken ct = default);

    Task<ServiceResult<bool>> RevokeAllOtherSessionsAsync(string currentRefreshToken, CancellationToken ct = default);

    Task<ServiceResult<bool>> RequestEmailChangeAsync(RequestEmailChangeRequest request, CancellationToken ct = default);

    Task<ServiceResult<bool>> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, CancellationToken ct = default);

    Task<ServiceResult<bool>> CancelEmailChangeAsync(CancellationToken ct = default);
}
