using Microsoft.AspNetCore.Diagnostics;

namespace Puntiro.Cloud.Http;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        logger.LogError(
            "Unhandled request failure {FailureType} for trace {TraceId}",
            exception.GetType().Name,
            ApiProblem.SafeTraceId(httpContext));
        await ApiProblem.WriteAsync(
            httpContext,
            StatusCodes.Status500InternalServerError,
            "request.failed",
            "The request could not be completed.");
        return true;
    }
}
