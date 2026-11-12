using API.Errors;
using Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace API.Middleware;

/// <summary>
/// Single place that converts exceptions to ProblemDetails (spec 11.4):
/// Validation → 400, AuthenticationFailed → 401, NotFound → 404, Conflict → 409, BusinessRule → 422,
/// anything else → 500 without technical details.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away; there is nobody to send a response to.
            _logger.LogInformation("Request was cancelled by the client");
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(exception, "Unhandled exception after the response has started");
                throw;
            }

            var problem = CreateProblem(exception);
            ProblemDetailsEnricher.AddTraceIds(problem, context);
            Log(exception, problem.Status!.Value);

            context.Response.Clear();
            context.Response.StatusCode = problem.Status!.Value;
            await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
        }
    }

    private static ProblemDetails CreateProblem(Exception exception)
    {
        if (exception is AppException appException)
        {
            var status = appException switch
            {
                ValidationException => StatusCodes.Status400BadRequest,
                AuthenticationFailedException => StatusCodes.Status401Unauthorized,
                NotFoundException => StatusCodes.Status404NotFound,
                ConflictException => StatusCodes.Status409Conflict,
                BusinessRuleException => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status500InternalServerError,
            };

            if (status != StatusCodes.Status500InternalServerError)
            {
                var problem = new ProblemDetails
                {
                    Status = status,
                    Type = ProblemTitles.TypeBaseUri + appException.ErrorType,
                    Title = appException.Title,
                    Detail = appException.Detail,
                };

                // Messages by field name (camelCase), so the client can show them next to the fields.
                if (appException.Errors.Count > 0)
                {
                    problem.Extensions["errors"] = appException.Errors;
                }

                return problem;
            }
        }

        // Unexpected: never expose the exception message or stack trace to the client.
        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Type = ProblemTitles.GetType(StatusCodes.Status500InternalServerError),
            Title = ProblemTitles.GetTitle(StatusCodes.Status500InternalServerError),
            Detail = ProblemTitles.UnexpectedDetail,
        };
    }

    private void Log(Exception exception, int status)
    {
        // Expected errors: log the type only (messages are user-facing, no stack trace needed).
        switch (status)
        {
            case StatusCodes.Status400BadRequest:
                _logger.LogInformation("Validation failed: {ErrorType}", (exception as AppException)?.ErrorType);
                break;
            case StatusCodes.Status401Unauthorized:
                // The cause (unknown user / wrong password / lockout) is logged by AuthService, by user id only.
                _logger.LogInformation("Authentication failed: {ErrorType}", (exception as AppException)?.ErrorType);
                break;
            case StatusCodes.Status404NotFound:
                _logger.LogInformation("Not found: {ErrorType}", (exception as AppException)?.ErrorType);
                break;
            case StatusCodes.Status409Conflict:
                _logger.LogWarning("Conflict: {ErrorType}", (exception as AppException)?.ErrorType);
                break;
            case StatusCodes.Status422UnprocessableEntity:
                _logger.LogWarning("Business rule violated: {ErrorType}", (exception as AppException)?.ErrorType);
                break;
            default:
                _logger.LogError(exception, "Unhandled exception");
                break;
        }
    }
}
