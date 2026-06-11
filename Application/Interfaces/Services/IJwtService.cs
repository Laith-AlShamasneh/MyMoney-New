using System.Security.Claims;
using Application.Features.Authentication.DTOs;

namespace Application.Interfaces.Services;

public interface IJwtService
{
    string GenerateAccessToken(JwtTokenResponse model);
    ClaimsPrincipal? GetPrincipalFromToken(string token);
}
