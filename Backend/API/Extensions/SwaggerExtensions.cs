namespace API.Extensions;

public static class SwaggerExtensions
{
    private const string OpenApiDocumentPath = "/openapi/v1.json";

    /// <summary>
    /// Development only: serves the OpenAPI document (Microsoft.AspNetCore.OpenApi)
    /// and Swagger UI on top of it at /swagger.
    /// </summary>
    public static WebApplication UseDevelopmentSwagger(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(OpenApiDocumentPath, "EmployeeSupportSystem API v1");
            options.DocumentTitle = "EmployeeSupportSystem API";
        });

        return app;
    }
}
