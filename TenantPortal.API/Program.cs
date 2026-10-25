using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

using TenantPortal.Application.Common.Mappings;
using TenantPortal.Application.Common.Settings;
using TenantPortal.Application.DTOs.Common;
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
        // Logging (Serilog) — console for live viewing, rolling file so
        // exceptions survive after the console/process is gone.
        // -----------------------
        builder.Host.UseSerilog((ctx, services, cfg) => cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(AppContext.BaseDirectory, "logs", "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30));

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
        // Rate limiting (IP-based, auth endpoints only) — per-account
        // progressive lockout already exists in AuthService, but nothing
        // previously stopped a single IP from spraying many different
        // emails/login attempts per minute.
        // -----------------------
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 10,
                        QueueLimit = 0
                    }));
        });

        // -----------------------
        // CORS (frontend dev servers)
        // -----------------------
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("Frontend", policy =>
            {
                policy.WithOrigins("http://localhost:5173", "https://localhost:5173", "http://localhost:4200", "https://localhost:4200", "http://localhost:4201", "https://localhost:4201")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });


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
        builder.Services.AddScoped<IMfaOtpRepository, MfaOtpRepository>();
        builder.Services.AddScoped<IKnownDeviceRepository, KnownDeviceRepository>();
        builder.Services.AddScoped<ICaptchaVerifier, DevCaptchaVerifier>();
        builder.Services.AddScoped<IDashboardRepository,DashboardRepository>();
        builder.Services.AddScoped<IDashboardService,DashboardService>();

        builder.Services.AddScoped<IUserManagementRepository, UserManagementRepository>();
        builder.Services.AddScoped<IUserApprovalRequestRepository, UserApprovalRequestRepository>();



        builder.Services.AddScoped<ITenantDetailRepository, TenantDetailRepository>();
        builder.Services.AddScoped<ITenantDetailService, TenantDetailService>();
        builder.Services.AddScoped<IAgreementsService, AgreementsService>();
        builder.Services.AddScoped<IOrdersService, OrdersService>();
        builder.Services.AddScoped<ISubscriptionsService, SubscriptionsService>();
        builder.Services.AddSingleton<IFileStorage>(_ => new LocalFileStorage(Path.Combine(builder.Environment.ContentRootPath, "wwwroot")));

        builder.Services.AddScoped<ITenantProvisioningService,TenantProvisioningService>();
        // Always real SMTP, regardless of environment — delivery is gated by
        // the EmailSettings row (Settings > Email in the admin UI), not by
        // dev-vs-prod. SmtpEmailSender already fails clearly ("not
        // configured"/"disabled") when that row is missing or off, so
        // there's no need for a log-only stand-in here.
        builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
        builder.Services.AddScoped<IEmailSettingsRepository, EmailSettingsRepository>();
        builder.Services.AddScoped<ITenantUserOnboardingService, TenantUserOnboardingService>();
        builder.Services.AddScoped<ITenantSubscriptionRepository, TenantSubscriptionRepository>();
        builder.Services.AddScoped<IErpUserService, ErpUserService>();
        builder.Services.AddScoped<IErpUserRepository, ErpUserRepository>();
     
        builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

        builder.Services.AddAutoMapper(typeof(TenantProfile).Assembly);
        builder.Services.AddScoped<ISecretProtector, SecretProtector>();
        builder.Services.AddScoped<ILookupRepository, LookupRepository>();
        builder.Services.AddScoped<IPriceListService, PriceListService>();

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
        })
        // All enums serialize/deserialize as their string names project-wide (e.g. "Excluded",
        // not 0) — and Swashbuckle reflects this in the generated schema/examples too, so the
        // Swagger "Try it out" sample shows the right kind of value instead of a raw number.
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

        // Model-binding/DataAnnotations failures (e.g. a missing [Required] field) would
        // otherwise short-circuit straight to ASP.NET's default ValidationProblemDetails
        // shape, bypassing the unified { isSuccess, errorMessage } contract entirely.
        builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var firstError = context.ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                    ?? "One or more fields are invalid.";

                return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(ApiResult.Failure(firstError));
            };
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

        // Outermost middleware, so the "responded X in Yms" log line reflects
        // the FINAL status code (e.g. 404) after UseExceptionHandler below has
        // translated the exception — not the raw 500 it would see otherwise.
        app.UseSerilogRequestLogging();

        // -----------------------
        // Seed data
        // -----------------------
        using (var scope = app.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var logger = sp.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Seeding core data (lookups, RBAC catalog, roles, first SuperAdmin): START");
            await TenantPortal.Infrastructure.Seed.SeedCoreData.RunAsync(sp);
            logger.LogInformation("Seeding core data: DONE");

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

                var exLogger = context.RequestServices.GetRequiredService<ILogger<Program>>();

                var feature = context.Features.Get<IExceptionHandlerFeature>();
                if (feature?.Error == null)
                {
                    exLogger.LogError("Exception handler invoked with no captured error for {Path}", context.Request.Path);
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsJsonAsync(ApiResult.Failure("Internal server error."));
                    return;
                }

                // Every failure — regardless of which endpoint shape (ApiListResponse/ApiResponse/
                // ApiResult) succeeds on — comes back as this same lean { isSuccess, errorMessage }
                // shape. A client that checks isSuccess first never needs to branch on failure shape.
                switch (feature.Error)
                {
                    case DomainException ex:
                        exLogger.LogWarning(ex, "Domain rule rejected {Method} {Path}: {Message}", context.Request.Method, context.Request.Path, ex.Message);
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsJsonAsync(ApiResult.Failure(ex.Message));
                        break;

                    case ForbiddenException ex:
                        exLogger.LogWarning(ex, "Forbidden on {Method} {Path}: {Message}", context.Request.Method, context.Request.Path, ex.Message);
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(ApiResult.Failure(ex.Message));
                        break;

                    case NotFoundException ex:
                        exLogger.LogWarning(ex, "Not found on {Method} {Path}: {Message}", context.Request.Method, context.Request.Path, ex.Message);
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await context.Response.WriteAsJsonAsync(ApiResult.Failure(ex.Message));
                        break;

                    default:
                        exLogger.LogError(feature.Error, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        await context.Response.WriteAsJsonAsync(ApiResult.Failure("Internal server error."));
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

        // Serves uploaded Agreement documents from wwwroot/uploads/... (see LocalFileStorage).
        app.UseStaticFiles();

        app.UseCors("Frontend");

        app.UseRateLimiter();

        app.UseAuthentication();
  

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}