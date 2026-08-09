namespace Puntiro.Cloud.Http;

public sealed record ApiProblemDocument(
    int Status,
    string Code,
    string Title,
    string TraceId,
    string Type,
    string Instance);

public static class ApiProblem
{
    public static IResult Result(HttpContext context, int status, string code, string title)
    {
        return Results.Json(
            new ApiProblemDocument(
                status,
                code,
                title,
                SafeTraceId(context),
                $"https://puntiro.invalid/problems/{code}",
                context.Request.Path),
            statusCode: status,
            contentType: "application/problem+json");
    }

    public static async Task WriteAsync(
        HttpContext context,
        int status,
        string code,
        string title)
    {
        context.Response.StatusCode = status;
        await Result(context, status, code, title).ExecuteAsync(context);
    }

    public static string SafeTraceId(HttpContext context)
    {
        var activityTrace = System.Diagnostics.Activity.Current?.TraceId.ToHexString();
        return string.IsNullOrEmpty(activityTrace)
            ? Guid.NewGuid().ToString("N")
            : activityTrace;
    }
}
