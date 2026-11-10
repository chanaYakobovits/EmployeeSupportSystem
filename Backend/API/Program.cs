using API.Extensions;
using API.Middleware;
using Serilog;
using Services.Extensions;


Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services));

    builder.Services.AddApi(builder.Configuration);
    builder.Services.AddApiAuth(builder.Configuration);
    builder.Services.AddServices(builder.Configuration);

    var app = builder.Build();

    
    if (args.IsSeedCommand())
    {
        Environment.ExitCode = await app.RunSeedAsync();
        return;
    }

    // Order matters: correlation id first, so the request log and error responses carry it;
    // request logging before error handling, so it records the final status code.
    app.UseMiddleware<CorrelationIdMiddleware>();
    // CORS before error handling, so error responses (404/500 ProblemDetails) are readable by the browser too.
    app.UseCors(ServiceCollectionExtensions.FrontendCorsPolicy);
    // Logs method, path, status and duration only – never request/response bodies or headers (spec 6.4).
    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseStatusCodePages(); // ProblemDetails for empty error responses (e.g. unknown route → 404)

    app.UseDevelopmentSwagger();

    app.UseHttpsRedirection();
    // Rate limiting before authentication: a rejected login attempt costs nothing.
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Application terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
