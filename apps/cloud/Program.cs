using Puntiro.Contracts;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Puntiro.Cloud.Configuration;
using Puntiro.Cloud.Endpoints;
using Puntiro.Cloud.Http;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPuntiroCloud(builder.Configuration, builder.Environment);
var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    var (code, title) = context.Response.StatusCode switch
    {
        StatusCodes.Status404NotFound =>
            ("request.not_found", "The requested resource was not found."),
        StatusCodes.Status405MethodNotAllowed =>
            ("request.method_not_allowed", "The request method is not allowed."),
        StatusCodes.Status413PayloadTooLarge =>
            ("request.too_large", "The request is too large."),
        StatusCodes.Status415UnsupportedMediaType =>
            ("request.unsupported_media_type", "The request media type is not supported."),
        _ => (string.Empty, string.Empty)
    };
    if (code.Length > 0)
    {
        await ApiProblem.WriteAsync(context, context.Response.StatusCode, code, title);
    }
});
app.UseMiddleware<RequestBoundsMiddleware>();
app.UseAuthentication();
app.UsePuntiroAdminCsrf();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "alive",
    protocolVersion = ProtocolVersion.Current
}));

app.MapGet("/health/ready", async (
    HttpContext context,
    HealthCheckService health,
    CancellationToken cancellationToken) =>
{
    var report = await health.CheckHealthAsync(
        registration => registration.Name == "cloud-readiness",
        cancellationToken);
    return report.Status == HealthStatus.Healthy
        ? Results.Ok(new { status = "ready", protocolVersion = ProtocolVersion.Current })
        : ApiProblem.Result(
            context,
            StatusCodes.Status503ServiceUnavailable,
            "configuration.not_ready",
            "The service is not ready.");
}).WithName("CloudReadiness").ExcludeFromDescription();

app.MapAdminAuthEndpoints();
app.MapOpenApi();

app.Run();

public partial class Program;
