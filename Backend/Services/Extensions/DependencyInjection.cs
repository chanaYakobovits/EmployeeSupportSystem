using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Common.Interfaces;
using Common.Options;
using Repositories.Extensions;
using Services.Implementations;
using Services.Implementations.Ai;
using Services.Interfaces;

namespace Services.Extensions;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the business layer and, through it, the layer below (Repositories).
    /// API calls only this method, so it never needs a reference to Repositories.
    /// </summary>
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRepositories(configuration);

        services.AddSingleton<IPasswordHasher, PasswordHasherService>();
        services.AddSingleton<IPiiRedactor, PiiRedactor>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Failed-login counters of unknown identifiers (spec 4.1). The real users' counters live in the database.
        services.AddMemoryCache();
        services.AddScoped<IAuthService, AuthService>();

        services.AddAi(configuration);

        // Later tasks: services (Scoped); TicketStatusRules (Singleton).
        // Required from the host: ICurrentUser and IAccessTokenIssuer (registered by API).
        return services;
    }

    internal static void AddAi(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AiOptions.SectionName);

        // The API key is deliberately not validated: without it the client returns Disabled and the app still starts.
        services.AddOptions<AiOptions>()
            .Bind(section)
            // The binder ignores an empty value for a nullable property and keeps the default (0.2).
            // "Temperature": "" (or null in JSON) must mean "do not send the parameter".
            .PostConfigure(o =>
            {
                var temperature = section[nameof(AiOptions.Temperature)];
                if (temperature is { Length: 0 })
                {
                    o.Temperature = null;
                }
            })
            .Validate(o => !string.IsNullOrWhiteSpace(o.Model), "Ai:Model is required.")
            .Validate(o => o.TimeoutSeconds is >= 1 and <= 300, "Ai:TimeoutSeconds must be between 1 and 300.")
            .Validate(o => o.MinConfidence is >= 0m and <= 1m, "Ai:MinConfidence must be between 0 and 1.")
            .Validate(o => o.Temperature is null or (>= 0f and <= 2f), "Ai:Temperature must be empty or between 0 and 2.")
            .Validate(o => o.MaxAttempts >= 1 && o.BatchSize >= 1 && o.WorkerIntervalSeconds >= 1 && o.FallbackAfterMinutes >= 1,
                "Ai:MaxAttempts, BatchSize, WorkerIntervalSeconds and FallbackAfterMinutes must be positive.")
            .ValidateOnStart();

        services.AddSingleton<AiResponseValidator>();

        // Typed client (spec 10.3). The per-attempt CancellationTokenSource in OpenAiClient is the timeout,
        // so HttpClient's own 100s timeout is turned off.
        services.AddHttpClient<IAiClient, OpenAiClient>(client => client.Timeout = Timeout.InfiniteTimeSpan);
    }
}
