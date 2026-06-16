using Commerce.Application.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Commerce.Api.Startup;

public class DatabaseHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Healthy("Database is responding and connection is healthy.");
            }
            return HealthCheckResult.Unhealthy("Cannot establish connection to the database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check failed with an exception.", ex);
        }
    }
}
