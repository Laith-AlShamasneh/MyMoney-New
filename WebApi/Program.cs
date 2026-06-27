using Application.Common.Extensions;
using WebApi.Features.Authentication;
using WebApi.Features.Category;
using WebApi.Features.Dashboard;
using WebApi.Features.Onboarding;
using WebApi.Features.Profile;
using WebApi.Features.CashFlow;
using WebApi.Features.FinancialIntelligence;
using WebApi.Features.Notifications;
using WebApi.Features.Budget;
using WebApi.Features.Calendar;
using WebApi.Features.Goals;
using WebApi.Features.RecurringTransactions;
using WebApi.Features.Report;
using WebApi.Features.Subscriptions;
using WebApi.Features.Transaction;
using WebApi.Features.Receipt;
using WebApi.Features.Currency;
using WebApi.Features.Workspace;
using FluentValidation;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;
using WebApi.Common.Exceptions;
using WebApi.Common.Middlewares;
using WebApi.Features.Files;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Serilog ────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// ── 2. Architecture layers ────────────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── 3. Validators (scan Application assembly) ─────────────────────────────────
builder.Services.AddValidatorsFromAssembly(
    typeof(Application.Common.Extensions.ServiceCollectionExtensions).Assembly,
    ServiceLifetime.Scoped);

// ── 4. CORS ───────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ── 5. Cross-cutting ──────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ── 5. JWT authentication ─────────────────────────────────────────────────────
// Fail fast: the signing key must be supplied out-of-band (user-secrets in dev,
// the Jwt__SecretKey environment variable in prod) and never committed.
var jwtSecret = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException(
        "Jwt:SecretKey is missing or shorter than 32 bytes. Supply it via user-secrets " +
        "(dev) or the Jwt__SecretKey environment variable (prod). It must never live in appsettings.json.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ── 6. Rate limiting ──────────────────────────────────────────────────────────
// Global per-IP limiter protects every endpoint; the strict "auth" policy is
// applied to credential/token endpoints to blunt brute-force and email-bomb abuse.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window      = TimeSpan.FromMinutes(1),
            QueueLimit  = 0
        });
    });

    options.AddPolicy("auth", context =>
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window      = TimeSpan.FromMinutes(1),
            QueueLimit  = 0
        });
    });
});

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────────────────────

app.UseHttpsRedirection();

// Block anonymous static access to sensitive uploaded artifacts. Receipts and
// reports contain financial PII and are reachable only through their
// authenticated endpoints (or short-lived signed file links). Public assets
// (profile pictures, category icons) remain served by the static middleware.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/uploads/receipts") || path.StartsWithSegments("/uploads/reports"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});
app.UseStaticFiles();

app.UseExceptionHandler(opt => { });

app.UseCors("FrontendPolicy");

app.UseRateLimiter();

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// ── 8. Endpoints ──────────────────────────────────────────────────────────────
app.MapAuthenticationEndpoints();
app.MapProfileEndpoints();
app.MapDashboardEndpoints();
app.MapTransactionEndpoints();
app.MapCategoryEndpoints();
app.MapReportEndpoints();
app.MapNotificationEndpoints();
app.MapFinancialIntelligenceEndpoints();
app.MapCashFlowEndpoints();
app.MapRecurringTransactionEndpoints();
app.MapSubscriptionEndpoints();
app.MapGoalEndpoints();
app.MapBudgetEndpoints();
app.MapCalendarEndpoints();
app.MapOnboardingEndpoints();
app.MapReceiptEndpoints();
app.MapCurrencyEndpoints();
app.MapWorkspaceEndpoints();
app.MapFileEndpoints();

app.Run();
