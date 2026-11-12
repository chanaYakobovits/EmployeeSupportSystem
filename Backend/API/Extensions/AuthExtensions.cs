using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using API.Auth;
using API.Errors;
using Common.Enums;
using Common.Interfaces;
using Common.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Services.Interfaces;

namespace API.Extensions;

public static class AuthExtensions
{
    public const string LoginRateLimitPolicy = "login";
    public const string ChangePasswordRateLimitPolicy = "change-password";
    public const string SetPasswordRateLimitPolicy = "set-password";

    /// <summary>
    /// Decision in CLAUDE.md: 10 set-password attempts per 15 minutes per IP. Tokens cannot be guessed (256 bits);
    /// this only stops hammering the endpoint.
    /// </summary>
    private const int SetPasswordPermitsPerWindow = 10;
    private static readonly TimeSpan SetPasswordWindow = TimeSpan.FromMinutes(15);

    /// <summary>Decision in CLAUDE.md: 5 change-password attempts per 15 minutes per user (against guessing the current password).</summary>
    private const int ChangePasswordPermitsPerWindow = 5;
    private static readonly TimeSpan ChangePasswordWindow = TimeSpan.FromMinutes(15);

    /// <summary>Spec 6.1: 10 login attempts per minute per IP address.</summary>
    private const int LoginPermitsPerWindow = 10;
    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Registers authentication (JWT bearer), authorization policies (spec 6.2), the current user,
    /// the access token issuer and the login rate limit. Fails fast if the signing key is missing.
    /// </summary>
    public static IServiceCollection AddApiAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
        ValidateJwtOptions(jwt);

        services.AddOptions<JwtOptions>().Bind(jwtSection);
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep the short JWT claim names ("sub", "role") instead of the long WS-* URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtAccessTokenIssuer.CreateSigningKey(jwt.SigningKey),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = AuthClaimTypes.UserId,
                    RoleClaimType = AuthClaimTypes.Role,
                    // The default (5 minutes) would stretch a 15-minute token to 20.
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorization(options =>
        {
            // Every endpoint requires a signed-in user with no pending password change, unless marked otherwise:
            // the fallback policy covers endpoints without [Authorize], the default policy those with a bare [Authorize].
            var signedIn = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PasswordChangedRequirement())
                .Build();
            options.DefaultPolicy = signedIn;
            options.FallbackPolicy = signedIn;

            options.AddPolicy(Policies.PasswordChangePending, policy => policy.RequireAuthenticatedUser());

            foreach (var role in Enum.GetValues<Role>())
            {
                options.AddPolicy(role.ToString(), policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PasswordChangedRequirement())
                    .RequireRole(role.ToString()));
            }
        });
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler,
            ProblemDetailsAuthorizationResultHandler>();

        services.AddAuthRateLimits();

        return services;
    }

    private static void ValidateJwtOptions(JwtOptions jwt)
    {
        // The signing key lives only in user-secrets (dev) or environment variables (server) – spec 6.5.
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < JwtOptions.MinSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"JWT signing key '{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}' is missing or shorter than " +
                $"{JwtOptions.MinSigningKeyBytes} bytes. Set it with: " +
                $"dotnet user-secrets set \"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}\" \"<random value, 32+ characters>\" --project API " +
                $"(server: environment variable {JwtOptions.SectionName}__{nameof(JwtOptions.SigningKey)}).");
        }

        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
        {
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience are required (see appsettings.json).");
        }

        if (jwt.AccessTokenMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException("Jwt:AccessTokenMinutes must be between 1 and 60.");
        }

        if (jwt.RefreshTokenDays is < 1 or > 30)
        {
            throw new InvalidOperationException("Jwt:RefreshTokenDays must be between 1 and 30.");
        }
    }

    private static void AddAuthRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(LoginRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    // No reverse proxy yet; behind one, ForwardedHeaders must be configured first.
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = LoginPermitsPerWindow,
                        Window = LoginWindow,
                        QueueLimit = 0,
                    }));

            // Anonymous endpoint: per IP, like login.
            options.AddPolicy(SetPasswordRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    "set-password:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = SetPasswordPermitsPerWindow,
                        Window = SetPasswordWindow,
                        QueueLimit = 0,
                    }));

            // Per user, not per IP. Runs after authorization, so the user is always known here.
            options.AddPolicy(ChangePasswordRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    "user:" + (context.User.FindFirst(AuthClaimTypes.UserId)?.Value ?? "anonymous"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = ChangePasswordPermitsPerWindow,
                        Window = ChangePasswordWindow,
                        QueueLimit = 0,
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Type = ProblemTitles.GetType(StatusCodes.Status429TooManyRequests),
                    Title = ProblemTitles.GetTitle(StatusCodes.Status429TooManyRequests),
                };
                ProblemDetailsEnricher.AddTraceIds(problem, http);

                http.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(AuthExtensions))
                    .LogWarning("Rate limit exceeded on {Path}", http.Request.Path.Value);

                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await http.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null,
                    "application/problem+json", cancellationToken);
            };
        });
    }
}
