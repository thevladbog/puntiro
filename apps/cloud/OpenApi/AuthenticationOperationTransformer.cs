using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Puntiro.Cloud.OpenApi;

public sealed class AuthenticationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAuthorizeData>().Any() &&
            !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("AdminSession", context.Document, null)] = []
            });

            if (IsUnsafe(context.Description.HttpMethod))
            {
                operation.Parameters ??= [];
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "X-Puntiro-CSRF",
                    In = ParameterLocation.Header,
                    Required = true,
                    Description = "Antiforgery token issued by GET /api/admin/auth/session.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsUnsafe(string? method) =>
        !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(method, "TRACE", StringComparison.OrdinalIgnoreCase);
}
