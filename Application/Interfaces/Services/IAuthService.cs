using Application.Features.Authentication.DTOs;
using Shared.Results;

namespace Application.Interfaces.Services;

public interface IAuthService
{
    Task<ServiceResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<ServiceResult<LoginResponse>>    LoginAsync(LoginRequest request, CancellationToken ct = default);
}
