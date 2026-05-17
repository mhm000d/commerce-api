using Commerce.Application.Models;
using Hangfire.Dashboard;

namespace Commerce.Api;

public class HangfireAdminAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Allow unrestricted access in Development for convenience.
        if (httpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment())
            return true;

        return httpContext.User.Identity?.IsAuthenticated == true
               && httpContext.User.IsInRole(nameof(UserRole.Admin));
    }
}
