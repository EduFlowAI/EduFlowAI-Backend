using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EduFlowAI.Api.Middlewares
{
    /// <summary>
    /// Converts unhandled exceptions into RFC 7807 ProblemDetails responses with a safe message
    /// and a traceId, and logs the full exception server-side. Prevents leaking internal details
    /// (stack traces, provider error bodies) to API clients.
    /// </summary>
    public sealed class ExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<ExceptionHandler> _logger;

        public ExceptionHandler(ILogger<ExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // The client disconnected mid-request: nothing useful to send, and not an error to alert on.
            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation("Request {Path} was cancelled by the client.", httpContext.Request.Path);
                httpContext.Response.StatusCode = 499; // client closed request
                return true;
            }

            var (status, title, detail) = Map(exception);

            if (status >= StatusCodes.Status500InternalServerError)
                _logger.LogError(exception, "Unhandled exception on {Path}", httpContext.Request.Path);
            else
                _logger.LogWarning(exception, "Handled {Status} on {Path}", status, httpContext.Request.Path);

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            };
            problem.Extensions["traceId"] = httpContext.TraceIdentifier;

            httpContext.Response.StatusCode = status;
            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        // Maps exception types to a status code and a client-safe message. Internal exception text
        // is never returned for 5xx; only our own controlled 404 messages are surfaced.
        private static (int status, string title, string detail) Map(Exception exception) => exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request", "One or more request values were invalid."),
            HttpRequestException => (StatusCodes.Status502BadGateway, "Upstream service error", "The AI service is temporarily unavailable. Please try again in a moment."),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error", "An unexpected error occurred. If it persists, contact support with the traceId.")
        };
    }
}
