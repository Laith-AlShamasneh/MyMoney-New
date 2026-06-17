using Application.Common.Extensions;
using WebApi.Features.Authentication;
using WebApi.Features.Category;
using WebApi.Features.Dashboard;
using WebApi.Features.Onboarding;
using WebApi.Features.Profile;
using WebApi.Features.FinancialIntelligence;
using WebApi.Features.Notifications;
using WebApi.Features.Report;
using WebApi.Features.Transaction;
using FluentValidation;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using WebApi.Common.Exceptions;
using WebApi.Common.Middlewares;

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
var jwtSecret = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey is missing from configuration.");

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

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────────────────────

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseExceptionHandler(opt => { });

app.UseCors("FrontendPolicy");

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
app.MapOnboardingEndpoints();

app.Run();
