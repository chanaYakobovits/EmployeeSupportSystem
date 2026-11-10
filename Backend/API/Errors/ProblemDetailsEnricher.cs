using System.Diagnostics;
using API.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace API.Errors;

/// <summary>
/// Adds the identifiers that let a user-reported error be located in the log.
/// </summary>
public static class ProblemDetailsEnricher
{
    public static void AddTraceIds(ProblemDetails problem, HttpContext context)
    {
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
        if (correlationId is not null)
        {
            problem.Extensions["correlationId"] = correlationId;
        }
    }
}
