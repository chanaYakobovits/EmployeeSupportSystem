using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Common.Interfaces;
using Common.Options;
using Repositories.Extensions;
using Services.Implementations;
using Services.Implementations.Ai;
using Services.Implementations.Email;
using Services.Implementations.Invitations;
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

        // Failed-login counters of unknown identifiers (spec 4.1; the real users' counters live in the database)
        // and the reference data cache.
        services.AddMemoryCache();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISlaPolicyService, SlaPolicyService>();
        services.AddScoped<ITicketService, TicketService>();

        // Departments and categories per organization, 10 minutes (spec 11.3).
        services.AddSingleton<IReferenceDataCache, MemoryReferenceDataCache>();

        // Request validators (spec 11.4), run by the services. Stateless, so one instance each.
        services.AddValidatorsFromAssemblyContaining<Validators.UserListQueryValidator>(ServiceLifetime.Singleton);

        // Organization of the query filters: the signed-in user's, or one entered explicitly by a background job.
        // One instance per scope behind both interfaces.
        services.AddScoped<OrganizationContext>();
        services.AddScoped<IOrganizationContext>(sp => sp.GetRequiredService<OrganizationContext>());
        services.AddScoped<ISystemOrganizationContext>(sp => sp.GetRequiredService<OrganizationContext>());

        services.AddAi(configuration);
        services.AddInvitations(configuration);

        // Later tasks: services (Scoped); TicketStatusRules (Singleton).
        // Required from the host: ICurrentUser, IAccessTokenIssuer and IHostEnvironment (registered by API).
        return services;
    }

    internal static void AddInvitations(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.FromAddress), "Email:FromAddress is required.")
            .Validate(o => o.Provider != EmailProvider.Smtp || !string.IsNullOrWhiteSpace(o.Smtp.Host),
                "Email:Smtp:Host is required when Email:Provider is Smtp.")
            .Validate(o => o.Smtp.Port is > 0 and <= 65535 && o.Smtp.TimeoutSeconds is >= 1 and <= 300,
                "Email:Smtp:Port must be 1-65535 and Email:Smtp:TimeoutSeconds 1-300.")
            .Validate(o => o.Provider != EmailProvider.File || !string.IsNullOrWhiteSpace(o.File.Directory),
                "Email:File:Directory is required when Email:Provider is File.")
            // The .eml files contain the invitation links in clear text.
            .Validate<IHostEnvironment>((o, environment) => o.Provider != EmailProvider.File || environment.IsDevelopment(),
                "Email:Provider 'File' is allowed only in Development. Configure Email:Provider 'Smtp'.")
            .ValidateOnStart();

        services.AddOptions<InvitationOptions>()
            .Bind(configuration.GetSection(InvitationOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.FrontendBaseUrl, UriKind.Absolute, out var uri)
                           && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp),
                "Invitations:FrontendBaseUrl must be an absolute http(s) address of the Angular app.")
            .Validate<IHostEnvironment>((o, environment) => environment.IsDevelopment()
                    || o.FrontendBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
                "Invitations:FrontendBaseUrl must use https outside Development (the link carries a secret token).")
            .Validate(o => o.ExpiryHours is >= 1 and <= 720, "Invitations:ExpiryHours must be between 1 and 720.")
            .Validate(o => o.WorkerIntervalSeconds >= 1 && o.BatchSize >= 1 && o.MaxAttempts >= 1 && o.LockMinutes >= 1,
                "Invitations:WorkerIntervalSeconds, BatchSize, MaxAttempts and LockMinutes must be positive.")
            .Validate(o => o.RetryDelaysMinutes.Length > 0 && o.RetryDelaysMinutes.All(d => d >= 1),
                "Invitations:RetryDelaysMinutes needs at least one value, each at least 1.")
            .ValidateOnStart();

        // Stateless senders; a new SMTP connection per message.
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<FileEmailSender>();
        services.AddSingleton<IEmailSender>(sp =>
            sp.GetRequiredService<IOptions<EmailOptions>>().Value.Provider == EmailProvider.Smtp
                ? sp.GetRequiredService<SmtpEmailSender>()
                : sp.GetRequiredService<FileEmailSender>());

        services.AddSingleton<InvitationEmailComposer>();
        services.AddScoped<IInvitationEmailProcessor, InvitationEmailProcessor>();
        services.AddScoped<IInvitationService, InvitationService>();
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
