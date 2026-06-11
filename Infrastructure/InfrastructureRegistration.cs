using Application.Interfaces.Database;
using Application.Interfaces.Services;
using Infrastructure.Database;
using Infrastructure.Services.Authentication;
using Infrastructure.Services.Authentication.Options;
using Infrastructure.Services.Caching;
using Infrastructure.Services.Localization;
using Infrastructure.Services.Storage;
using Infrastructure.Services.Storage.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // 1. Options
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<StorageOptions>(config.GetSection("Storage"));

        // 2. Database engine
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IDbExecutor, DbExecutor>();

        // 3. Repositories

        // 4. Auth services
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IUserContext, UserContext>();
        //services.AddHttpContextAccessor();

        // 5. Cache & Localization
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddScoped<IMessageProvider, MessageProvider>();

        // 6. Storage
        services.AddSingleton<IStorageUtility, StorageUtility>();

        return services;
    }
}
