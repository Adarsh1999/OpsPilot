using Microsoft.AspNetCore.Diagnostics;

namespace OpsPilot.Web.Services;

public sealed class SafeExceptionHandler(ILogger<SafeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError("Unhandled HTTP failure {FailureType}; trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "OpsPilot could not complete this request. Please reload and try again.",
            status = 500,
            traceId = context.TraceIdentifier
        }, cancellationToken);
        return true;
    }
}
