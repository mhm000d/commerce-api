using Commerce.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            logger.LogWarning(ex, "Domain exception [{Code}]", ex.Code);
            await WriteErrorAsync(context, ex.StatusCode, ex.Message, ex.Code, ex.Details);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Concurrency conflict");
            await WriteErrorAsync(context, 409, "A conflict occurred, please retry.", "CONCURRENCY_CONFLICT");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, 500, "An unexpected error occurred.", "INTERNAL_ERROR");
        }
    }

    private static Task WriteErrorAsync(
        HttpContext ctx, int status, string message, string code, object? details = null)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";

        // details is omitted from JSON entirely when null — keeps response clean
        object body = details is null
            ? new { error = message, code }
            : new { error = message, code, details };

        return ctx.Response.WriteAsJsonAsync(body);
    }
}
