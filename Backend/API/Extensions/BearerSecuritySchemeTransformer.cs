using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace API.Extensions;

/// <summary>
/// Adds a "Bearer" security scheme to the OpenAPI document, so Swagger UI shows an Authorize button
/// that accepts an access token and sends it with every request.
/// </summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeId = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token from POST /api/auth/login (without the \"Bearer \" prefix).",
        };

        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = SchemeId },
            }] = [],
        });

        return Task.CompletedTask;
    }
}
