using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Puntiro.Cloud.Configuration;

namespace Puntiro.Cloud.OpenApi;

public sealed class AuthenticationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var authorization = metadata.OfType<IAuthorizeData>().ToArray();
        if (authorization.Length > 0 &&
            !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security ??= [];
            var integrationScope = authorization
                .Select(item => item.Policy)
                .FirstOrDefault(policy => policy is
                    CloudServiceCollectionExtensions.IntegrationShipmentsReadPolicy or
                    CloudServiceCollectionExtensions.IntegrationShipmentsWritePolicy);
            if (integrationScope is not null)
            {
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(
                        "IntegrationToken",
                        context.Document,
                        null)] = [integrationScope.Replace("integration.", string.Empty, StringComparison.Ordinal)]
                });
                ApplyProblemResponses(
                    operation,
                    context.Document,
                    [
                        new ApiProblemResponseMetadata(
                            StatusCodes.Status401Unauthorized,
                            "integration.invalid_credentials"),
                        new ApiProblemResponseMetadata(
                            StatusCodes.Status403Forbidden,
                            "integration.scope_forbidden"),
                        new ApiProblemResponseMetadata(
                            StatusCodes.Status429TooManyRequests,
                            "integration.rate_limited")
                    ]);
                return Task.CompletedTask;
            }

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

        ApplyProblemResponses(
            operation,
            context.Document,
            metadata.OfType<ApiProblemResponseMetadata>());

        return Task.CompletedTask;
    }

    private static void ApplyProblemResponses(
        OpenApiOperation operation,
        OpenApiDocument? document,
        IEnumerable<ApiProblemResponseMetadata> metadata)
    {
        operation.Responses ??= new OpenApiResponses();
        foreach (var group in metadata.GroupBy(item => item.StatusCode))
        {
            var status = group.Key.ToString(System.Globalization.CultureInfo.InvariantCulture);
            OpenApiResponse response;
            if (!operation.Responses.TryGetValue(status, out var existing))
            {
                response = new OpenApiResponse();
                operation.Responses[status] = response;
            }
            else
            {
                response = existing as OpenApiResponse ?? new OpenApiResponse
                {
                    Description = existing.Description
                };
                operation.Responses[status] = response;
            }

            var codes = group.SelectMany(item => item.Codes)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            response.Description = "Problem codes: " + string.Join(", ", codes) + ".";
            response.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal);
            response.Content["application/problem+json"] = new OpenApiMediaType
            {
                Schema = new OpenApiSchemaReference("ApiProblemDocument", document, null)
            };
        }
    }

    private static bool IsUnsafe(string? method) =>
        !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(method, "TRACE", StringComparison.OrdinalIgnoreCase);
}
