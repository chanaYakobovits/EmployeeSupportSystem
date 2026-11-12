using System.Text.Json.Serialization;
using API.Errors;
using API.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace API.Extensions;

public static class ServiceCollectionExtensions
{
    public const string FrontendCorsPolicy = "Frontend";

    /// <summary>
    /// Registers the HTTP layer: controllers, OpenAPI document, CORS and uniform ProblemDetails.
    /// </summary>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers(options =>
            {
                // Request DTOs are validated by FluentValidation in the services (Hebrew messages, field keys).
                // Without this, MVC would reject a missing non-nullable string first, with an English message.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                // Body binding errors use the JSON (camelCase) names, like ours.
                options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
                SetHebrewBindingMessages(options.ModelBindingMessageProvider);
            })
            .AddJsonOptions(options =>
            {
                // Enums as names ("Handler", "InProgress"), in requests and responses.
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                // No English System.Text.Json messages to the client; the Hebrew fallback below is used instead.
                options.AllowInputFormatterExceptionMessages = false;
            })
            .ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = BindingErrorResponse);
        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        // Spec 6.3: CORS open only to the Angular app. Origins come from configuration
        // (Development: http://localhost:4200); an empty list allows no cross-origin calls at all.
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Any(origin => origin.Contains('*')))
        {
            // Credentials (the refresh-token cookie) must never be allowed for a wildcard origin.
            throw new InvalidOperationException("Cors:AllowedOrigins must list exact origins; wildcards are not allowed.");
        }

        services.AddCors(options =>
        {
            // AllowCredentials: lets the browser send the refresh-token cookie from the Angular app.
            options.AddPolicy(FrontendCorsPolicy, policy => policy
                .WithOrigins(allowedOrigins)
                .AllowCredentials()
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

    /// <summary>
    /// 400 for model-binding errors (malformed query value or JSON body), in the same shape as FluentValidation errors:
    /// ValidationProblemDetails whose "errors" keys are camelCase field names ("pageSize", "roles[0]").
    /// Query-string keys arrive as C# property names ("PageSize") and JSON ones as paths ("$.roles[0]").
    /// </summary>
    private static IActionResult BindingErrorResponse(ActionContext context)
    {
        var errors = new ModelStateDictionary();
        foreach (var (key, entry) in context.ModelState)
        {
            foreach (var error in entry.Errors)
            {
                errors.AddModelError(ToFieldName(key),
                    string.IsNullOrEmpty(error.ErrorMessage) ? InvalidValueMessage : error.ErrorMessage);
            }
        }

        var problem = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(context.HttpContext, errors, StatusCodes.Status400BadRequest);
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private const string InvalidValueMessage = "הערך אינו תקין";

    private static string ToFieldName(string modelStateKey)
    {
        var key = modelStateKey.StartsWith("$.", StringComparison.Ordinal) ? modelStateKey[2..] : modelStateKey;
        return string.Join('.', key.Split('.').Select(segment =>
            segment.Length == 0 ? segment : char.ToLowerInvariant(segment[0]) + segment[1..]));
    }

    /// <summary>Messages of model-binding errors (malformed query values / body), shown next to the field.</summary>
    private static void SetHebrewBindingMessages(DefaultModelBindingMessageProvider messages)
    {
        messages.SetAttemptedValueIsInvalidAccessor((value, _) => $"הערך '{value}' אינו תקין");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"הערך '{value}' אינו תקין");
        messages.SetUnknownValueIsInvalidAccessor(_ => "הערך אינו תקין");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "הערך אינו תקין");
        messages.SetValueIsInvalidAccessor(value => $"הערך '{value}' אינו תקין");
        messages.SetValueMustBeANumberAccessor(_ => "יש להזין מספר");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "יש להזין מספר");
        messages.SetValueMustNotBeNullAccessor(_ => "יש להזין ערך");
        messages.SetMissingBindRequiredValueAccessor(_ => "יש להזין ערך");
        messages.SetMissingKeyOrValueAccessor(() => "יש להזין ערך");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "גוף הבקשה חסר");
    }
}
