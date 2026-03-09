using System.Net;
using System.Text.Json;
using Relevantz.ExitManagement.Common.Exceptions;

namespace Relevantz.ExitManagement.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            NotFoundException nfe
                => (HttpStatusCode.NotFound, nfe.Message),

            InvalidOperationException ioe
                => (HttpStatusCode.BadRequest, ioe.Message),

            UnauthorizedAccessException uae
                => (HttpStatusCode.Unauthorized, uae.Message),

            ArgumentException ae
                => (HttpStatusCode.BadRequest, ae.Message),

            _   => (HttpStatusCode.InternalServerError,
                    "An unexpected error occurred. Please try again later.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = (int)statusCode;

        var payload = JsonSerializer.Serialize(new
        {
            message,
            statusCode = (int)statusCode
        });

        return context.Response.WriteAsync(payload);
    }
}
