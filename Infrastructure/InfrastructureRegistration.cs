using Application.Common.Options;
using Application.Interfaces.Database;
using Dapper;
using Application.Interfaces.Jobs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Reports;
using Application.Interfaces.Services;
using Infrastructure.Database;
using Infrastructure.Jobs;
using Infrastructure.Jobs.Handlers;
using Infrastructure.Jobs.Options;
using Infrastructure.Reports.Generators;
using Infrastructure.Services.Authentication;
using Infrastructure.Services.Authentication.Options;
using Infrastructure.Services.Onboarding;
using Infrastructure.Services.Caching;
using Infrastructure.Services.Email;
using Infrastructure.Services.Email.Options;
using Infrastructure.Services.Localization;
using Infrastructure.Services.Category;
using Infrastructure.Services.Dashboard;
using Infrastructure.Services.Profile;
using Infrastructure.Services.FinancialIntelligence;
using Infrastructure.Services.Notifications;
using Infrastructure.Services.RecurringTransactions;
using Infrastructure.Services.Reports;
using Infrastructure.Services.Transaction;
using Infrastructure.Services.Storage;
using Infrastructure.Services.Storage.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // 0. Dapper type handlers
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        // 1. Options
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<StorageOptions>(config.GetSection("Storage"));
        services.Configure<SmtpOptions>(config.GetSection("Smtp"));
        services.Configure<BackgroundJobOptions>(config.GetSection("BackgroundJobs"));
        services.Configure<AuthenticationOptions>(config.GetSection("Authentication"));

        // 2. Database engine
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IDbExecutor, DbExecutor>();

        // 3. Repositories
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IOnboardingRepository, OnboardingRepository>();
        services.AddScoped<IBackgroundJobRepository, BackgroundJobRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IFinancialIntelligenceRepository, FinancialIntelligenceRepository>();
        services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();

        // 4. Auth & identity services
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenHasher, TokenHasher>();
        services.AddScoped<IUserContext, UserContext>();

        // 5. Cache & Localization
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddScoped<IMessageProvider, MessageProvider>();

        // 6. Storage
        services.AddSingleton<IStorageUtility, StorageUtility>();
        services.AddSingleton<IFileService, LocalFileService>();

        // 7. Background jobs
        services.AddScoped<IBackgroundJobService, BackgroundJobService>();
        services.AddScoped<IJobHandler, WelcomeEmailHandler>();
        services.AddScoped<IJobHandler, EmailConfirmationHandler>();
        services.AddScoped<IJobHandler, PasswordResetEmailHandler>();
        services.AddScoped<IJobHandler, PasswordChangedEmailHandler>();
        services.AddScoped<IJobHandler, EmailChangeRequestedHandler>();
        services.AddScoped<IJobHandler, EmailChangedHandler>();
        services.AddScoped<IJobHandler, GenerateReportHandler>();
        services.AddScoped<IJobHandler, ReportCompletedEmailHandler>();
        services.AddScoped<IJobHandler, CreateNotificationHandler>();
        services.AddScoped<IJobHandler, DailyFILJobHandler>();
        services.AddScoped<IJobHandler, HourlyAnomalyJobHandler>();
        services.AddScoped<IJobHandler, MonthlyFILJobHandler>();
        services.AddScoped<IJobHandler, ProcessRecurringTransactionsHandler>();
        services.AddScoped<IJobHandler, SendUpcomingPaymentNotificationHandler>();
        services.AddHostedService<BackgroundJobProcessor>();
        services.AddHostedService<NotificationCleanupService>();
        services.AddHostedService<FILSchedulerService>();
        services.AddHostedService<RecurringTransactionSchedulerService>();

        // 7b. Report generators
        services.AddScoped<IReportGenerator, FinancialSummaryReportGenerator>();
        services.AddScoped<IReportGenerator, TransactionDetailReportGenerator>();
        services.AddScoped<IReportGenerator, IncomeAnalysisReportGenerator>();
        services.AddScoped<IReportGenerator, ExpenseAnalysisReportGenerator>();
        services.AddScoped<IReportGenerator, CategoryAnalysisReportGenerator>();

        // 8. Email
        services.AddSingleton<IEmailService, SmtpEmailService>();
        services.AddSingleton<IEmailTemplateService, EmailTemplateService>();

        return services;
    }
}
