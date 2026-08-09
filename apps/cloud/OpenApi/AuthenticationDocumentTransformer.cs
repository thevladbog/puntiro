using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Puntiro.Cloud.OpenApi;

public sealed class AuthenticationDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info.Title = "Puntiro Cloud API";
        document.Info.Version = "v1";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes["AdminSession"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = "__Host-puntiro_session",
            Description = "Secure, host-only, server-side admin session."
        };
        document.Components.SecuritySchemes["IntegrationToken"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "Puntiro integration token",
            Description = "Long-lived integration credential sent in one Authorization header."
        };
        return Task.CompletedTask;
    }
}
