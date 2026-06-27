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
using Infrastructure.Services.Budget;
using Infrastructure.Services.Calendar;
using Infrastructure.Services.CashFlow;
using Infrastructure.Services.FinancialIntelligence;
using Infrastructure.Services.Notifications;
using Infrastructure.Services.Goals;
using Infrastructure.Services.RecurringTransactions;
using Infrastructure.Services.Reports;
using Infrastructure.Services.Transaction;
using Infrastructure.Services.Storage;
using Infrastructure.Services.Storage.Options;
using Infrastructure.Services.Receipt;
using Infrastructure.Services.Ocr;
using Infrastructure.Services.Currency;
using Infrastructure.Services.Workspace;
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

        // 1. Options — critical ones are validated and fail fast at startup.
        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection("Jwt"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer),   "Jwt:Issuer is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
            .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.SecretKey ?? string.Empty) >= 32,
                "Jwt:SecretKey must be supplied out-of-band and be at least 32 bytes.")
            .Validate(o => o.ExpiryMinutes > 0, "Jwt:ExpiryMinutes must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<SmtpOptions>()
            .Bind(config.GetSection("Smtp"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Host),        "Smtp:Host is required.")
            .Validate(o => o.Port is > 0 and <= 65535,               "Smtp:Port must be a valid port.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.FromAddress), "Smtp:FromAddress is required.")
            .ValidateOnStart();

        services.AddOptions<AuthenticationOptions>()
            .Bind(config.GetSection("Authentication"))
            .Validate(o => o.MaxFailedLoginAttempts > 0,     "Authentication:MaxFailedLoginAttempts must be greater than zero.")
            .Validate(o => o.LockoutDurationMinutes > 0,     "Authentication:LockoutDurationMinutes must be greater than zero.")
            .Validate(o => o.PasswordResetExpiryMinutes > 0, "Authentication:PasswordResetExpiryMinutes must be greater than zero.")
            .Validate(o => o.EmailConfirmationExpiryHours > 0, "Authentication:EmailConfirmationExpiryHours must be greater than zero.")
            .ValidateOnStart();

        services.Configure<StorageOptions>(config.GetSection("Storage"));
        services.Configure<BackgroundJobOptions>(config.GetSection("BackgroundJobs"));
        services.Configure<ReceiptOptions>(config.GetSection("Receipts"));

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
        services.AddScoped<IGoalRepository, GoalRepository>();
        services.AddScoped<ICashFlowForecastRepository, CashFlowForecastRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<ICalendarRepository, CalendarRepository>();
        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<ICurrencyRepository, CurrencyRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();

        // 4. Auth & identity services
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenHasher, TokenHasher>();
        services.AddScoped<IUserContext, UserContext>();

        // 5. Cache & Localization
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddScoped<IMessageProvider, MessageProvider>();

        // 6. Storage & OCR
        services.AddSingleton<IStorageUtility, StorageUtility>();
        services.AddSingleton<IFileService, LocalFileService>();
        services.AddSingleton<IFileLinkService, FileLinkService>();
        services.AddSingleton<IOcrProvider, LocalOcrProvider>();

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
        services.AddScoped<IJobHandler, SnapshotRecomputeHandler>();
        services.AddScoped<IJobHandler, ProcessRecurringTransactionsHandler>();
        services.AddScoped<IJobHandler, SendUpcomingPaymentNotificationHandler>();
        services.AddScoped<IJobHandler, GoalBehindScheduleCheckHandler>();
        services.AddScoped<IJobHandler, GoalAutoContributionSyncHandler>();
        services.AddScoped<IJobHandler, ComputeForecastHandler>();
        services.AddScoped<IJobHandler, ComputeBudgetSnapshotHandler>();
        services.AddScoped<IJobHandler, BudgetDailyMaintenanceHandler>();
        services.AddScoped<IJobHandler, CalendarReminderHandler>();
        services.AddScoped<IJobHandler, ProcessReceiptOcrHandler>();
        services.AddScoped<IJobHandler, ExchangeRateSyncHandler>();
        services.AddScoped<IJobHandler, ExchangeRateValidationHandler>();
        services.AddScoped<IJobHandler, WorkspaceInvitationEmailHandler>();
        // The job processor pulls work off the queue; its pick-up SP is atomic, so it is
        // safe to run on every instance under horizontal scale.
        services.AddHostedService<BackgroundJobProcessor>();

        // Timer-based schedulers enqueue work on a wall-clock cadence and track their
        // "last run" in memory — running them on every instance would double-enqueue.
        // Gate them behind a flag: leave true on exactly one instance (or a dedicated
        // scheduler deployment) and set BackgroundJobs:RunSchedulers=false on the rest.
        var runSchedulers = config.GetValue<bool?>("BackgroundJobs:RunSchedulers") ?? true;
        if (runSchedulers)
        {
            services.AddHostedService<NotificationCleanupService>();
            services.AddHostedService<FILSchedulerService>();
            services.AddHostedService<RecurringTransactionSchedulerService>();
            services.AddHostedService<GoalSchedulerService>();
            services.AddHostedService<CashFlowSchedulerService>();
            services.AddHostedService<BudgetSchedulerService>();
            services.AddHostedService<CalendarSchedulerService>();
            services.AddHostedService<CurrencySchedulerService>();
        }

        // 7a. Currency services
        services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
        services.AddSingleton<IExchangeRateProvider, ManualExchangeRateProvider>();

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
