using Puntiro.Contracts;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Puntiro.Cloud.Configuration;
using Puntiro.Cloud.Endpoints;
using Puntiro.Cloud.Http;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPuntiroCloud(builder.Configuration, builder.Environment);
var app = builder.Build();

app.UseExceptionHandler();
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
