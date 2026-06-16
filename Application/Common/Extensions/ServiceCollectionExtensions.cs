using Application.Features.Authentication.Services;
using Application.Features.Category.Services;
using Application.Features.Dashboard.Services;
using Application.Features.Notifications;
using Application.Features.Notifications.Services;
using Application.Features.Onboarding.Services;
using Application.Features.Profile.Services;
using Application.Features.Reports;
using Application.Features.Transaction.Services;
using Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();

        return services;
    }
}
