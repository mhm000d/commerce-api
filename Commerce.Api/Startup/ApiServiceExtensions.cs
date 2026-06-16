using System.Security.Claims;
using System.Threading.RateLimiting;
using Commerce.Api;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace Commerce.Api.Startup;

internal static class ApiStartupDefaults
{
    public const string CorsPolicyName = "ConfiguredCors";

    public static bool IsRateLimitingEnabled(
        IConfiguration configuration,
        IWebHostEnvironment environment) =>
        configuration.GetValue("RateLimiting:Enabled", true)
        && !environment.IsEnvironment("Testing");
}

public static class ApiServiceExtensions
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(type => type.FullName);
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Description = "Enter a JWT access token. Swagger UI adds the Bearer prefix automatically."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
            });
        });

        return services;
    }

    public static IServiceCollection AddConfiguredCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(ApiStartupDefaults.CorsPolicyName, policy =>
            {
                var allowedOrigins = configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>() ?? [];

                if (allowedOrigins.Length == 0)
                    return;

                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database");
        return services;
    }

    public static IServiceCollection AddConfiguredRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        if (!ApiStartupDefaults.IsRateLimitingEnabled(configuration, environment))
            return services;

        var rateLimitWindow = TimeSpan.FromSeconds(
            configuration.GetValue("RateLimiting:WindowSeconds", 60));
        var anonymousPermitLimit = configuration.GetValue("RateLimiting:AnonymousPermitLimit", 120);
        var authenticatedPermitLimit = configuration.GetValue("RateLimiting:AuthenticatedPermitLimit", 600);
        var authEndpointPermitLimit = configuration.GetValue("RateLimiting:AuthEndpointPermitLimit", 30);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? context.User.FindFirstValue("sub");

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: $"global:user:{userId}",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = authenticatedPermitLimit,
                            Window = rateLimitWindow,
                            QueueLimit = 0
                        });
                }

                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"global:ip:{clientIp}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = anonymousPermitLimit,
                        Window = rateLimitWindow,
                        QueueLimit = 0
                    });
            });

            options.AddPolicy(RateLimitPolicies.Auth, context =>
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? context.User.FindFirstValue("sub");
                var partitionKey = !string.IsNullOrWhiteSpace(userId)
                    ? $"auth:user:{userId}"
                    : $"auth:ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authEndpointPermitLimit,
                        Window = rateLimitWindow,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }
}
