using Commerce.Api;
using Commerce.Application.Database;
using Commerce.Application.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Startup;

public static class HangfireStartupExtensions
{
    public static WebApplication UseHangfireDashboardAndJobs(this WebApplication app)
    {
        if (app.Environment.IsEnvironment("Testing"))
            return app;

        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new HangfireAdminAuthFilter()]
        });

        RecurringJob.AddOrUpdate<EmailSenderJob>(
            recurringJobId: "email-sender",
            methodCall: job => job.ExecuteAsync(),
            cronExpression: Cron.Minutely());

        RecurringJob.AddOrUpdate<PaymentTimeoutJob>(
            recurringJobId: "payment-timeout",
            methodCall: job => job.ExecuteAsync(CancellationToken.None),
            cronExpression: "*/5 * * * *");

        RecurringJob.AddOrUpdate<CleanupJob>(
            recurringJobId: "cleanup",
            methodCall: job => job.ExecuteAsync(CancellationToken.None),
            cronExpression: "0 2 * * *");

        return app;
    }

    public static async Task MigrateAndSeedDatabaseAsync(this WebApplication app)
    {
        if (app.Environment.IsEnvironment("Testing"))
            return;

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.MigrateAsync();
        await DbSeeder.SeedAsync(dbContext);
    }
}
