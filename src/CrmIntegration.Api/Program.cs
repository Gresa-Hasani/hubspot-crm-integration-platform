using System.Text;
using CrmIntegration.Application;
using CrmIntegration.Application.Common;
using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Security;
using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure;
using CrmIntegration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HubSpot CRM Integration & Sales Automation Platform",
        Version = "v1",
        Description = "Internal integration API connecting HubSpot CRM with the internal sales database."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Without this, the JWT handler's default inbound claim-type mapping silently rewrites
        // short claim names ("sub", "email") to long legacy XML-namespace ClaimTypes URIs when
        // reading an incoming token, even though the token was issued with the short names —
        // breaking JwtRegisteredClaimNames.Sub/.Email lookups on User.Claims after validation.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// RBAC — see docs/SECURITY.md "RBAC matrix" for the full role/policy mapping this centralizes.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(AuthorizationPolicies.CanReadCrm, p => p.RequireRole(
        UserRole.Admin.ToString(), UserRole.Operations.ToString(), UserRole.Sales.ToString(), UserRole.ReadOnly.ToString()))
    .AddPolicy(AuthorizationPolicies.CanWriteCrm, p => p.RequireRole(
        UserRole.Admin.ToString(), UserRole.Operations.ToString(), UserRole.Sales.ToString()))
    .AddPolicy(AuthorizationPolicies.CanManageIntegrations, p => p.RequireRole(
        UserRole.Admin.ToString(), UserRole.Operations.ToString()))
    .AddPolicy(AuthorizationPolicies.CanManageAutomations, p => p.RequireRole(
        UserRole.Admin.ToString(), UserRole.Operations.ToString()))
    .AddPolicy(AuthorizationPolicies.CanViewSalesReports, p => p.RequireRole(
        UserRole.Admin.ToString(), UserRole.Operations.ToString(), UserRole.Sales.ToString(), UserRole.ReadOnly.ToString()))
    .AddPolicy(AuthorizationPolicies.CanViewOperationalHealth, p => p.RequireRole(
        UserRole.Admin.ToString(), UserRole.Operations.ToString()))
    .AddPolicy(AuthorizationPolicies.CanManageUsers, p => p.RequireRole(UserRole.Admin.ToString()));

var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
const string CorsPolicyName = "ConfiguredOrigins";
if (corsOptions.AllowedOrigins.Length > 0)
{
    // Never AllowAnyOrigin combined with credentials — only explicitly configured origins, per
    // docs/SECURITY.md "CORS policy". No frontend exists yet (Phase 10); this is inert until
    // Cors:AllowedOrigins is actually configured.
    builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        policy.WithOrigins(corsOptions.AllowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
}

builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));

builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Default") ?? string.Empty,
        name: "postgresql",
        tags: new[] { "ready" });

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? context.TraceIdentifier;
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        var (status, title) = exception switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            DomainValidationException => (StatusCodes.Status400BadRequest, exception.Message),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, exception.Message),
            InvalidRefreshTokenException => (StatusCodes.Status401Unauthorized, exception.Message),
            SyncAmbiguousMatchException => (StatusCodes.Status409Conflict, exception.Message),
            SyncMappingConflictException => (StatusCodes.Status409Conflict, exception.Message),
            DuplicateEmailException => (StatusCodes.Status409Conflict, exception.Message),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, exception.Message),
            HubSpotConflictException => (StatusCodes.Status409Conflict, exception.Message),
            HubSpotRateLimitedException or HubSpotServerException or HubSpotTransientException =>
                (StatusCodes.Status503ServiceUnavailable, "HubSpot is temporarily unavailable; the sync job has been recorded and can be retried."),
            HubSpotApiException => (StatusCodes.Status502BadGateway, "HubSpot rejected the request; see the sync job's FailureCategory for details."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = title,
            Status = status,
            Extensions = { ["correlationId"] = correlationId }
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Standard ASP.NET Core production behavior (docker-compose runs with
    // ASPNETCORE_ENVIRONMENT=Development, so this never triggers for local Docker dev — see
    // docs/SECURITY.md "HTTPS/HSTS"). A real deployment would still sit behind a reverse
    // proxy/load balancer terminating TLS; this is not a claim of a hardened production setup.
    app.UseHsts();
}

app.UseHttpsRedirection();

if (corsOptions.AllowedOrigins.Length > 0)
{
    app.UseCors(CorsPolicyName);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Anonymous by design — see docs/SECURITY.md "Health endpoint security". No sensitive
// diagnostics are returned (just aggregate Healthy/Unhealthy status), and infrastructure probes
// (Docker/Kubernetes/load balancers) generally cannot present a JWT.
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

using (var bootstrapScope = app.Services.CreateScope())
{
    await bootstrapScope.ServiceProvider.GetRequiredService<IAdminBootstrapService>().BootstrapAsync();
}

app.Run();

public partial class Program;
