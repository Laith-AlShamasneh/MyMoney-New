using Application.Features.Authentication.DbModels;

namespace Application.Interfaces.Repositories;

public interface IAuthRepository
{
    Task<bool>              CheckEmailExistsAsync(string email, CancellationToken ct = default);
    Task<RegisterDbResult?> RegisterAsync(RegisterDbInput input, CancellationToken ct = default);
    Task                    SaveRefreshTokenAsync(SaveRefreshTokenDbInput input, CancellationToken ct = default);
    Task<LoginDbResult?>    GetByEmailForLoginAsync(string email, CancellationToken ct = default);
    Task                    UpdateLoginAsync(LoginUpdateDbModel model, CancellationToken ct = default);

    Task                                     SaveConfirmationTokenAsync(SaveConfirmationTokenDbInput input, CancellationToken ct = default);
    Task<ConfirmEmailDbResult>               ConfirmEmailAsync(ConfirmEmailDbInput input, CancellationToken ct = default);
    Task<UserConfirmationStatusDbResult?>    GetUserConfirmationStatusAsync(string email, CancellationToken ct = default);
}
