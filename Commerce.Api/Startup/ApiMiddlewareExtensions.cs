using Microsoft.AspNetCore.RateLimiting;

namespace Commerce.Api.Startup;

public static class ApiMiddlewareExtensions
{
    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.UseSwagger();
        app.UseSwaggerUI();

        return app;
    }

    public static WebApplication UseApiSecurity(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseCors(ApiStartupDefaults.CorsPolicyName);
        app.UseAuthentication();

        if (ApiStartupDefaults.IsRateLimitingEnabled(app.Configuration, app.Environment))
        {
            app.UseRateLimiter();
        }

        app.UseAuthorization();

        return app;
    }

    public static WebApplication UseRequestBodyBuffering(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Request.EnableBuffering();
            await next();
        });

        return app;
    }

    public static WebApplication MapApiHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health").DisableRateLimiting();
        return app;
    }
}
