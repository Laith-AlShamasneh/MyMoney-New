using Application.Features.Authentication.DbModels;

namespace Application.Interfaces.Repositories;

public interface IAuthRepository
{
    Task<bool> CheckEmailExistsAsync(string email, CancellationToken ct = default);
    Task<RegisterDbResult?> RegisterAsync(RegisterDbInput input, CancellationToken ct = default);
    Task SaveRefreshTokenAsync(SaveRefreshTokenDbInput input, CancellationToken ct = default);
}
