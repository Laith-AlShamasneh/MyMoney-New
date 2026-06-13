using Application.Features.Profile.DbModels;

namespace Application.Interfaces.Repositories;

public interface IProfileRepository
{
    Task<GetProfileDbResult?> GetProfileAsync(long userId, CancellationToken ct = default);

    Task<UpdateProfileDbResult>  UpdateProfileAsync(UpdateProfileDbInput input, CancellationToken ct = default);

    Task<UpdateProfilePictureDbResult> UpdateProfilePictureAsync(UpdateProfilePictureDbInput input, CancellationToken ct = default);

    Task<RemoveProfilePictureDbResult> RemoveProfilePictureAsync(long userId, CancellationToken ct = default);

    Task<IReadOnlyList<GetSessionsDbResult>> GetSessionsAsync(long userId, string? currentTokenHash, CancellationToken ct = default);

    Task<RevokeSessionDbResult> RevokeSessionAsync(RevokeSessionDbInput input, CancellationToken ct = default);

    Task RevokeAllOtherSessionsAsync(RevokeAllOtherSessionsDbInput input, CancellationToken ct = default);

    Task<GetProfileForEmailChangeDbResult?> GetProfileForEmailChangeAsync(long userId, CancellationToken ct = default);

    Task<bool> CheckEmailExistsAsync(string email, CancellationToken ct = default);

    Task RequestEmailChangeAsync(RequestEmailChangeDbInput input, CancellationToken ct = default);

    Task<ConfirmEmailChangeDbResult> ConfirmEmailChangeAsync(ConfirmEmailChangeDbInput input, CancellationToken ct = default);

    Task CancelEmailChangeAsync(long userId, CancellationToken ct = default);
}
