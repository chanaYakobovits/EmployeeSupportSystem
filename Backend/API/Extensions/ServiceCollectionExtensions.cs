using API.Errors;

namespace API.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HTTP layer: controllers, OpenAPI document and uniform ProblemDetails.
    /// </summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddOpenApi();

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
