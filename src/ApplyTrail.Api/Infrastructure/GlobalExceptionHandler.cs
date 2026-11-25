using ApplyTrail.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApplyTrail.Api.Infrastructure;

/// <summary>
/// Turns exceptions into RFC 7807 problem responses. Expected failures (not found, conflict, ...) carry their own
/// safe message; anything else becomes a generic 500 so internals are never leaked to the client.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problems, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true; // the client went away; nothing to report
        }

        var (status, title, detail) = exception switch
        {
            AppException app => (app.StatusCode, app.Title, app.Detail),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "The request could not be read.", null),
            _ => (StatusCodes.Status500InternalServerError, "Something went wrong on our side.", null),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
