using System.Net;
using System.Text.Json;
using ScholarshipApi.Services;

namespace ScholarshipApi.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (EmailNotVerifiedException ex)
        {
            await WriteErrorResponse(context, HttpStatusCode.Forbidden, ex.Message, "email_not_verified");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized access");
            await WriteErrorResponse(context, HttpStatusCode.Forbidden, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "Resource not found");
            await WriteErrorResponse(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Bad request");
            await WriteErrorResponse(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteErrorResponse(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message, string? code = null)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        var body = code is null
            ? JsonSerializer.Serialize(new { error = message })
            : JsonSerializer.Serialize(new { error = message, code });
        await context.Response.WriteAsync(body);
    }
}
