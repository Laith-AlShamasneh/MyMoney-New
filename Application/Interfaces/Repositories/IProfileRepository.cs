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
}
