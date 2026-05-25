using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Cryptography;
using System.Text;

using TenantPortal.Application.Common.Mappings;
using TenantPortal.Application.Common.Settings;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Identity;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Application.Queries;
using TenantPortal.Application.Services;

using TenantPortal.Infrastructure.Authorization;
using TenantPortal.Infrastructure.Identity;
using TenantPortal.Infrastructure.Persistence;
using TenantPortal.Infrastructure.Repositories;
using TenantPortal.Infrastructure.Services;


public partial class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // -----------------------
        // DB
        // -----------------------
        builder.Services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

        // -----------------------
        // Settings
        // -----------------------
        builder.Services.Configure<TenantSettings>(builder.Configuration.GetSection("TenantSettings"));

        // -----------------------
        // JWT
        // -----------------------
        var jwtSection = builder.Configuration.GetSection("Jwt");
        var issuer = jwtSection["Issuer"] ?? "TenantPortal";
        var audience = jwtSection["Audience"] ?? "TenantPortal";
        var secret = jwtSection["Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");
        var minutes = int.TryParse(jwtSection["ExpiryMinutes"], out var m) ? m : 60;

        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret));

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters.RoleClaimType = "role";
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    RoleClaimType = "role"
                };

                opt.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = async ctx =>
                    {
                        if (ctx.Response.HasStarted) return;

                        ctx.NoResult();
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        ctx.Response.ContentType = "application/json";

                        var code = ctx.Exception is SecurityTokenExpiredException ? "TOKEN_EXPIRED" : "INVALID_TOKEN";
                        await ctx.Response.WriteAsJsonAsync(new { errorCode = code });
                    },

                    OnChallenge = async ctx =>
                    {
                        if (ctx.Response.HasStarted) return;

                        ctx.HandleResponse();
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsJsonAsync(new { errorCode = "AUTH_REQUIRED" });
                    }
                };
            });

        builder.Services.AddAuthorization();


        // -----------------------
        // DI - Core services
        // -----------------------

        builder.Services.AddDataProtection();

        builder.Services.AddHttpContextAccessor();

        builder.Services.AddSingleton<ITokenService>(_ => new JwtTokenService(issuer, audience, secret, minutes));
        builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<GetIndividualsWithSubscriptionQuery>();

        builder.Services.AddScoped<ISessionRepository, SessionRepository>();
        builder.Services.AddScoped<IInvitationRepository, InvitationRepository>();
        builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        builder.Services.AddScoped<IAuthorizationRepository, AuthorizationRepository>();
       builder.Services.AddScoped<IPermissionAuthorizer, PermissionAuthorizer>();

        builder.Services.AddScoped<IGeoScopeContext, GeoScopeContext>();

        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<ITenantRepository, TenantRepository>();
        builder.Services.AddScoped<ITenantService, TenantService>();
        builder.Services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
        builder.Services.AddScoped<IDashboardRepository,DashboardRepository>();
        builder.Services.AddScoped<IDashboardService,DashboardService>();

        builder.Services.AddScoped<IUserManagementRepository, UserManagementRepository>();
        builder.Services.AddScoped<IUserApprovalRequestRepository, UserApprovalRequestRepository>();



        builder.Services.AddScoped<ITenantDetailRepository, TenantDetailRepository>();
        builder.Services.AddScoped<ITenantDetailService, TenantDetailService>();

        builder.Services.AddScoped<ITenantProvisioningService,TenantProvisioningService>();
        //builder.Services.AddScoped<IEmailSender,EmailLogSender>();
        if (builder.Environment.IsDevelopment())
        {
            // TESTING ONLY: no SMTP, just log to EmailLogs
            builder.Services.AddScoped<IEmailSender, EmailLogSender>();
        }
        else
        {
            // REAL ENV: send actual emails
            builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        builder.Services.AddScoped<IEmailSettingsRepository, EmailSettingsRepository>();
        builder.Services.AddScoped<ITenantUserOnboardingService, TenantUserOnboardingService>();
        builder.Services.AddScoped<ITenantSubscriptionRepository, TenantSubscriptionRepository>();
        builder.Services.AddScoped<IErpUserService, ErpUserService>();
        builder.Services.AddScoped<IErpUserRepository, ErpUserRepository>();
     
        builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

        builder.Services.AddAutoMapper(typeof(TenantProfile).Assembly);
        builder.Services.AddScoped<ISecretProtector, SecretProtector>();
        builder.Services.AddScoped<ILookupRepository, LookupRepository>();

        builder.Services.AddScoped<IRbacAdminRepository, RbacAdminRepository>();
        builder.Services.AddScoped<IRbacAdminService, RbacAdminService>();
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<IPermissionCacheInvalidator, PermissionAuthorizer>();
        builder.Services.AddScoped<api.middleware.ServiceApiKeyMiddleware>();


        builder.Services.AddScoped<ITenantContext>(sp =>
        {
            var http = sp.GetRequiredService<IHttpContextAccessor>();
            Guid? tenantId = null;

            var claim = http.HttpContext?.User?.FindFirst("tenant_id")?.Value;
            if (Guid.TryParse(claim, out var t)) tenantId = t;

            return new TenantContext(tenantId);
        });

        // -----------------------
        // Controllers + global auth filter
        // -----------------------
        builder.Services.AddControllers(options =>
        {
            var policy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(policy));
        });

        // -----------------------
        // Swagger
        // -----------------------
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "TenantPortal.API", Version = "v1" });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Paste your JWT access token (no need to type 'Bearer ').",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
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

        var app = builder.Build();

        // -----------------------
        // Seed users
        // -----------------------
        using (var scope = app.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Seeding platform users: START");

            await TenantPortal.Infrastructure.Seed.SeedPlatformUsers.RunAsync(sp);

            logger.LogInformation("Seeding platform users: DONE");
        }

        // -----------------------
        // Exception handler (safe)
        // -----------------------
        app.UseExceptionHandler(appError =>
        {
            appError.Run(async context =>
            {
                if (context.Response.HasStarted) return;

                context.Response.Clear();
                context.Response.ContentType = "application/json";

                var feature = context.Features.Get<IExceptionHandlerFeature>();
                if (feature?.Error == null)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsJsonAsync(new { message = "Internal server error." });
                    return;
                }

                switch (feature.Error)
                {
                    case DomainException ex:
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsJsonAsync(new { errorCode = ex.ErrorCode, message = ex.Message });
                        break;

                    case ForbiddenException ex:
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new { message = ex.Message });
                        break;

                    case NotFoundException ex:
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await context.Response.WriteAsJsonAsync(new { message = ex.Message });
                        break;

                    default:
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        await context.Response.WriteAsJsonAsync(new { message = "Internal server error." });
                        break;
                }
            });
        });

        // -----------------------
        // Middleware pipeline
        // -----------------------
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseHttpsRedirection();

        app.UseAuthentication();
  

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}