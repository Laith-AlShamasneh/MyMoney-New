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
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using FluentValidation;
using Infrastructure;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;
using WebApi.Common.Exceptions;
using WebApi.Common.Health;
using WebApi.Common.Middlewares;
using WebApi.Features.Files;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;

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

// H8: when enabled, each request's access token must carry a security stamp that
// matches the user's current stamp (cached ~60s). Bumping the stamp (on password
// change) revokes outstanding tokens. Keep off until the H8 migration is applied.
var validateAccessTokenStamp = builder.Configuration.GetValue<bool>("Authentication:ValidateAccessTokenStamp");

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

        if (validateAccessTokenStamp)
        {
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var principal  = context.Principal;
                    var userIdStr  = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var tokenStamp = principal?.FindFirst("sstamp")?.Value;

                    if (!long.TryParse(userIdStr, out var userId) || string.IsNullOrEmpty(tokenStamp))
                    {
                        context.Fail("Missing or invalid security stamp.");
                        return;
                    }

                    var services = context.HttpContext.RequestServices;
                    var cache    = services.GetRequiredService<ICacheService>();
                    var cacheKey = $"sstamp:{userId}";

                    var currentStamp = await cache.GetAsync<string>(cacheKey);
                    if (currentStamp is null)
                    {
                        var authRepo = services.GetRequiredService<IAuthRepository>();
                        currentStamp = (await authRepo.GetSecurityStampAsync(userId))?.ToString() ?? string.Empty;
                        await cache.SetAsync(cacheKey, currentStamp, TimeSpan.FromSeconds(60));
                    }

                    if (!string.Equals(currentStamp, tokenStamp, StringComparison.OrdinalIgnoreCase))
                        context.Fail("Security stamp mismatch.");
                }
            };
        }
    });

builder.Services.AddAuthorization();

// ── 5b. Health checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

// ── 5c. OpenAPI / Swagger ─────────────────────────────────────────────────────
// The UI is only mapped in Development (below). To expose it in another environment,
// put it behind authorization first — it documents the entire API surface.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "MyMoney API", Version = "v1" });

    // "Authorize" button: paste a JWT to send it as the Bearer header on calls.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Paste the JWT access token (without the 'Bearer ' prefix)."
    });
});

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

// API documentation — Development only (gate behind auth before enabling elsewhere).
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

// Correlation id first so every log line for the request — including early
// pipeline and exception logs — carries it.
app.UseMiddleware<CorrelationIdMiddleware>();

// Baseline security response headers applied to every response.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"]        = "DENY";
    headers["Referrer-Policy"]        = "no-referrer";
    await next();
});

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

app.UseAuthentication();
app.UseAuthorization();

// After authentication so the logged UserId reflects the authenticated caller.
app.UseMiddleware<RequestLoggingMiddleware>();

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

// Liveness: process is up (no dependency checks). Readiness: dependencies (DB) are reachable.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
   .DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
   .DisableRateLimiting();

app.Run();
