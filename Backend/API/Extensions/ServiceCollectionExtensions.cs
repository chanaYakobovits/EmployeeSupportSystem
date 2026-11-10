using API.Errors;
using API.Middleware;

namespace API.Extensions;

public static class ServiceCollectionExtensions
{
    public const string FrontendCorsPolicy = "Frontend";

    /// <summary>
    /// Registers the HTTP layer: controllers, OpenAPI document, CORS and uniform ProblemDetails.
    /// </summary>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddOpenApi();

        // Spec 6.3: CORS open only to the Angular app. Origins come from configuration
        // (Development: http://localhost:4200); an empty list allows no cross-origin calls at all.
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders(CorrelationIdMiddleware.HeaderName));
        });

        // Errors produced by the framework itself (model validation, 404 for an unknown route, 405, ...)
        // get the same shape as ours: Hebrew title + traceId + correlationId.
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var problem = context.ProblemDetails;
                var status = problem.Status ?? context.HttpContext.Response.StatusCode;

                problem.Title = ProblemTitles.GetTitle(status);
                problem.Type = ProblemTitles.GetType(status);
                ProblemDetailsEnricher.AddTraceIds(problem, context.HttpContext);
            };
        });

        return services;
    }
}
