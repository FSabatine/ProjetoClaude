using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Fleet.Api.Authorization;
using Fleet.Api.Controllers;
using Fleet.Api.Infrastructure;
using Fleet.Application;
using Fleet.Application.Common;
using Fleet.Infrastructure;
using Fleet.Infrastructure.Persistence;
using Fleet.Infrastructure.Security;
using Fleet.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

if (!builder.Environment.IsDevelopment()) builder.Logging.AddJsonConsole();

// ---- Application layers
builder.Services.AddFleetApplication();
builder.Services.AddFleetInfrastructure(configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HttpCurrentUser>();
// ADR-045: background jobs act for one company through SystemExecutionContext; requests always use the JWT caller.
builder.Services.AddScoped<ICurrentUser, SystemAwareCurrentUser>();
builder.Services.Configure<RefreshCookieOptions>(configuration.GetSection(RefreshCookieOptions.SectionName));
// Relative storage paths live next to the app's content root (not bin/), outside the web root.
builder.Services.PostConfigure<FileStorageOptions>(o =>
{
    if (!Path.IsPathRooted(o.LocalRootPath)) o.LocalRootPath = Path.Combine(builder.Environment.ContentRootPath, o.LocalRootPath);
});

// ---- API, errors
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
    {
        // Malformed JSON / wrong types: same contract and tone as FluentValidation errors.
        // Keys like "$.status" become "status"; the bound parameter itself ("request") is not a field.
        var parameterNames = context.ActionDescriptor.Parameters.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .Select(e => e.Key.TrimStart('$', '.'))
            .Where(key => key.Length > 0 && !parameterNames.Contains(key))
            .Distinct()
            .ToDictionary(key => char.ToLowerInvariant(key[0]) + key[1..], _ => new[] { "Valor em formato inválido." });
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Alguns campos estão em formato inválido. Corrija os itens destacados e tente novamente.",
        };
        problem.Extensions["errors"] = errors;
        problem.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem);
    });
builder.Services.AddExceptionHandler<AppExceptionHandler>();

// ---- Background jobs (ADR-025): time-based operational events
builder.Services.Configure<DocumentExpirationJobOptions>(configuration.GetSection(DocumentExpirationJobOptions.SectionName));
builder.Services.AddHostedService<DocumentExpirationJob>();
builder.Services.Configure<RecurringExpenseGenerationJobOptions>(configuration.GetSection(RecurringExpenseGenerationJobOptions.SectionName));
builder.Services.AddHostedService<RecurringExpenseGenerationJob>();
builder.Services.Configure<AutomationJobOptions>(configuration.GetSection(AutomationJobOptions.SectionName));
builder.Services.AddSingleton<AutomationRunner>();
builder.Services.AddHostedService<AutomationJob>();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    // Friendly titles for responses produced by the framework itself (401/403/404/429 without body).
    ctx.ProblemDetails.Title = ctx.ProblemDetails.Status switch
    {
        StatusCodes.Status401Unauthorized when ctx.Exception is null => "Sua sessão expirou. Entre novamente para continuar.",
        StatusCodes.Status403Forbidden when ctx.Exception is null => "Você não tem permissão para esta ação. Se precisar de acesso, fale com o administrador.",
        StatusCodes.Status404NotFound when ctx.Exception is null => "Recurso não encontrado.",
        StatusCodes.Status429TooManyRequests => "Muitas tentativas em pouco tempo. Aguarde um minuto e tente novamente.",
        _ => ctx.ProblemDetails.Title,
    };
    ctx.ProblemDetails.Type = null;
});

// ---- Authentication (JWT) and authorization (permissions, ADR-006)
var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = jwt.GetSecurityKey(),
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = FleetClaims.Name,
    };
});
builder.Services.AddAuthorization(o =>
{
    // Secure by default: an endpoint without an explicit attribute still requires authentication.
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// ---- Brute force protection on login/refresh
var authRequestsPerMinute = configuration.GetValue("Auth:RequestsPerMinutePerIp", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AuthController.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authRequestsPerMinute, Window = TimeSpan.FromMinutes(1) }));
    // ADR-050: each question may call the AI provider (cost) — limited per user, not per IP.
    var assistantPerMinute = configuration.GetValue("Assistant:RequestsPerMinutePerUser", 12);
    o.AddPolicy(AssistantController.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.FindFirst(FleetClaims.UserId)?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = assistantPerMinute, Window = TimeSpan.FromMinutes(1) }));
    // ADR-051: limited per client IP — the device key is caller-controlled, so it must not choose the bucket
    // (inventing a key per request would bypass the limit). A provider platform pushing many devices from one IP
    // gets a generous default; tune per deployment.
    var ingestPerMinute = configuration.GetValue("Tracking:IngestRequestsPerMinutePerIp", 1200);
    o.AddPolicy(TrackingController.IngestRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = ingestPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddHealthChecks().AddDbContextCheck<FleetDbContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Controle de Frota API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Access token de POST /api/v1/auth/login",
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

await PrepareDatabaseAsync(app);
app.Run();

// Development only (DATABASE.md): production applies migrations in an explicit deploy step.
static async Task PrepareDatabaseAsync(WebApplication app)
{
    if (!app.Configuration.GetValue("Database:MigrateOnStartup", false)) return;

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue("Database:SeedDevelopmentData", false))
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DevDataSeeder>();
        await seeder.SeedAsync(app.Configuration.GetValue("Database:SeedSampleData", false));
    }
}

public partial class Program;
