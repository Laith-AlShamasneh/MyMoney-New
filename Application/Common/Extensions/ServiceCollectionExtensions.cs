using Application.Features.Authentication.Services;
using Application.Features.Dashboard.Services;
using Application.Features.Profile.Services;
using Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
