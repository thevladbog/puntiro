using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Puntiro.Cloud.Configuration;

namespace Puntiro.Cloud.Http;

public static class SensitiveBodyRedaction
{
    public static async Task<T?> ReadJsonAsync<T>(
        HttpContext context,
        CancellationToken cancellationToken)
        where T : class
    {
        var options = context.RequestServices.GetRequiredService<PuntiroCloudOptions>();
        var jsonOptions = context.RequestServices.GetRequiredService<IOptions<JsonOptions>>();
        if (context.Request.ContentLength is > 0 &&
            context.Request.ContentLength > options.Security.MaximumRequestBodyBytes)
        {
            return null;
        }

        if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var mediaType) ||
            !string.Equals(mediaType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var maximum = options.Security.MaximumRequestBodyBytes;
        var buffer = ArrayPool<byte>.Shared.Rent(maximum + 1);
        try
        {
            var read = 0;
            while (read <= maximum)
            {
                var count = await context.Request.Body.ReadAsync(
                    buffer.AsMemory(read, maximum + 1 - read),
                    cancellationToken);
                if (count == 0) break;
                read += count;
            }

            if (read == 0 || read > maximum)
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, read), jsonOptions.Value.SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            buffer.AsSpan(0, maximum + 1).Clear();
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

internal sealed class AdminCsrfMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery,
        PuntiroCloudOptions options)
    {
        var isAdminMutation = context.Request.Path.StartsWithSegments("/api/admin") &&
            !HttpMethods.IsGet(context.Request.Method) &&
            !HttpMethods.IsHead(context.Request.Method) &&
            !HttpMethods.IsOptions(context.Request.Method) &&
            !HttpMethods.IsTrace(context.Request.Method);
        if (!isAdminMutation)
        {
            await next(context);
            return;
        }

        if (context.Request.Headers.Origin.Count != 1 ||
            !SameOrigin(context.Request.Headers.Origin[0], options.Admin.AllowedOrigin))
        {
            await ApiProblem.WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                "auth.csrf_invalid",
                "The request origin or antiforgery token is invalid.");
            return;
        }

        var isLogin = context.Request.Path.Equals(
            "/api/admin/auth/login",
            StringComparison.Ordinal);
        if (isLogin || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
        {
            await ApiProblem.WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                "auth.csrf_invalid",
                "The request origin or antiforgery token is invalid.");
            return;
        }

        await next(context);
    }

    private static bool SameOrigin(string? presented, string configured)
    {
        if (!Uri.TryCreate(presented, UriKind.Absolute, out var left) ||
            !Uri.TryCreate(configured, UriKind.Absolute, out var right))
        {
            return false;
        }

        return string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.IdnHost, right.IdnHost, StringComparison.OrdinalIgnoreCase) &&
            left.Port == right.Port && left.AbsolutePath == "/" &&
            string.IsNullOrEmpty(left.Query) && string.IsNullOrEmpty(left.Fragment);
    }
}

internal sealed class RequestBoundsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, PuntiroCloudOptions options)
    {
        var total = 0;
        foreach (var header in context.Request.Headers)
        {
            total = checked(total + Encoding.UTF8.GetByteCount(header.Key));
            foreach (var value in header.Value)
            {
                total = checked(total + Encoding.UTF8.GetByteCount(value ?? string.Empty));
                if (total > options.Security.MaximumHeaderBytes)
                {
                    await ApiProblem.WriteAsync(
                        context,
                        StatusCodes.Status400BadRequest,
                        "request.invalid",
                        "The request is invalid.");
                    return;
                }
            }
        }

        await next(context);
    }
}
